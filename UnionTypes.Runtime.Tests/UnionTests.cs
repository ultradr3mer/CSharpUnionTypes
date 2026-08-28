namespace UnionTypes.Runtime.Tests;

public class UnionTests
{
    [Fact]
    public void Construct_FromT1_IsT1True_IsT2False()
    {
        Union<string, int> u = new("hello");

        Assert.True(u.IsT1);
        Assert.False(u.IsT2);
    }

    [Fact]
    public void Construct_FromT2_IsT2True_IsT1False()
    {
        Union<string, int> u = new(42);

        Assert.True(u.IsT2);
        Assert.False(u.IsT1);
    }

    [Fact]
    public void TryGetT1_ReturnsValue_WhenActive()
    {
        Union<string, int> u = "hello";

        Assert.True(u.TryGetT1(out var s));
        Assert.Equal("hello", s);
        Assert.False(u.TryGetT2(out var i));
        Assert.Equal(0, i);
    }

    [Fact]
    public void TryGetT2_ReturnsValue_WhenActive()
    {
        Union<string, int> u = 42;

        Assert.True(u.TryGetT2(out var i));
        Assert.Equal(42, i);
        Assert.False(u.TryGetT1(out var s));
        Assert.Null(s);
    }

    [Fact]
    public void ImplicitOperator_T1_CreatesT1Variant()
    {
        Union<string, int> u = "implicit";

        Assert.True(u.IsT1);
        Assert.Equal("implicit", u.Match(s => s, _ => "no"));
    }

    [Fact]
    public void ImplicitOperator_T2_CreatesT2Variant()
    {
        Union<string, int> u = 7;

        Assert.True(u.IsT2);
        Assert.Equal("7", u.Match(_ => "no", i => i.ToString()));
    }

    [Fact]
    public void Match_DispatchesToCorrectVariant()
    {
        Union<string, int> a = "abc";
        Union<string, int> b = 9;

        Assert.Equal("ABC", a.Match(s => s.ToUpperInvariant(), i => i.ToString()));
        Assert.Equal("9", b.Match(s => s, i => i.ToString()));
    }

    [Fact]
    public void Switch_DispatchesToCorrectVariant()
    {
        Union<string, int> u = 5;
        string captured = "";

        u.Switch(s => captured = "s:" + s, i => captured = "i:" + i);

        Assert.Equal("i:5", captured);
    }

    [Fact]
    public void Match_OnUninitialized_Throws()
    {
        Union<string, int> u = default;

        Assert.Throws<InvalidOperationException>(() => u.Match(_ => 1, _ => 2));
    }

    [Fact]
    public void NullableT1_NullValue_IsCorrectlyTracked_AsT1()
    {
        Union<string?, int> u = (string?)null;

        Assert.True(u.IsT1);
        Assert.False(u.IsT2);
        Assert.True(u.TryGetT1(out var s));
        Assert.Null(s);
        Assert.False(u.TryGetT2(out _));
    }

    [Fact]
    public void NullableT2_NullValue_IsCorrectlyTracked_AsT2()
    {
        Union<int, string?> u = (string?)null;

        Assert.True(u.IsT2);
        Assert.False(u.IsT1);
        Assert.True(u.TryGetT2(out var s));
        Assert.Null(s);
        Assert.False(u.TryGetT1(out _));
    }

    [Fact]
    public void DefaultUnion_IsNeitherVariant()
    {
        Union<string, int> u = default;

        Assert.False(u.IsT1);
        Assert.False(u.IsT2);
        Assert.False(u.TryGetT1(out _));
        Assert.False(u.TryGetT2(out _));
    }

    [Fact]
    public void ValueTypes_RoundtripCorrectly()
    {
        Union<int, double> u = 3.14;

        Assert.True(u.IsT2);
        Assert.True(u.TryGetT2(out var d));
        Assert.Equal(3.14, d);
    }

    [Fact]
    public void IdenticalVariantTypes_ThrowsAtStaticInit()
    {
        // Trigger the static initializer (which rejects identical variants)
        // directly, since constructor calls are ambiguous at compile/runtime.
        var type = typeof(Union<,>).MakeGenericType(typeof(string), typeof(string));

        Assert.Throws<TypeInitializationException>(() =>
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        });
    }

    [Fact]
    public void Equals_SameVariantSameValue_AreEqual()
    {
        Union<string, int> a = "x";
        Union<string, int> b = "x";
        Union<string, int> c = 1;

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void GetHashCode_SameVariantSameValue_Match()
    {
        Union<string, int> a = "x";
        Union<string, int> b = "x";

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_IndicatesVariantAndValue()
    {
        Union<string, int> a = "hi";
        Union<string, int> b = 7;

        Assert.Contains("T1", a.ToString());
        Assert.Contains("hi", a.ToString());
        Assert.Contains("T2", b.ToString());
        Assert.Contains("7", b.ToString());
    }

    [Fact]
    public void OverlappingTypes_AssignmentCompilesAndProducesVariant()
    {
        Union<object, string> u = "s";

        // The implicit operator prefers the more specific (string -> T2).
        Assert.True(u.IsT2);
    }
}
