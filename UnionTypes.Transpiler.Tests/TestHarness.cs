namespace UnionTypes.Transpiler.Tests;

/// <summary>
/// Thin wrapper to call the public <see cref="UnionTransformer"/> from tests.
/// </summary>
internal static class TestHarness
{
    public static string Transform(string source, string runtimeNamespace = "UnionTypes")
    {
        return UnionTransformer.Transform(source, "test.cs", runtimeNamespace).Output;
    }

    public static (string Output, IReadOnlyList<Diagnostic> Diagnostics) TransformWithDiagnostics(
        string source, string runtimeNamespace = "UnionTypes")
    {
        var r = UnionTransformer.Transform(source, "test.cs", runtimeNamespace);
        return (r.Output, r.Diagnostics);
    }
}
