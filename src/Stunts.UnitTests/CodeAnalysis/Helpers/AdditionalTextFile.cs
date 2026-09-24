using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Represents a non source code file.
/// </summary>
sealed class AdditionalTextFile : AdditionalText
{
    readonly Lazy<SourceText?> text;

    public AdditionalTextFile(string path)
    {
        Path = path;
        text = new Lazy<SourceText?>(() => SourceText.From(File.ReadAllText(path), Encoding.UTF8));
    }

    /// <summary>
    /// Path to the file.
    /// </summary>
    public override string Path { get; }

    /// <summary>
    /// Returns a <see cref="SourceText"/> with the contents of this file, or <c>null</c> if
    /// there were errors reading the file.
    /// </summary>
    public override SourceText? GetText(CancellationToken cancellationToken = default) => text.Value;
}