namespace UnionTypes.Transpiler;

/// <summary>
/// Diagnostic codes emitted by the union types transpiler.
/// </summary>
public static class DiagnosticCode
{
    public const string UT001 = "UT001";
    public const string UT002 = "UT002";
    public const string UT003 = "UT003";
    public const string UT004 = "UT004";
    public const string UT005 = "UT005";

    public static string Describe(string code) => code switch
    {
        UT001 => "Union Types mit mehr als vier Varianten werden noch nicht unterstützt.",
        UT002 => "Union Type an dieser Position nicht unterstützt.",
        UT003 => "Ungültige oder nicht parsebare Union-Type-Syntax.",
        UT004 => "Identische Union-Mitglieder nicht unterstützt.",
        UT005 => "Nicht unterstütztes Type-Konstrukt innerhalb einer Union.",
        _ => code,
    };
}

/// <summary>
/// A single transpiler diagnostic with source location.
/// </summary>
public sealed record Diagnostic(string Code, string FilePath, int Line, int Column, string Message)
{
    /// <summary>Formats the diagnostic like a compiler error.</summary>
    public override string ToString()
    {
        return $"{FilePath}({Line},{Column}): error {Code}: {Message}";
    }
}
