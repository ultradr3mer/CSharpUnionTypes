// This is union-syntax SOURCE for the transpiler (NOT valid C#).
// Run the transpiler to produce compilable output:
//
//   dotnet run --project ../../UnionTypes.Transpiler -- --input . --output ..
//
// Do not open this file expecting it to compile in the IDE.

using UnionTypes;
using BasicSample;

string | int value = 42;

var example = new Example { Value = "hello" };
Console.WriteLine(example.Describe());

var result = value.Match(s => $"s:{s}", i => $"i:{i}");
Console.WriteLine(result);
