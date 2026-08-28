using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnionTypes.Suppressors;

/// <summary>
/// Suppresses C# compiler syntax errors that arise from union-type syntax
/// (<c>string | int value;</c>) in source files that live under a <c>Source/</c>
/// directory. These files are transpiled before compilation by the
/// UnionTypes.Transpiler, so the syntax errors are false positives in the IDE.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionSyntaxDiagnosticSuppressor : Microsoft.CodeAnalysis.Diagnostics.DiagnosticSuppressor
{
    private static readonly ImmutableArray<SuppressionDescriptor> Descriptors =
        ImmutableArray.Create(
            CreateSuppression("CS1519"),
            CreateSuppression("CS1003"),
            CreateSuppression("CS1001"),
            CreateSuppression("CS1002"),
            CreateSuppression("CS0117"),
            CreateSuppression("CS0246"),
            CreateSuppression("CS8802"),
            CreateSuppression("CS0101"),
            CreateSuppression("CS8019")
        );

    private static SuppressionDescriptor CreateSuppression(string diagnosticId) =>
        new(
            id: "UTSUPPRESS",
            suppressedDiagnosticId: diagnosticId,
            justification: $"Union-type syntax source file is transpiled before compilation by UnionTypes.Transpiler.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => Descriptors;

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree?.FilePath is not { } path)
            {
                continue;
            }

            if (!IsUnionSourceFile(path))
            {
                continue;
            }

            var sourceText = diagnostic.Location.SourceTree.GetText(context.CancellationToken);
            if (!ContainsUnionSyntax(sourceText.ToString()))
            {
                continue;
            }

            var descriptor = GetDescriptor(diagnostic.Id);
            if (descriptor is null)
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
        }
    }

    private static SuppressionDescriptor? GetDescriptor(string diagnosticId)
    {
        foreach (var d in Descriptors)
        {
            if (d.SuppressedDiagnosticId == diagnosticId)
            {
                return d;
            }
        }
        return null;
    }

    private static bool IsUnionSourceFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (string.Equals(segment, "Source", System.StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsUnionSyntax(string source)
    {
        var inString = false;
        var inChar = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (int i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var prev = i > 0 ? source[i - 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n') inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }

            if (inString)
            {
                if (c == '"' && prev != '\\') inString = false;
                continue;
            }

            if (inChar)
            {
                if (c == '\'' && prev != '\\') inChar = false;
                continue;
            }

            if (c == '/' && i + 1 < source.Length)
            {
                if (source[i + 1] == '/') { inLineComment = true; i++; continue; }
                if (source[i + 1] == '*') { inBlockComment = true; i++; continue; }
            }

            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }

            if (c == '|')
            {
                return true;
            }
        }

        return false;
    }
}