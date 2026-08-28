namespace UnionTypes.Runtime.Tests;

public class Union3Tests
{
    [Fact]
    public void Construct_FromT1_IsT1()
    {
        Union<string, int, bool> u = "hello";
        Assert.True(u.IsT1);
        Assert.False(u.IsT2);
        Assert.False(u.IsT3);
    }

    [Fact]
    public void Construct_FromT2_IsT2()
    {
        Union<string, int, bool> u = 42;
        Assert.True(u.IsT2);
        Assert.False(u.IsT1);
        Assert.False(u.IsT3);
    }

    [Fact]
    public void Construct_FromT3_IsT3()
    {
        Union<string, int, bool> u = true;
        Assert.True(u.IsT3);
        Assert.False(u.IsT1);
        Assert.False(u.IsT2);
    }

    [Fact]
    public void TryGet_ReturnsCorrectVariant()
    {
        Union<string, int, bool> a = "x";
        Union<string, int, bool> b = 7;
        Union<string, int, bool> c = false;

        Assert.True(a.TryGetT1(out var s));
        Assert.Equal("x", s);
        Assert.True(b.TryGetT2(out var i));
        Assert.Equal(7, i);
        Assert.True(c.TryGetT3(out var b2));
        Assert.False(b2);
    }

    [Fact]
    public void Match_DispatchesCorrectly()
    {
        Union<string, int, bool> u = 42;
        var result = u.Match(s => $"s:{s}", i => $"i:{i}", b => $"b:{b}");
        Assert.Equal("i:42", result);
    }

    [Fact]
    public void Switch_DispatchesCorrectly()
    {
        Union<string, int, bool> u = true;
        string captured = "";
        u.Switch(s => captured = "s", i => captured = "i", b => captured = $"b:{b}");
        Assert.Equal("b:True", captured);
    }

    [Fact]
    public void ImplicitOperators_WorkForAllVariants()
    {
        Union<string, int, bool> a = "hello";
        Union<string, int, bool> b = 42;
        Union<string, int, bool> c = true;

        Assert.True(a.IsT1);
        Assert.True(b.IsT2);
        Assert.True(c.IsT3);
    }

    [Fact]
    public void Default_IsNoVariant()
    {
        Union<string, int, bool> u = default;
        Assert.False(u.IsT1);
        Assert.False(u.IsT2);
        Assert.False(u.IsT3);
    }

    [Fact]
    public void Equals_SameVariantSameValue()
    {
        Union<string, int, bool> a = 42;
        Union<string, int, bool> b = 42;
        Union<string, int, bool> c = "42";

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void IdenticalTypes_Throws()
    {
        var type = typeof(Union<,,>).MakeGenericType(typeof(string), typeof(string), typeof(int));
        Assert.Throws<TypeInitializationException>(() =>
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle));
    }
}

public class Union4Tests
{
    [Fact]
    public void Construct_FromEachVariant()
    {
        Union<string, int, bool, double> a = "x";
        Union<string, int, bool, double> b = 1;
        Union<string, int, bool, double> c = true;
        Union<string, int, bool, double> d = 3.14;

        Assert.True(a.IsT1);
        Assert.True(b.IsT2);
        Assert.True(c.IsT3);
        Assert.True(d.IsT4);
    }

    [Fact]
    public void Match_DispatchesCorrectly()
    {
        Union<string, int, bool, double> u = 3.14;
        var result = u.Match(
            s => "s", i => "i", b => "b", d => $"d:{d}");
        Assert.True(result.StartsWith("d:3"), $"Expected 'd:3...' got: {result}");
    }

    [Fact]
    public void Switch_DispatchesCorrectly()
    {
        Union<string, int, bool, double> u = true;
        string captured = "";
        u.Switch(
            s => captured = "s", i => captured = "i",
            b => captured = "b", d => captured = "d");
        Assert.Equal("b", captured);
    }

    [Fact]
    public void TryGetT4_Works()
    {
        Union<string, int, bool, double> u = 2.5;
        Assert.True(u.TryGetT4(out var d));
        Assert.Equal(2.5, d);
        Assert.False(u.TryGetT1(out _));
    }

    [Fact]
    public void Default_IsNoVariant()
    {
        Union<string, int, bool, double> u = default;
        Assert.False(u.IsT1);
        Assert.False(u.IsT2);
        Assert.False(u.IsT3);
        Assert.False(u.IsT4);
    }

    [Fact]
    public void ToString_IndicatesVariant()
    {
        Union<string, int, bool, double> u = "hi";
        Assert.Contains("T1", u.ToString());
        Assert.Contains("hi", u.ToString());
    }
}