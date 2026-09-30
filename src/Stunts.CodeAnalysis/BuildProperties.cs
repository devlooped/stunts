using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Stunts
{
    /// <summary>Compiler-visible MSBuild properties read by the analyzers and the compile-time generator.</summary>
    static class BuildProperties
    {
        /// <summary>Editorconfig keys for <see cref="BuildProperties"/>.</summary>
        public static class Name
        {
            /// <summary><c>EnableCompileTimeStunts</c>.</summary>
            public const string EnableCompileTimeStunts = "build_property.EnableCompileTimeStunts";

            /// <summary><c>AllowUnsafeBlocks</c>.</summary>
            public const string AllowUnsafeBlocks = "build_property.AllowUnsafeBlocks";

            /// <summary><c>StuntsAnalyzerDir</c>.</summary>
            public const string StuntsAnalyzerDir = "build_property.StuntsAnalyzerDir";

            /// <summary><c>EmitCompilerGeneratedFiles</c>.</summary>
            public const string EmitCompilerGeneratedFiles = "build_property.EmitCompilerGeneratedFiles";

            /// <summary><c>IntermediateOutputPath</c>.</summary>
            public const string IntermediateOutputPath = "build_property.IntermediateOutputPath";

            /// <summary><c>MSBuildProjectDirectory</c>.</summary>
            public const string MSBuildProjectDirectory = "build_property.MSBuildProjectDirectory";

            /// <summary><c>DebugSourceGenerators</c>.</summary>
            public const string DebugSourceGenerators = "build_property.DebugSourceGenerators";

            /// <summary><c>Debug</c> plus the generator name, such as <c>DebugStuntGenerator</c>.</summary>
            public static string Debug(string generator) => "build_property.Debug" + generator;
        }

        /// <summary>
        /// Both <c>EnableCompileTimeStunts</c> and <c>AllowUnsafeBlocks</c> are true.
        /// </summary>
        public static bool CompileTimeStuntsAndUnsafe(AnalyzerConfigOptions options)
            => IsTrue(options, Name.EnableCompileTimeStunts) &&
               IsTrue(options, Name.AllowUnsafeBlocks);

        /// <summary>Directory that contains analyzer dependencies.</summary>
        public static string? StuntsAnalyzerDir(AnalyzerConfigOptions options)
            => Text(options, Name.StuntsAnalyzerDir);

        /// <summary><c>EmitCompilerGeneratedFiles</c> is true.</summary>
        public static bool EmitCompilerGeneratedFiles(AnalyzerConfigOptions options)
            => IsTrue(options, Name.EmitCompilerGeneratedFiles);

        /// <summary>Project intermediate output path.</summary>
        public static string? IntermediateOutputPath(AnalyzerConfigOptions options)
            => Text(options, Name.IntermediateOutputPath);

        /// <summary>Project directory.</summary>
        public static string? MSBuildProjectDirectory(AnalyzerConfigOptions options)
            => Text(options, Name.MSBuildProjectDirectory);

        /// <summary><c>DebugSourceGenerators</c> is true.</summary>
        public static bool DebugSourceGenerators(AnalyzerConfigOptions options)
            => IsTrue(options, Name.DebugSourceGenerators);

        /// <summary><c>Debug</c> plus <paramref name="generator"/> is true.</summary>
        public static bool Debug(AnalyzerConfigOptions options, string generator)
            => IsTrue(options, Name.Debug(generator));

        static bool IsTrue(AnalyzerConfigOptions options, string name)
            => options.TryGetValue(name, out var value) &&
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        static string? Text(AnalyzerConfigOptions options, string name)
            => options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}
