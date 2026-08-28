using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnionTypes.CodeFixes;

/// <summary>
/// Analyzer that detects <c>Union&lt;T1, T2&gt;</c> usages in source files under a
/// <c>Source/</c> directory and offers a code fix to rewrite them as
/// <c>T1 | T2</c> union-type syntax.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionTypeSyntaxAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "UT0101";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Union type can use pipe syntax",
        messageFormat: "Union<{0}, {1}> can be written as {0} | {1}",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.Unnecessary });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
    }

    private static void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
    {
        var genericName = (GenericNameSyntax)context.Node;

        if (genericName.Identifier.Text != "Union")
        {
            return;
        }

        var typeArgs = genericName.TypeArgumentList.Arguments;
        if (typeArgs.Count != 2)
        {
            return;
        }

        // Only in files under Source/ directory.
        var filePath = context.Node.SyntaxTree.FilePath;
        if (!IsUnionSourceFile(filePath))
        {
            return;
        }

        // Check the symbol if available, but fall back to syntax-only matching
        // when the semantic model doesn't have enough info (common in test scenarios).
        var symbolInfo = context.SemanticModel.GetSymbolInfo(genericName);
        if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.Name != "Union" || typeSymbol.ContainingNamespace?.Name != "UnionTypes")
            {
                return;
            }

            if (typeSymbol.TypeParameters.Length != 2)
            {
                return;
            }
        }
        // If symbol info is null (unresolved type), we still proceed based on the
        // syntax match: GenericName "Union" with 2 args in a Source/ file.

        var leftType = typeArgs[0];
        var rightType = typeArgs[1];

        var diagnostic = Diagnostic.Create(
            Rule,
            genericName.GetLocation(),
            additionalLocations: new[] { leftType.GetLocation(), rightType.GetLocation() },
            properties: ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, string>("LeftType", leftType.ToString()),
                new KeyValuePair<string, string>("RightType", rightType.ToString()),
            }),
            leftType.ToString(),
            rightType.ToString());

        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsUnionSourceFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

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
}