using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avatars.CodeAnalysis;
using Avatars.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;
using static WorkspaceHelper;

namespace Avatars.UnitTests
{
    public class AvatarScaffoldTests
    {
        readonly ITestOutputHelper output;

        public AvatarScaffoldTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public async Task EnsureScaffold()
        {
            var (_, project) = CreateWorkspaceAndProject(LanguageNames.CSharp);
            var compilation = await project.GetCompilationAsync();
            var naming = new NamingConvention();
            var context = new ProcessorContext(compilation, project.ParseOptions);
            var factory = AvatarSyntaxFactory.CreateFactory(LanguageNames.CSharp);
            var symbols = new INamedTypeSymbol[]
            {
                compilation.GetTypeByMetadataName("System.IDisposable") ?? throw new InvalidOperationException(),
                compilation.GetTypeByMetadataName("System.IServiceProvider") ?? throw new InvalidOperationException(),
            };
            var syntax = factory.CreateSyntax(naming, symbols);

            var driver = new SyntaxProcessorDriver(AvatarGenerator.DefaultProcessors.Append(new MemberScaffold()));
            syntax = driver.Process(syntax, context);
            var code = syntax.NormalizeWhitespace().ToFullString();

            if (Debugger.IsAttached)
                output.WriteLine(code);

            Assert.Contains("Dispose", code);
            Assert.Contains("GetService", code);

            compilation = await project
                .AddDocument("test.cs", SourceText.From(code, Encoding.UTF8))
                .Project.GetCompilationAsync();

            var assembly = compilation.Emit(true);
            var type = assembly.GetType(naming.GetFullName(symbols), true);

            Assert.True(typeof(IDisposable).IsAssignableFrom(type));
            Assert.True(typeof(IServiceProvider).IsAssignableFrom(type));
        }
    }
}
