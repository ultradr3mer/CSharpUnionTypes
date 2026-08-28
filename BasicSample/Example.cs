using UnionTypes;

namespace BasicSample;

public class Example
{
    private Union<string, int> _value;

    public Union<string, int> Value
    {
        get => _value;
        set => _value = value;
    }

    public Union<string, int> GetValue(bool asText)
    {
        return asText ? "hello" : 42;
    }

    public string Describe()
    {
        return Value.Match(s => $"string: {s}", i => $"int: {i}");
    }
}
