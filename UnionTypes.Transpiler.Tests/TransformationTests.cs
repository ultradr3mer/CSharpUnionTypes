using UnionTypes.Transpiler;
using Xunit;

namespace UnionTypes.Transpiler.Tests;

public class TransformationTests
{
    private static string Transform(string source, string runtimeNamespace = "UnionTypes")
    {
        // UnionTransformer is internal; reach it via TranspilerApp + an in-memory filesystem.
        return TestHarness.Transform(source, runtimeNamespace);
    }

    [Fact]
    public void LocalVariable_UnionType_Transforms()
    {
        var src = "string | int value;";
        Assert.Equal("global::UnionTypes.Union<string, int> value;", Transform(src));
    }

    [Fact]
    public void LocalVariable_WithInitializer_Transforms()
    {
        var src = "string | int value = GetValue();";
        Assert.Equal("global::UnionTypes.Union<string, int> value = GetValue();", Transform(src));
    }

    [Fact]
    public void NullableVariant_Transforms()
    {
        var src = "string | int? value;";
        Assert.Equal("global::UnionTypes.Union<string, int?> value;", Transform(src));
    }

    [Fact]
    public void NestedGenericVariants_Transforms()
    {
        var src = "List<string> | Dictionary<string, int> value;";
        Assert.Equal("global::UnionTypes.Union<List<string>, Dictionary<string, int>> value;", Transform(src));
    }

    [Fact]
    public void TupleVariant_Transforms()
    {
        var src = "(string Name, int Age) | Guid value;";
        Assert.Equal("global::UnionTypes.Union<(string Name, int Age), Guid> value;", Transform(src));
    }

    [Fact]
    public void Parameter_UnionType_Transforms()
    {
        var src = "void M(string | int value) { }";
        Assert.Equal("void M(global::UnionTypes.Union<string, int> value) { }", Transform(src));
    }

    [Fact]
    public void ReturnType_UnionType_Transforms()
    {
        var src = "string | int GetValue() => \"test\";";
        Assert.Equal("global::UnionTypes.Union<string, int> GetValue() => \"test\";", Transform(src));
    }

    [Fact]
    public void GenericArgument_UnionType_Transforms()
    {
        var src = "List<string | int> values;";
        Assert.Equal("List<global::UnionTypes.Union<string, int>> values;", Transform(src));
    }

    [Fact]
    public void Field_UnionType_Transforms()
    {
        var src = "private string | int _value;";
        Assert.Equal("private global::UnionTypes.Union<string, int> _value;", Transform(src));
    }

    [Fact]
    public void Property_UnionType_Transforms()
    {
        var src = "public string | int Value { get; init; }";
        Assert.Equal("public global::UnionTypes.Union<string, int> Value { get; init; }", Transform(src));
    }

    [Fact]
    public void FullExample_ClassBody_Transforms()
    {
        var src = """
        namespace Demo;

        public class Example
        {
            private string | int _value;

            public string | int Value
            {
                get => _value;
                set => _value = value;
            }

            public string | int GetValue(bool asText)
            {
                return asText ? "hello" : 42;
            }
        }
        """;

        var expected = """
        namespace Demo;

        public class Example
        {
            private global::UnionTypes.Union<string, int> _value;

            public global::UnionTypes.Union<string, int> Value
            {
                get => _value;
                set => _value = value;
            }

            public global::UnionTypes.Union<string, int> GetValue(bool asText)
            {
                return asText ? "hello" : 42;
            }
        }
        """;

        Assert.Equal(expected, Transform(src));
    }

    // --- Nicht verändern cases ---

    [Fact]
    public void BitwiseOrOperator_NotTransformed()
    {
        var src = "var flags = first | second;";
        Assert.Equal(src, Transform(src));
    }

    [Fact]
    public void StringLiteral_WithPipe_NotTransformed()
    {
        var src = "var text = \"string | int\";";
        Assert.Equal(src, Transform(src));
    }

    [Fact]
    public void Comment_WithPipe_NotTransformed()
    {
        var src = "// string | int";
        Assert.Equal(src, Transform(src));
    }

    [Fact]
    public void ExpressionInCallArgs_NotTransformed()
    {
        var src = "M(a | b);";
        Assert.Equal(src, Transform(src));
    }

    // --- Error cases ---

    [Fact]
    public void ThreeVariants_ProducesUT001Diagnostic()
    {
        var src = "string | int | Guid value;";
        var (output, diagnostics) = TestHarness.TransformWithDiagnostics(src);
        Assert.Single(diagnostics);
        Assert.Equal("UT001", diagnostics[0].Code);
        Assert.Contains("string | int | Guid", diagnostics[0].Message);
    }

    [Fact]
    public void IdenticalMembers_ProducesUT004Diagnostic()
    {
        var src = "string | string value;";
        var (output, diagnostics) = TestHarness.TransformWithDiagnostics(src);
        Assert.Single(diagnostics);
        Assert.Equal("UT004", diagnostics[0].Code);
    }
}
