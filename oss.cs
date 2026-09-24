// First run: dnx runfile https://github.com/devlooped/oss/blob/main/oss.cs --yes --alias oss
// Subsequently: dnx runfile oss --yes
#:package Spectre.Console@*
#:package CliWrap@*
#:package ConsoleAppFramework@*

using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using ConsoleAppFramework;
using Spectre.Console;

await ConsoleApp.RunAsync(args, async (
    /// <summary>Project name</summary>
    string? project = null,
    /// <summary>Repo name (defaults to project name)</summary>
    string? repo = null,
    /// <summary>Package ID (defaults to Devlooped.{project})</summary>
    string? package = null) =>
{
    // Framework-dependent / R2R: muxer is three levels above the shared runtime.
    // Native AOT (e.g. dnx go default publish) reports the app dir as the runtime
    // directory, so that walk is wrong — use DOTNET_HOST_PATH / DOTNET_ROOT / PATH.
    var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
    var dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", fileName));
    if (!File.Exists(dotnet))
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
            dotnet = host;
        else if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root
            && File.Exists(Path.Combine(root, fileName)))
            dotnet = Path.Combine(root, fileName);
        else
            dotnet = fileName; // PATH
    }

    AnsiConsole.Write(new FigletText("devlooped oss").Color(Color.Green));
    AnsiConsole.WriteLine();

    var projectName = project ?? AnsiConsole.Prompt(
        new TextPrompt<string>("[green]Project name[/]:")
            .PromptStyle("yellow")
            .ValidationErrorMessage("[red]Project name cannot be empty[/]")
            .Validate(v => !string.IsNullOrWhiteSpace(v)));

    var repoName = repo ?? AnsiConsole.Prompt(
        new TextPrompt<string>("[green]Repo name[/]:")
            .PromptStyle("yellow")
            .DefaultValue(projectName));

    var packageId = package ?? AnsiConsole.Prompt(
        new TextPrompt<string>("[green]Package ID[/]:")
            .PromptStyle("yellow")
            .DefaultValue($"Devlooped.{projectName}"));

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[yellow]Setting up OSS project[/]").RuleStyle("grey").LeftJustified());
    AnsiConsole.WriteLine();

    await RunDotNet("file init https://github.com/devlooped/oss/blob/main/.netconfig", "Initializing dotnet file sync from devlooped/oss");
    await RunDotNet($"new classlib -n {projectName} -o src/{projectName} -f net10.0", $"Creating class library src/{projectName}");
    File.Delete($"src/{projectName}/Class1.cs");
    await RunDotNet($"package add NuGetizer --project src/{projectName}/{projectName}.csproj", "Adding NuGetizer");
    await RunDotNet($"new xunit -n Tests -o src/Tests -f net10.0", "Creating xUnit test project src/Tests");
    File.Delete($"src/Tests/UnitTest1.cs");
    await RunDotNet($"add src/Tests/Tests.csproj reference src/{projectName}/{projectName}.csproj", $"Adding reference from Tests to {projectName}");

    var doc = XDocument.Load($"src/{projectName}/{projectName}.csproj", LoadOptions.None);
    doc.Root?.Element("PropertyGroup")?.Element("ImplicitUsings")?.Remove();
    doc.Root?.Element("PropertyGroup")?.Element("Nullable")?.Remove();
    doc.Root?.Element("PropertyGroup")?.Add(new XElement("PackageId", packageId));
    doc.Save($"src/{projectName}/{projectName}.csproj");

    doc = XDocument.Load($"src/Tests/Tests.csproj", LoadOptions.None);
    doc.Root?.Element("PropertyGroup")?.Element("ImplicitUsings")?.Remove();
    doc.Root?.Element("PropertyGroup")?.Element("Nullable")?.Remove();
    doc.Root?.Element("PropertyGroup")?.Element("IsPackable")?.Remove();
    doc.Save($"src/Tests/Tests.csproj");

    File.WriteAllText($"src/Directory.props",
        $"""
        <Project>
          <PropertyGroup>
            <Product>{projectName}</Product>
            <ImplicitUsings>true</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """);

    await RunDotNet($"new solution -n {projectName}", $"Creating solution {projectName}.slnx");
    await RunDotNet($"sln {projectName}.slnx add --in-root src/{projectName}/{projectName}.csproj src/Tests/Tests.csproj", $"Adding projects to {projectName}.slnx");

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .StartAsync("Downloading readme.md template...", async ctx =>
        {
            using var http = new HttpClient();
            var readmeContent = await http.GetStringAsync(
                "https://raw.githubusercontent.com/devlooped/oss/main/readme.tmp.md");

            readmeContent = readmeContent
                .Replace("{{PROJECT_NAME}}", projectName)
                .Replace("{{PACKAGE_ID}}", packageId)
                .Replace("{{REPO_NAME}}", repoName);

            await File.WriteAllTextAsync("readme.md", readmeContent);
            ctx.Status("Downloaded and processed readme.md");
        });

    AnsiConsole.MarkupLine("[green]✓[/] Created [yellow]readme.md[/]");

    await File.WriteAllTextAsync(
        Path.Combine("src", projectName, "readme.md"),
        $"""
        [![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](osmfeula.txt)
        [![OSS](https://img.shields.io/github/license/devlooped/oss.svg?color=blue)](license.txt)
        [![GitHub](https://img.shields.io/badge/-source-181717.svg?logo=GitHub)](https://github.com/devlooped/{repoName})

        <!-- include ../../readme.md#content -->

        <!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->

        <!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->

        <!-- exclude -->
        """);

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[green]Done![/]").RuleStyle("grey").LeftJustified());
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[bold]Project:[/] [yellow]{projectName}[/]");
    AnsiConsole.MarkupLine($"[bold]Repo:[/] [yellow]{repoName}[/]");
    AnsiConsole.MarkupLine($"[bold]Package ID:[/] [yellow]{packageId}[/]");

    async Task RunDotNet(string command, string description)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(description)}...[/]");
        await Cli.Wrap(dotnet)
            .WithArguments(command)
            .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
            .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
            .ExecuteAsync();
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(description)}");
    }
});