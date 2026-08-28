using UnionTypes;
using BasicSample;

string | int value = 42;

var example = new Example { Value = "hello" };
Console.WriteLine(example.Describe());

var result = value.Match(s => $"s:{s}", i => $"i:{i}");
Console.WriteLine(result);

var nonZeroEnumerable = ExampleFunction.NonZeroEnumerable(new int[] { -1, 0, 1, 2, 3 });
Console.WriteLine($"ExampleFunction.NonZeroEnumerable(new int[] {{ -1, 0, 1, 2, 3 }}): {string.Join(", ", nonZeroEnumerable)}");

var nonZeroInt1 = ExampleFunction.NonZeroEnumerable(1);
Console.WriteLine($"ExampleFunction.NonZeroEnumerable(1): {string.Join(", ", nonZeroInt1)}");
