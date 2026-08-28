using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace UnionTypes.Transpiler;

/// <summary>
/// Result of parsing a type (possibly a union) from a token list.
/// </summary>
public sealed record TypeParse
{
    /// <summary>Index in the token list just past the last token of this type.</summary>
    public required int End { get; init; }

    /// <summary>Transformed text: verbatim original when no union, reconstructed when union present.</summary>
    public required string Text { get; init; }

    /// <summary>True if this type (recursively) contains a union.</summary>
    public bool HasUnion { get; init; }

    /// <summary>Diagnostic produced while parsing this type, if any.</summary>
    public Diagnostic? Error { get; init; }

    public bool IsError => Error is not null;
}

/// <summary>
/// Result of transforming a single source file.
/// </summary>
public sealed record TransformResult(string Output, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>A single text replacement.</summary>
public sealed record Replacement(int Start, int End, string Text);

/// <summary>
/// Token-based transformer that rewrites <c>Type | Type</c> union syntax at known type
/// positions into <c>global::NS.Union&lt;Left, Right&gt;</c>.
/// </summary>
public static class UnionTransformer
{
    private static readonly HashSet<SyntaxKind> ModifierKeywords = new()
    {
        SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword,
        SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword,
        SyntaxKind.StaticKeyword, SyntaxKind.ReadOnlyKeyword,
        SyntaxKind.PartialKeyword, SyntaxKind.AbstractKeyword,
        SyntaxKind.VirtualKeyword, SyntaxKind.OverrideKeyword,
        SyntaxKind.SealedKeyword, SyntaxKind.NewKeyword,
        SyntaxKind.UnsafeKeyword, SyntaxKind.VolatileKeyword,
        SyntaxKind.ExternKeyword, SyntaxKind.AsyncKeyword,
        SyntaxKind.ConstKeyword,
        SyntaxKind.RefKeyword, SyntaxKind.OutKeyword,
        SyntaxKind.InKeyword, SyntaxKind.ParamsKeyword,
    };

    private static readonly HashSet<SyntaxKind> DeclarationKeywords = new()
    {
        SyntaxKind.NamespaceKeyword,
        SyntaxKind.ClassKeyword, SyntaxKind.StructKeyword,
        SyntaxKind.InterfaceKeyword, SyntaxKind.EnumKeyword,
        SyntaxKind.RecordKeyword,
        SyntaxKind.DelegateKeyword,
    };

    private static readonly HashSet<SyntaxKind> PredefinedTypeKeywords = new()
    {
        SyntaxKind.BoolKeyword, SyntaxKind.ByteKeyword, SyntaxKind.SByteKeyword,
        SyntaxKind.CharKeyword, SyntaxKind.DecimalKeyword, SyntaxKind.DoubleKeyword,
        SyntaxKind.FloatKeyword, SyntaxKind.IntKeyword, SyntaxKind.UIntKeyword,
        SyntaxKind.LongKeyword, SyntaxKind.ULongKeyword, SyntaxKind.ShortKeyword,
        SyntaxKind.UShortKeyword, SyntaxKind.ObjectKeyword, SyntaxKind.StringKeyword,
        SyntaxKind.VoidKeyword,
    };

    /// <summary>
    /// Token kinds that establish a type-start position at the following token.
    /// </summary>
    private static bool IsTrigger(SyntaxKind k) =>
        k == SyntaxKind.OpenBraceToken || k == SyntaxKind.SemicolonToken ||
        k == SyntaxKind.LessThanToken || k == SyntaxKind.CommaToken ||
        ModifierKeywords.Contains(k);

    public static TransformResult Transform(string source, string filePath, string runtimeNamespace)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var t in SyntaxFactory.ParseTokens(source))
        {
            tokens.Add(t);
        }

        var diagnostics = new List<Diagnostic>();
        var replacements = new List<Replacement>();
        var unionTypeName = $"global::{runtimeNamespace}.Union";

        var sb = new System.Text.StringBuilder(source.Length + 16);

        int i = 0;
        int lastTrigger = 0; // start of file is a type position
        int parenDepth = 0;
        int angleDepth = 0;

        while (i < tokens.Count)
        {
            var t = tokens[i];
            var kind = t.Kind();

            // End-of-file sentinel: nothing to do.
            if (kind == SyntaxKind.EndOfFileToken)
            {
                i++;
                continue;
            }

            // Declaration keywords (class/struct/namespace/...) are not types; clear
            // the pending trigger so the following identifier is not parsed as a type.
            if (DeclarationKeywords.Contains(kind))
            {
                lastTrigger = -1;
                i++;
                continue;
            }

            if (IsTrigger(kind))
            {
                lastTrigger = i + 1;
                if (kind == SyntaxKind.LessThanToken) angleDepth++;
                i++;
                continue;
            }

            if (kind == SyntaxKind.OpenParenToken)
            {
                // A '(' following an identifier or '>' (generic method) opens a parameter
                // list: types begin after it. Otherwise it is either a tuple type at a
                // type-start position (handled by the parser below) or a parenthesized
                // expression, which we leave alone.
                var prev = PreviousSignificantKind(tokens, i);
                if (prev == SyntaxKind.IdentifierToken ||
                    prev == SyntaxKind.GreaterThanToken ||
                    prev == SyntaxKind.GreaterThanGreaterThanToken ||
                    prev == SyntaxKind.GreaterThanGreaterThanGreaterThanToken)
                {
                    lastTrigger = i + 1;
                    parenDepth++;
                    i++;
                    continue;
                }
                // Not a parameter list: fall through. If i == lastTrigger, the parser
                // will treat '(' as a tuple type; otherwise it is a parenthesized
                // expression and we simply skip it.
                if (i != lastTrigger)
                {
                    i++;
                    continue;
                }
            }

            if (kind == SyntaxKind.CloseParenToken)
            {
                if (parenDepth > 0) parenDepth--;
                i++;
                continue;
            }

            if (kind == SyntaxKind.GreaterThanToken) { angleDepth = Math.Max(0, angleDepth - 1); i++; continue; }
            if (kind == SyntaxKind.GreaterThanGreaterThanToken) { angleDepth = Math.Max(0, angleDepth - 2); i++; continue; }
            if (kind == SyntaxKind.GreaterThanGreaterThanGreaterThanToken) { angleDepth = Math.Max(0, angleDepth - 3); i++; continue; }

            if (i == lastTrigger && lastTrigger >= 0 && lastTrigger < tokens.Count)
            {
                // Only attempt to parse a type when the current token is a plausible
                // type start. Other keywords (using, if, for, return, ...) are statement
                // starts, not types.
                if (!IsPotentialTypeStart(kind))
                {
                    lastTrigger = -1;
                    i++;
                    continue;
                }

                var parse = ParseTypeOrUnion(tokens, i, filePath, unionTypeName);
                if (parse.IsError && parse.Error is not null)
                {
                    diagnostics.Add(parse.Error);
                    i = parse.End > i ? parse.End : i + 1;
                    lastTrigger = -1;
                    continue;
                }

                if (parse.HasUnion && FrameIsSupported(tokens, parse.End, parenDepth, angleDepth))
                {
                    replacements.Add(new Replacement(
                        tokens[i].SpanStart,
                        tokens[parse.End - 1].Span.End,
                        parse.Text));
                }

                i = parse.End > i ? parse.End : i + 1;
                lastTrigger = -1;
                continue;
            }

            i++;
        }

        if (replacements.Count == 0 && diagnostics.Count == 0)
        {
            return new TransformResult(source, diagnostics);
        }

        var ordered = replacements.OrderBy(r => r.Start).ToList();
        int cursor = source.Length;
        foreach (var r in Enumerable.Reverse(ordered))
        {
            sb.Insert(0, source.AsSpan(r.End, cursor - r.End));
            sb.Insert(0, r.Text);
            cursor = r.Start;
        }
        sb.Insert(0, source.AsSpan(0, cursor));

        return new TransformResult(sb.ToString(), diagnostics);
    }

    private static SyntaxKind PreviousSignificantKind(IReadOnlyList<SyntaxToken> tokens, int index)
    {
        for (int j = index - 1; j >= 0; j--)
        {
            var k = tokens[j].Kind();
            if (k == SyntaxKind.EndOfFileToken || k == SyntaxKind.None) continue;
            return k;
        }
        return SyntaxKind.None;
    }

    private static bool FrameIsSupported(IReadOnlyList<SyntaxToken> tokens, int end, int parenDepth, int angleDepth)
    {
        if (end >= tokens.Count)
        {
            return false;
        }

        var next = tokens[end];
        var nextKind = next.Kind();

        // Closing a generic argument list.
        if (nextKind == SyntaxKind.GreaterThanToken ||
            nextKind == SyntaxKind.GreaterThanGreaterThanToken ||
            nextKind == SyntaxKind.GreaterThanGreaterThanGreaterThanToken)
        {
            return angleDepth > 0;
        }

        // Separator between generic arguments or parameters.
        if (nextKind == SyntaxKind.CommaToken)
        {
            return angleDepth > 0 || parenDepth > 0;
        }

        // Followed by an identifier (the declared name).
        if (nextKind == SyntaxKind.IdentifierToken)
        {
            if (end + 1 < tokens.Count)
            {
                var after = tokens[end + 1].Kind();
                return after == SyntaxKind.SemicolonToken ||
                       after == SyntaxKind.EqualsToken ||
                       after == SyntaxKind.OpenBraceToken ||
                       after == SyntaxKind.OpenParenToken ||
                       after == SyntaxKind.LessThanToken ||
                       after == SyntaxKind.CloseParenToken ||
                       after == SyntaxKind.CommaToken;
            }
        }

        return false;
    }

    private static TypeParse ParseTypeOrUnion(IReadOnlyList<SyntaxToken> tokens, int start, string filePath, string unionTypeName)
    {
        var left = ParseType(tokens, start, filePath, unionTypeName);
        if (left.IsError)
        {
            return left;
        }

        int j = left.End;
        if (j < tokens.Count && tokens[j].Kind() == SyntaxKind.BarToken)
        {
            var right = ParseType(tokens, j + 1, filePath, unionTypeName);
            if (right.IsError)
            {
                return right;
            }

            int k = right.End;
            if (k < tokens.Count && tokens[k].Kind() == SyntaxKind.BarToken)
            {
                var (line, col) = ComputeLineCol(tokens, tokens[k].SpanStart);
                return new TypeParse
                {
                    End = k + 1,
                    Text = "",
                    HasUnion = true,
                    Error = new Diagnostic(
                        DiagnosticCode.UT001, filePath, line, col,
                        $"{DiagnosticCode.Describe(DiagnosticCode.UT001)}{Environment.NewLine}{ReconstructTokenText(tokens, start, k + 1)}"),
                };
            }

            string leftText = left.Text;
            string rightText = right.Text;

            if (Normalize(leftText) == Normalize(rightText))
            {
                var (line, col) = ComputeLineCol(tokens, tokens[j].SpanStart);
                return new TypeParse
                {
                    End = right.End,
                    Text = "",
                    HasUnion = true,
                    Error = new Diagnostic(
                        DiagnosticCode.UT004, filePath, line, col,
                        $"{DiagnosticCode.Describe(DiagnosticCode.UT004)}: {leftText} | {rightText}"),
                };
            }

            string unionText = $"{unionTypeName}<{leftText}, {rightText}>";
            return new TypeParse
            {
                End = right.End,
                Text = unionText,
                HasUnion = true,
            };
        }

        return left;
    }

    private static TypeParse ParseType(IReadOnlyList<SyntaxToken> tokens, int start, string filePath, string unionTypeName)
    {
        if (start >= tokens.Count)
        {
            return new TypeParse
            {
                End = start,
                Text = "",
                HasUnion = false,
                Error = new Diagnostic(DiagnosticCode.UT003, filePath, 1, 1, DiagnosticCode.Describe(DiagnosticCode.UT003)),
            };
        }

        int i = start;

        if (tokens[i].Kind() == SyntaxKind.OpenParenToken)
        {
            return ParseTupleType(tokens, i, filePath, unionTypeName);
        }

        if (tokens[i].Kind() == SyntaxKind.DelegateKeyword &&
            i + 1 < tokens.Count && tokens[i + 1].Kind() == SyntaxKind.AsteriskToken)
        {
            return UnsupportedAt(tokens, i, filePath, DiagnosticCode.UT005);
        }

        var nameParse = ParseTypeName(tokens, i, filePath, unionTypeName);
        if (nameParse.IsError)
        {
            return nameParse;
        }

        i = nameParse.End;
        bool hasUnion = nameParse.HasUnion;
        string text = nameParse.Text;

        if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.QuestionToken)
        {
            text += "?";
            i++;
        }

        if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.OpenBracketToken)
        {
            var (line, col) = ComputeLineCol(tokens, tokens[i].SpanStart);
            return new TypeParse
            {
                End = SkipBrackets(tokens, i),
                Text = text,
                HasUnion = hasUnion,
                Error = new Diagnostic(DiagnosticCode.UT005, filePath, line, col, DiagnosticCode.Describe(DiagnosticCode.UT005)),
            };
        }

        if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.AsteriskToken)
        {
            var (line, col) = ComputeLineCol(tokens, tokens[i].SpanStart);
            return new TypeParse
            {
                End = i + 1,
                Text = text,
                HasUnion = hasUnion,
                Error = new Diagnostic(DiagnosticCode.UT005, filePath, line, col, DiagnosticCode.Describe(DiagnosticCode.UT005)),
            };
        }

        return new TypeParse { End = i, Text = text, HasUnion = hasUnion };
    }

    private static TypeParse ParseTupleType(IReadOnlyList<SyntaxToken> tokens, int start, string filePath, string unionTypeName)
    {
        int i = start + 1;
        var parts = new List<string>();
        bool anyUnion = false;
        Diagnostic? error = null;

        while (i < tokens.Count)
        {
            if (tokens[i].Kind() == SyntaxKind.CloseParenToken)
            {
                i++;
                break;
            }

            var elem = ParseType(tokens, i, filePath, unionTypeName);
            if (elem.IsError)
            {
                error = elem.Error;
                i = elem.End > i ? elem.End : i + 1;
                continue;
            }

            anyUnion |= elem.HasUnion;
            string elemText = elem.Text;
            i = elem.End;

            if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.IdentifierToken)
            {
                elemText += " " + tokens[i].ToString();
                i++;
            }

            parts.Add(elemText);

            if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.CommaToken)
            {
                i++;
                continue;
            }
        }

        string text = "(" + string.Join(", ", parts) + ")";
        return new TypeParse { End = i, Text = text, HasUnion = anyUnion, Error = error };
    }

    private static TypeParse ParseTypeName(IReadOnlyList<SyntaxToken> tokens, int start, string filePath, string unionTypeName)
    {
        int i = start;

        string prefix = "";
        if (i + 1 < tokens.Count &&
            tokens[i].Kind() == SyntaxKind.IdentifierToken &&
            tokens[i].ToString() == "global" &&
            tokens[i + 1].Kind() == SyntaxKind.ColonColonToken)
        {
            prefix = "global::";
            i += 2;
        }

        if (i >= tokens.Count || !IsTypeAtom(tokens[i]))
        {
            int pos = i < tokens.Count ? tokens[i].SpanStart : (i > 0 ? tokens[i - 1].Span.End : 0);
            var (line, col) = ComputeLineCol(tokens, pos);
            return new TypeParse
            {
                End = i > start ? i : start + 1,
                Text = "",
                HasUnion = false,
                Error = new Diagnostic(DiagnosticCode.UT003, filePath, line, col, DiagnosticCode.Describe(DiagnosticCode.UT003)),
            };
        }

        var nameBuilder = new System.Text.StringBuilder(prefix);
        nameBuilder.Append(tokens[i].ToString());
        i++;

        while (i + 1 < tokens.Count &&
               tokens[i].Kind() == SyntaxKind.DotToken &&
               IsTypeAtom(tokens[i + 1]))
        {
            nameBuilder.Append('.').Append(tokens[i + 1].ToString());
            i += 2;
        }

        string nameText = nameBuilder.ToString();
        bool hasUnion = false;

        if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.LessThanToken)
        {
            var generic = ParseGenericArgs(tokens, i, filePath, unionTypeName);
            if (generic.IsError)
            {
                return generic;
            }

            nameText += generic.Text;
            hasUnion |= generic.HasUnion;
            i = generic.End;
        }

        return new TypeParse { End = i, Text = nameText, HasUnion = hasUnion };
    }

    private static TypeParse ParseGenericArgs(IReadOnlyList<SyntaxToken> tokens, int start, string filePath, string unionTypeName)
    {
        int i = start + 1;
        var args = new List<string>();
        bool anyUnion = false;
        Diagnostic? error = null;

        while (i < tokens.Count)
        {
            if (tokens[i].Kind() == SyntaxKind.GreaterThanToken ||
                tokens[i].Kind() == SyntaxKind.GreaterThanGreaterThanToken ||
                tokens[i].Kind() == SyntaxKind.GreaterThanGreaterThanGreaterThanToken)
            {
                i++;
                break;
            }

            var arg = ParseTypeOrUnion(tokens, i, filePath, unionTypeName);
            if (arg.IsError)
            {
                error = arg.Error;
                i = arg.End > i ? arg.End : i + 1;
                continue;
            }

            anyUnion |= arg.HasUnion;
            args.Add(arg.Text);
            i = arg.End;

            if (i < tokens.Count && tokens[i].Kind() == SyntaxKind.CommaToken)
            {
                i++;
                continue;
            }
        }

        string text = "<" + string.Join(", ", args) + ">";
        return new TypeParse { End = i, Text = text, HasUnion = anyUnion, Error = error };
    }

    private static bool IsTypeAtom(SyntaxToken token)
    {
        var k = token.Kind();
        return k == SyntaxKind.IdentifierToken || PredefinedTypeKeywords.Contains(k);
    }

    private static bool IsPotentialTypeStart(SyntaxKind k) =>
        k == SyntaxKind.IdentifierToken ||
        k == SyntaxKind.OpenParenToken ||
        PredefinedTypeKeywords.Contains(k);

    private static int SkipBrackets(IReadOnlyList<SyntaxToken> tokens, int start)
    {
        int i = start;
        int depth = 0;
        while (i < tokens.Count)
        {
            var k = tokens[i].Kind();
            if (k == SyntaxKind.OpenBracketToken) depth++;
            else if (k == SyntaxKind.CloseBracketToken)
            {
                depth--;
                i++;
                if (depth == 0) break;
                continue;
            }
            i++;
        }
        return i;
    }

    private static TypeParse UnsupportedAt(IReadOnlyList<SyntaxToken> tokens, int i, string filePath, string code)
    {
        var (line, col) = ComputeLineCol(tokens, tokens[i].SpanStart);
        return new TypeParse
        {
            End = i + 1,
            Text = "",
            HasUnion = false,
            Error = new Diagnostic(code, filePath, line, col, DiagnosticCode.Describe(code)),
        };
    }

    private static string Normalize(string typeText)
    {
        return new string(typeText.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    private static string ReconstructTokenText(IReadOnlyList<SyntaxToken> tokens, int startIdx, int endIdx)
    {
        var sb = new System.Text.StringBuilder();
        for (int x = startIdx; x <= endIdx && x < tokens.Count; x++)
        {
            sb.Append(tokens[x].ToString());
            sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static (int line, int col) ComputeLineCol(IReadOnlyList<SyntaxToken> tokens, int position)
    {
        int line = 1;
        int col = 1;
        foreach (var tk in tokens)
        {
            foreach (var tr in tk.LeadingTrivia)
            {
                var s = tr.ToString();
                for (int x = 0; x < s.Length; x++)
                {
                    if (s[x] == '\n') { line++; col = 1; }
                    else col++;
                }
            }

            if (tk.SpanStart <= position && position < tk.Span.End)
            {
                col += position - tk.SpanStart;
                return (line, col);
            }

            var ts = tk.ToString();
            for (int x = 0; x < ts.Length; x++)
            {
                if (ts[x] == '\n') { line++; col = 1; }
                else col++;
            }

            foreach (var tr in tk.TrailingTrivia)
            {
                var s = tr.ToString();
                for (int x = 0; x < s.Length; x++)
                {
                    if (s[x] == '\n') { line++; col = 1; }
                    else col++;
                }
            }
        }
        return (line, col);
    }
}
