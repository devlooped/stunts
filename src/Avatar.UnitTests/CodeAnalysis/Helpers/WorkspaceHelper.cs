using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avatars;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Xunit;

public static class WorkspaceHelper
{
    public static (AdhocWorkspace workspace, Project project) CreateWorkspaceAndProject(
        string language, string assemblyName = "Code", bool includeAvatarApi = true, bool includeMockApi = false)
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = CreateProjectInfo(language, assemblyName, includeAvatarApi, includeMockApi);
        var project = workspace.AddProject(projectInfo);

        return (workspace, project);
    }

    public static ProjectInfo CreateProjectInfo(string language, string assemblyName, bool includeAvatarApi = true, bool includeMockApi = false)
    {
        ParseOptions? parse = default;
        CompilationOptions? options = default;

        var args = CSharpCommandLineParser.Default.Parse(File.ReadAllLines("csc.txt"), ThisAssembly.Project.MSBuildProjectDirectory, sdkDirectory: null);

        if (language == LanguageNames.CSharp)
        {
            parse = args.ParseOptions;
            options = args.CompilationOptions.WithCryptoKeyFile(null).WithOutputKind(OutputKind.DynamicallyLinkedLibrary);
        }

        if (options == null)
        {
            options = language == LanguageNames.CSharp ?
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default) :
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionStrict: OptionStrict.On, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);
        }

        if (parse == null)
        {
            parse = language == LanguageNames.CSharp ?
                new CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest) :
                new VisualBasicParseOptions(Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.Latest);
        }

        var suffix = language == LanguageNames.CSharp ? "CS" : "VB";
        var projectId = ProjectId.CreateNewId();
        var documents = new List<DocumentInfo>();

        var libs = new HashSet<string>(File.ReadAllLines("lib.txt"), StringComparer.OrdinalIgnoreCase)
            .Distinct(FileNameEqualityComparer.Default)
            .ToDictionary(x => Path.GetFileName(x));

        var references = args.MetadataReferences.Select(x =>
            libs.TryGetValue(Path.GetFileName(x.Reference), out var lib) ?
            MetadataReference.CreateFromFile(lib) :
            MetadataReference.CreateFromFile(x.Reference));

        var projectDir = Path.Combine(Path.GetTempPath(), "Test", projectId.Id.ToString());

        return ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            assemblyName + "." + suffix,
            assemblyName + "." + suffix,
            language,
            filePath: language == LanguageNames.CSharp
                ? Path.Combine(projectDir, "code.csproj")
                : Path.Combine(projectDir, "code.vbproj"),
            compilationOptions: options,
            parseOptions: parse,
            metadataReferences: references,
            documents: documents.ToArray());
    }

    public static CancellationToken TimeoutToken(int seconds)
        => Debugger.IsAttached ?
            CancellationToken.None :
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    public static Document AddDocument(this AdhocWorkspace workspace, Project project, string content, string fileName = "code.cs")
        => workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "code.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(content), VersionStamp.Create()))));

    public static Assembly Emit(this Compilation compilation, bool symbols = true)
    {
        using var stream = new MemoryStream();
        var options = symbols ?
            new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded) :
            new EmitOptions();

        var cts = new CancellationTokenSource(10000);
        var result = compilation.Emit(stream,
            options: options,
            cancellationToken: cts.Token);

        result.AssertSuccess();

        stream.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(stream.ToArray());
    }

    public static void AssertSuccess(this EmitResult result)
    {
        if (!result.Success)
        {
            Assert.False(true,
                "Emit failed:\r\n" +
                Environment.NewLine +
                string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Id != "CS0436").Select(d => d.ToString())));
        }
    }
}