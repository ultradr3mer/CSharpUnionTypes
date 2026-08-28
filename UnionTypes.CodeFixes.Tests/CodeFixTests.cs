using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using UnionTypes.CodeFixes;
using Xunit;

namespace UnionTypes.CodeFixes.Tests;

public class CodeFixTests
{
    private static async Task<(Document doc, List<Diagnostic> diagnostics)> CreateDocumentAsync(
        string source, string filePath = "Source/Example.cs")
    {
        var projectId = ProjectId.CreateNewId();
        var solution = new AdhocWorkspace().CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Union<,>).Assembly.Location));

        var project = solution.GetProject(projectId)!
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var sourceText = SourceText.From(source);
        var docId = DocumentId.CreateNewId(projectId, filePath);
        solution = solution.AddDocument(docId, filePath, sourceText, filePath: filePath);
        var doc = solution.GetDocument(docId)!;

        var compilation = await project.GetCompilationAsync();
        var diagnostics = compilation!.GetDiagnostics().ToList();

        // Also run our analyzer.
        var analyzer = new UnionTypeSyntaxAnalyzer();
        var analyzerDiagnostics = await GetAnalyzerDiagnosticsAsync(doc, analyzer);
        diagnostics.AddRange(analyzerDiagnostics);

        return (doc, diagnostics);
    }

    private static async Task<List<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        Document doc, DiagnosticAnalyzer analyzer)
    {
        var compilation = await doc.Project.GetCompilationAsync();
        var compilationWithAnalyzer = compilation!
            .WithAnalyzers(ImmutableArray.Create((DiagnosticAnalyzer)analyzer));
        var allDiagnostics = await compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync();
        return allDiagnostics.ToList();
    }

    // ── Analyzer: Union<T1,T2> → UT0101 in Source/ files ────────────────

    [Fact]
    public async Task Analyzer_Fires_On_Union_In_Source_File()
    {
        var source = """
            namespace UnionTypes
            {
                public readonly struct Union<T1, T2> { }
            }

            namespace Demo
            {
                public class C
                {
                    public Union<string, int> Field;
                }
            }
            """;

        var (doc, diagnostics) = await CreateDocumentAsync(source, "Source/Example.cs");

        var ut0101 = diagnostics.FirstOrDefault(d => d.Id == "UT0101");
        Assert.NotNull(ut0101);
        Assert.Contains("string", ut0101!.GetMessage());
        Assert.Contains("int", ut0101.GetMessage());
    }

    [Fact]
    public async Task Analyzer_DoesNotFire_Outside_Source_Dir()
    {
        var source = """
            namespace UnionTypes
            {
                public readonly struct Union<T1, T2> { }
            }

            namespace Demo
            {
                public class C
                {
                    public Union<string, int> Field;
                }
            }
            """;

        var (doc, diagnostics) = await CreateDocumentAsync(source, "Example.cs");

        var ut0101 = diagnostics.FirstOrDefault(d => d.Id == "UT0101");
        Assert.Null(ut0101);
    }

    [Fact]
    public async Task Analyzer_DoesNotFire_For_Three_TypeArgs()
    {
        var source = """
            namespace UnionTypes
            {
                public readonly struct Union<T1, T2, T3> { }
            }

            namespace Demo
            {
                public class C
                {
                    public Union<string, int, bool> Field;
                }
            }
            """;

        var (doc, diagnostics) = await CreateDocumentAsync(source, "Source/Example.cs");

        var ut0101 = diagnostics.FirstOrDefault(d => d.Id == "UT0101");
        Assert.Null(ut0101);
    }

    [Fact]
    public async Task Analyzer_Fires_On_Nested_Generic_Union()
    {
        var source = """
            using System.Collections.Generic;
            namespace UnionTypes
            {
                public readonly struct Union<T1, T2> { }
            }

            namespace Demo
            {
                public class C
                {
                    public Union<IEnumerable<int>, int> Field;
                }
            }
            """;

        var (doc, diagnostics) = await CreateDocumentAsync(source, "Source/Example.cs");

        var ut0101 = diagnostics.FirstOrDefault(d => d.Id == "UT0101");
        Assert.NotNull(ut0101);
        Assert.Contains("IEnumerable<int>", ut0101!.GetMessage());
    }

    // ── CodeFix Direction 1: Union<T1,T2> → T1 | T2 ─────────────────────

    [Fact]
    public async Task CodeFix_UnionToPipe_Rewrites()
    {
        var source = """
            namespace UnionTypes
            {
                public readonly struct Union<T1, T2> { }
            }

            namespace Demo
            {
                public class C
                {
                    public Union<string, int> Field;
                }
            }
            """;

        var (doc, diagnostics) = await CreateDocumentAsync(source, "Source/Example.cs");

        var ut0101 = diagnostics.First(d => d.Id == "UT0101");

        var codeFixProvider = new UnionTypeSyntaxCodeFixProvider();
        var codeAction = await ApplyCodeFixAsync(doc, codeFixProvider, ut0101);

        Assert.NotNull(codeAction);

        var newDoc = await ApplyCodeActionAsync(doc, codeAction!);
        var newSource = await newDoc.GetTextAsync();
        var newSourceStr = newSource.ToString();
        Assert.True(newSourceStr.Contains("string | int"), $"Expected 'string | int' in output, got: {newSourceStr}");
        Assert.DoesNotContain("Union<string, int>", newSourceStr);
    }

    [Fact]
    public async Task CodeFix_UnionToPipe_NestedGenerics()
    {
        var source = """
            using System.Collections.Generic;
            namespace UnionTypes
            {
                public readonly struct Union<T1, T2> { }
            }

            namespace Demo
            {
                public class C
                {
                    public Union<IEnumerable<int>, int> Field;
                }
            }
            """;

        var (doc, diagnostics) = await CreateDocumentAsync(source, "Source/Example.cs");

        var ut0101 = diagnostics.First(d => d.Id == "UT0101");

        var codeFixProvider = new UnionTypeSyntaxCodeFixProvider();
        var codeAction = await ApplyCodeFixAsync(doc, codeFixProvider, ut0101);

        Assert.NotNull(codeAction);

        var newDoc = await ApplyCodeActionAsync(doc, codeAction!);
        var newSource = await newDoc.GetTextAsync();
        Assert.Contains("IEnumerable<int> | int", newSource.ToString());
    }

    private static async Task<CodeAction?> ApplyCodeFixAsync(
        Document doc,
        CodeFixProvider provider,
        Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            doc,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions.FirstOrDefault();
    }

    private static async Task<Document> ApplyCodeActionAsync(Document doc, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOp = operations.OfType<ApplyChangesOperation>().First();
        return applyOp.ChangedSolution.GetDocument(doc.Id)!;
    }
}
