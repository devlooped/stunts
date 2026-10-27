using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Stunts
{
    /// <summary>What the generator needs from a stunt besides its text.</summary>
    [Flags]
    enum StuntCacheFlags
    {
        None = 0,
        /// <summary>The processors left the scaffold unchanged, so nothing is emitted.</summary>
        Unchanged = 1,
        /// <summary>The stunt has no instance constructor (ST006).</summary>
        NoConstructor = 2,
        GenericMethods = 4,
        AotQueryable = 8,
        AotRefStruct = 16,
    }

    /// <summary>A cached generation result.</summary>
    sealed class StuntCacheEntry
    {
        public StuntCacheEntry(StuntCacheFlags flags, IReadOnlyList<string> defaults, string? code)
            => (Flags, Defaults, Code) = (flags, defaults, code);

        public StuntCacheFlags Flags { get; }

        /// <summary>Interfaces whose default implementation class the stunt calls, as <see cref="StuntFingerprint.Name"/>.</summary>
        public IReadOnlyList<string> Defaults { get; }

        /// <summary>The source added for the stunt, before registration nests forwarders in it.</summary>
        public string? Code { get; }

        internal string Write()
        {
            var builder = new StringBuilder()
                .Append(Header).Append('\n')
                .Append("flags ").Append(((int)Flags).ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var name in Defaults)
                builder.Append("default ").Append(name).Append('\n');

            var code = Code ?? "";
            return builder.Append("code ").Append(code.Length.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append(code).ToString();
        }

        internal static StuntCacheEntry? Read(string text)
        {
            var position = 0;
            if (Line(text, ref position) != Header)
                return null;

            var flags = StuntCacheFlags.None;
            var defaults = new List<string>();
            while (Line(text, ref position) is string line)
            {
                if (line.StartsWith("flags ", StringComparison.Ordinal) &&
                    int.TryParse(line.Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    flags = (StuntCacheFlags)value;
                else if (line.StartsWith("default ", StringComparison.Ordinal))
                    defaults.Add(line.Substring(8));
                else if (line.StartsWith("code ", StringComparison.Ordinal) &&
                    int.TryParse(line.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                    // A length mismatch is a damaged or foreign file: regenerate.
                    return text.Length - position == length
                        ? new StuntCacheEntry(flags, defaults, (flags & (StuntCacheFlags.Unchanged | StuntCacheFlags.NoConstructor)) != 0 ? null : text.Substring(position))
                        : null;
                else
                    return null;
            }

            return null;
        }

        const string Header = "stunts-cache 1";

        static string? Line(string text, ref int position)
        {
            var end = text.IndexOf('\n', position);
            if (end < 0)
                return null;

            var line = text.Substring(position, end - position);
            position = end + 1;
            return line;
        }
    }

    /// <summary>
    /// Generation results shared by builds and projects, keyed by <see cref="StuntFingerprint"/>.
    /// Entries live in a folder per generator version under <c>StuntsCacheDir</c>, plus an 
    /// in-memory copy for the lifetime of the compiler server process.
    /// </summary>
    sealed class StuntCache
    {
        const string Extension = ".stunt";
        // Characters of cached code kept in memory before it is dropped.
        const long MemoryBudget = 32L * 1024 * 1024;
        static readonly TimeSpan PruneInterval = TimeSpan.FromDays(1);
        static readonly TimeSpan TouchAge = TimeSpan.FromDays(7);
        static readonly TimeSpan EntryAge = TimeSpan.FromDays(30);
        static readonly TimeSpan VersionAge = TimeSpan.FromDays(7);

        static readonly ConcurrentDictionary<string, StuntCacheEntry> memory = new(StringComparer.Ordinal);
        static long memorySize;

        readonly string root;
        readonly string directory;
        int hits;
        int misses;

        StuntCache(string root, string directory) => (this.root, this.directory) = (root, directory);

        /// <summary>Opens the cache under <c>StuntsCacheDir</c>, or returns null when it is not set.</summary>
        public static StuntCache? Create(AnalyzerConfigOptions options, string version)
        {
            if (BuildProperties.StuntsCacheDir(options) is not string root)
                return null;

            try
            {
                root = Path.GetFullPath(root);
                var directory = Path.Combine(root, version);
                Directory.CreateDirectory(directory);
                var cache = new StuntCache(root, directory);
                cache.Prune();
                return cache;
            }
            catch (Exception ex) when (IsIOException(ex))
            {
                return null;
            }
        }

        public int Hits => hits;

        public int Misses => misses;

        public StuntCacheEntry? Get(string key)
        {
            var path = Path.Combine(directory, key + Extension);
            // Keyed by path, so each cache folder keeps its own entries.
            if (memory.TryGetValue(path, out var entry))
            {
                Interlocked.Increment(ref hits);
                return entry;
            }

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    Interlocked.Increment(ref misses);
                    return null;
                }

                entry = StuntCacheEntry.Read(File.ReadAllText(path, Encoding.UTF8));
                if (entry == null)
                {
                    Interlocked.Increment(ref misses);
                    return null;
                }

                // Entries in use survive pruning.
                if (DateTime.UtcNow - info.LastWriteTimeUtc > TouchAge)
                    File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }
            catch (Exception ex) when (IsIOException(ex))
            {
                Interlocked.Increment(ref misses);
                return null;
            }

            Remember(path, entry);
            Interlocked.Increment(ref hits);
            return entry;
        }

        public void Add(string key, StuntCacheEntry entry)
        {
            var path = Path.Combine(directory, key + Extension);
            Remember(path, entry);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (File.Exists(path))
                    return;

                File.WriteAllText(temp, entry.Write(), new UTF8Encoding(false));
                // Another build may have stored the same entry since. Its content is identical.
                try
                {
                    File.Move(temp, path);
                }
                catch (IOException)
                {
                    File.Delete(temp);
                }
            }
            catch (Exception ex) when (IsIOException(ex))
            {
                try { File.Delete(temp); } catch (Exception e) when (IsIOException(e)) { }
            }
        }

        /// <summary>Drops the in-memory entries, so the next reads come from disk.</summary>
        internal static void ClearMemory()
        {
            memory.Clear();
            Interlocked.Exchange(ref memorySize, 0);
        }

        static void Remember(string path, StuntCacheEntry entry)
        {
            if (memory.TryAdd(path, entry) &&
                Interlocked.Add(ref memorySize, entry.Code?.Length ?? 0) > MemoryBudget)
                ClearMemory();
        }

        // At most once a day: entries not read for a month, temporary files, and the 
        // folders of generator versions no build has written to for a week.
        void Prune()
        {
            var marker = Path.Combine(root, ".pruned");
            var now = DateTime.UtcNow;
            if (File.Exists(marker) && now - File.GetLastWriteTimeUtc(marker) < PruneInterval)
                return;

            File.WriteAllText(marker, now.ToString("o", CultureInfo.InvariantCulture));
            foreach (var version in Directory.EnumerateDirectories(root))
            {
                try
                {
                    if (!string.Equals(version, directory, StringComparison.OrdinalIgnoreCase) &&
                        now - Directory.GetLastWriteTimeUtc(version) > VersionAge)
                    {
                        Directory.Delete(version, true);
                        continue;
                    }

                    foreach (var file in Directory.EnumerateFiles(version))
                    {
                        var age = now - File.GetLastWriteTimeUtc(file);
                        if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ? age > PruneInterval : age > EntryAge)
                            File.Delete(file);
                    }
                }
                catch (Exception ex) when (IsIOException(ex))
                {
                }
            }
        }

        static bool IsIOException(Exception ex)
            => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or ArgumentException;
    }
}
