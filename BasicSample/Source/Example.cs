// This is union-syntax SOURCE for the transpiler (NOT valid C#).
// Run the transpiler to produce compilable output:
//
//   dotnet run --project ../../UnionTypes.Transpiler -- --input . --output ..
//
// Do not open this file expecting it to compile in the IDE.

using UnionTypes;

namespace BasicSample;

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

    public string Describe()
    {
        return Value.Match(s => $"string: {s}", i => $"int: {i}");
    }
}
