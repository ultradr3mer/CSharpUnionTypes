using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace UnionTypes.CodeFixes;

/// <summary>
/// Code fix provider that:
/// <list type="number">
/// <item>Rewrites <c>Union&lt;T1, T2&gt;</c> to <c>T1 | T2</c> (diagnostic UT0101).</item>
/// <item>Rewrites <c>T1 | T2</c> back to <c>global::UnionTypes.Union&lt;T1, T2&gt;</c>
/// for compiler syntax errors CS1003/CS1519 near a <c>|</c> token.</item>
/// </list>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnionTypeSyntaxCodeFixProvider))]
public sealed class UnionTypeSyntaxCodeFixProvider : CodeFixProvider
{
    public const string UnionToPipeTitle = "Rewrite as union-type syntax: T1 | T2";
    public const string PipeToUnionTitle = "Replace with Union<T1, T2>";

    private static readonly string[] FixableCompilerErrorIds =
        { "CS1003", "CS1519", "CS1001", "CS1002" };

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.CreateRange(new[] { UnionTypeSyntaxAnalyzer.DiagnosticId }.Concat(FixableCompilerErrorIds));

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();

        if (diagnostic.Id == UnionTypeSyntaxAnalyzer.DiagnosticId)
        {
            RegisterUnionToPipeFix(context, diagnostic);
        }
        else if (FixableCompilerErrorIds.Contains(diagnostic.Id))
        {
            await RegisterPipeToUnionFixAsync(context, diagnostic);
        }
    }

    private static void RegisterUnionToPipeFix(CodeFixContext context, Diagnostic diagnostic)
    {
        var leftType = diagnostic.Properties.GetValueOrDefault("LeftType") ?? "";
        var rightType = diagnostic.Properties.GetValueOrDefault("RightType") ?? "";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: UnionToPipeTitle,
                createChangedDocument: c => RewriteUnionToPipeAsync(context.Document, diagnostic, c),
                equivalenceKey: nameof(UnionToPipeTitle)),
            diagnostic);
    }

    private static async Task RegisterPipeToUnionFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var sourceText = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        var source = sourceText.ToString();

        // Find the `|` token near the diagnostic location.
        var diagSpan = diagnostic.Location.SourceSpan;
        var pipePos = FindPipeNear(source, diagSpan.Start);
        if (pipePos < 0) return;

        // Parse the union type using the token-based parser.
        var tokens = Tokenize(source);
        var (pipeTokenIdx, leftText, rightText) = FindUnionAroundPipe(tokens, pipePos);
        if (leftText is null || rightText is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: PipeToUnionTitle,
                createChangedDocument: c => RewritePipeToUnionAsync(context.Document, tokens, pipeTokenIdx, leftText, rightText, c),
                equivalenceKey: nameof(PipeToUnionTitle)),
            diagnostic);
    }

    // ── Direction 1: Union<T1, T2> → T1 | T2 ───────────────────────────

    private static async Task<Document> RewriteUnionToPipeAsync(
        Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        // Find the GenericNameSyntax at the diagnostic location, walking up if needed.
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var genericName = node as GenericNameSyntax ?? node.AncestorsAndSelf().OfType<GenericNameSyntax>().FirstOrDefault();
        if (genericName is null)
        {
            return document;
        }

        var args = genericName.TypeArgumentList.Arguments;
        if (args.Count != 2) return document;

        // Build the replacement: "T1 | T2"
        // Since `|` is not valid C# type syntax, we can't use ParseTypeName.
        // Instead, do a text replacement on the source.
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var source = sourceText.ToString();

        var fullSpan = genericName.FullSpan;
        var replacement = $"{args[0]} | {args[1]}";

        var newSource = source.Substring(0, fullSpan.Start) +
                        replacement +
                        source.Substring(fullSpan.End);

        // Parse the new source into a syntax tree (with errors, since `|` is invalid).
        var newTree = CSharpSyntaxTree.ParseText(newSource, path: document.Name,
            encoding: sourceText.Encoding,
            cancellationToken: cancellationToken);

        var newRoot = await newTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(newRoot);
    }

    // ── Direction 2: T1 | T2 → global::UnionTypes.Union<T1, T2> ─────────

    private static async Task<Document> RewritePipeToUnionAsync(
        Document document,
        List<SyntaxToken> tokens,
        int pipeTokenIdx,
        string leftTypeText,
        string rightTypeText,
        CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var source = sourceText.ToString();

        // Find the start of the left type and end of the right type in the token stream.
        // We need to parse from the pipe backwards and forwards.
        // pipeTokenIdx is the index of `|` in the tokens list.
        // The left type ends just before `|`, the right type starts just after.
        // We need to find the span of the entire `T1 | T2` expression.

        // Parse left type backwards from pipe
        int leftEnd = pipeTokenIdx;
        int leftStart = leftEnd;
        // Walk backwards to find the start of the left type
        // (simplified: use the token parser forwards from a trigger point)

        // Actually, let's find the full span by parsing forward from a position before the pipe.
        // We'll scan backwards to find a likely type-start position, then parse forward.

        // Simpler approach: find the replacement span directly from tokens.
        // Left type: from some token before pipe to pipe-1
        // Right type: from pipe+1 to some token after

        // Use the token parser to determine the spans.
        var pipeToken = tokens[pipeTokenIdx];
        int pipeStart = pipeToken.SpanStart;
        int pipeEnd = pipeToken.Span.End;

        // Find left type start: walk backwards skipping type tokens
        int leftTokenStart = FindLeftTypeStart(tokens, pipeTokenIdx);
        if (leftTokenStart < 0) return document;

        // Find right type end: walk forwards parsing the type
        int rightTokenEnd = FindRightTypeEnd(tokens, pipeTokenIdx);
        if (rightTokenEnd < 0) return document;

        int replaceStart = tokens[leftTokenStart].SpanStart;
        int replaceEnd = tokens[rightTokenEnd].Span.End;

        // Build the replacement text.
        string unionText = $"global::UnionTypes.Union<{leftTypeText}, {rightTypeText}>";

        var newSource = source.Substring(0, replaceStart) + unionText + source.Substring(replaceEnd);

        // Parse the new source to get a syntax tree.
        var newTree = CSharpSyntaxTree.ParseText(newSource, path: document.Name,
            encoding: sourceText.Encoding,
            cancellationToken: cancellationToken);

        var newRoot = await newTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(newRoot);
    }

    // ── Token utilities ─────────────────────────────────────────────────

    private static List<SyntaxToken> Tokenize(string source)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var t in SyntaxFactory.ParseTokens(source))
        {
            tokens.Add(t);
        }
        return tokens;
    }

    private static int FindPipeNear(string source, int position)
    {
        // Search within a window around the diagnostic position for a `|` outside
        // string/char literals and comments.
        const int window = 60;
        int start = Math.Max(0, position - window);
        int end = Math.Min(source.Length, position + window);

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

            if (c == '|' && i >= start && i <= end)
            {
                return i;
            }
        }

        return -1;
    }

    private static (int pipeIdx, string? leftText, string? rightText) FindUnionAroundPipe(
        List<SyntaxToken> tokens, int pipeCharPos)
    {
        // Find the `|` token in the token list.
        int pipeIdx = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind() == SyntaxKind.BarToken &&
                tokens[i].SpanStart == pipeCharPos)
            {
                pipeIdx = i;
                break;
            }
        }

        if (pipeIdx < 0) return (-1, null, null);

        // Parse the left type: walk backwards to find the start, then use the type parser.
        int leftStart = FindLeftTypeStart(tokens, pipeIdx);
        if (leftStart < 0) return (-1, null, null);

        // Parse the right type: walk forwards.
        int rightEnd = FindRightTypeEnd(tokens, pipeIdx);
        if (rightEnd < 0) return (-1, null, null);

        // Extract text from tokens for left and right types.
        var leftText = ExtractTokenText(tokens, leftStart, pipeIdx - 1);
        var rightText = ExtractTokenText(tokens, pipeIdx + 1, rightEnd);

        if (string.IsNullOrEmpty(leftText) || string.IsNullOrEmpty(rightText))
        {
            return (-1, null, null);
        }

        return (pipeIdx, leftText, rightText);
    }

    private static int FindLeftTypeStart(List<SyntaxToken> tokens, int pipeIdx)
    {
        // Walk backwards from the pipe to find the start of the left type.
        // A type consists of: identifiers, dots, <>, ?, commas in generics, parens for tuples.
        int depth = 0;
        for (int i = pipeIdx - 1; i >= 0; i--)
        {
            var k = tokens[i].Kind();

            // Closing > or ) increases depth.
            if (k == SyntaxKind.GreaterThanToken ||
                k == SyntaxKind.GreaterThanGreaterThanToken ||
                k == SyntaxKind.GreaterThanGreaterThanGreaterThanToken ||
                k == SyntaxKind.CloseParenToken)
            {
                depth++;
                continue;
            }

            // Opening < or ( decreases depth.
            if (k == SyntaxKind.LessThanToken)
            {
                if (depth == 0)
                {
                    return i;
                }
                depth--;
                continue;
            }

            if (k == SyntaxKind.OpenParenToken)
            {
                if (depth == 0)
                {
                    // Could be a tuple type start OR a method parameter list.
                    // Check the token before '(': if it's an identifier or '>',
                    // it's a method parameter list — the type starts AFTER '('.
                    var prevKind = i > 0 ? tokens[i - 1].Kind() : SyntaxKind.None;
                    if (prevKind == SyntaxKind.IdentifierToken ||
                        prevKind == SyntaxKind.GreaterThanToken ||
                        prevKind == SyntaxKind.GreaterThanGreaterThanToken ||
                        prevKind == SyntaxKind.GreaterThanGreaterThanGreaterThanToken)
                    {
                        return i + 1;
                    }
                    // Otherwise it's a tuple type start.
                    return i;
                }
                depth--;
                continue;
            }

            if (depth > 0)
            {
                continue;
            }

            // At depth 0, valid type tokens are identifiers, dots, ?, predefined types.
            if (k == SyntaxKind.IdentifierToken ||
                k == SyntaxKind.DotToken ||
                k == SyntaxKind.QuestionToken ||
                IsPredefinedType(k) ||
                k == SyntaxKind.ColonColonToken ||
                k == SyntaxKind.CommaToken)
            {
                continue;
            }

            // Hit a non-type token: the type starts at i+1.
            return i + 1;
        }

        return 0;
    }

    private static int FindRightTypeEnd(List<SyntaxToken> tokens, int pipeIdx)
    {
        // Walk forwards from after the pipe to find the end of the right type.
        int depth = 0;
        bool lastWasIdentifier = false;
        for (int i = pipeIdx + 1; i < tokens.Count; i++)
        {
            var k = tokens[i].Kind();

            if (k == SyntaxKind.EndOfFileToken) break;

            // Opening < or ( increases depth.
            if (k == SyntaxKind.LessThanToken ||
                k == SyntaxKind.OpenParenToken)
            {
                depth++;
                lastWasIdentifier = false;
                continue;
            }

            // Closing > or ) decreases depth.
            if (k == SyntaxKind.GreaterThanToken ||
                k == SyntaxKind.GreaterThanGreaterThanToken ||
                k == SyntaxKind.GreaterThanGreaterThanGreaterThanToken ||
                k == SyntaxKind.CloseParenToken)
            {
                if (depth == 0)
                {
                    return i - 1;
                }
                depth--;
                lastWasIdentifier = false;
                continue;
            }

            if (depth > 0)
            {
                lastWasIdentifier = (k == SyntaxKind.IdentifierToken || IsPredefinedType(k));
                continue;
            }

            // At depth 0: if we see an identifier and the previous token was also
            // an identifier (without a dot between), the second one is a parameter
            // name, not part of the type — stop.
            if ((k == SyntaxKind.IdentifierToken || IsPredefinedType(k)) && lastWasIdentifier)
            {
                return i - 1;
            }

            // At depth 0, valid type tokens.
            if (k == SyntaxKind.IdentifierToken ||
                k == SyntaxKind.DotToken ||
                k == SyntaxKind.QuestionToken ||
                IsPredefinedType(k) ||
                k == SyntaxKind.ColonColonToken ||
                k == SyntaxKind.CommaToken)
            {
                lastWasIdentifier = (k == SyntaxKind.IdentifierToken || IsPredefinedType(k));
                if (k == SyntaxKind.DotToken || k == SyntaxKind.ColonColonToken)
                    lastWasIdentifier = false;
                continue;
            }

            // Hit a non-type token: the type ends at i-1.
            return i - 1;
        }

        return tokens.Count - 2; // Before EOF
    }

    private static string ExtractTokenText(List<SyntaxToken> tokens, int from, int to)
    {
        if (from > to || from < 0 || to >= tokens.Count) return "";

        var sb = new System.Text.StringBuilder();
        for (int i = from; i <= to; i++)
        {
            var t = tokens[i];
            if (t.Kind() == SyntaxKind.EndOfFileToken) break;

            var text = t.ToString();
            if (string.IsNullOrEmpty(text)) continue;

            // Add a space between tokens if needed (heuristic for readability).
            if (sb.Length > 0)
            {
                var prev = tokens[i - 1].ToString();
                // No space after . , < > :: or before those.
                var last = sb[sb.Length - 1];
                if (last != '.' && last != ',' && last != '<' && last != '>' &&
                    last != ':' && text != "." && text != "," && text != "<" &&
                    text != ">" && text != "?" && text != "::")
                {
                    sb.Append(' ');
                }
            }
            sb.Append(text);
        }
        return sb.ToString().Trim();
    }

    private static bool IsPredefinedType(SyntaxKind k) =>
        k == SyntaxKind.BoolKeyword || k == SyntaxKind.ByteKeyword ||
        k == SyntaxKind.SByteKeyword || k == SyntaxKind.CharKeyword ||
        k == SyntaxKind.DecimalKeyword || k == SyntaxKind.DoubleKeyword ||
        k == SyntaxKind.FloatKeyword || k == SyntaxKind.IntKeyword ||
        k == SyntaxKind.UIntKeyword || k == SyntaxKind.LongKeyword ||
        k == SyntaxKind.ULongKeyword || k == SyntaxKind.ShortKeyword ||
        k == SyntaxKind.UShortKeyword || k == SyntaxKind.ObjectKeyword ||
        k == SyntaxKind.StringKeyword || k == SyntaxKind.VoidKeyword;
}