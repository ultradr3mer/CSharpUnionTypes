using BasicSample;
using UnionTypes;

// This sample uses the transpiled form (Union<string, int>) so it opens and builds
// cleanly in any C# IDE. The union-syntax SOURCE (string | int) that the transpiler
// consumes lives in the Source/ subfolder; run the transpiler on it to regenerate
// this output:
//
//   dotnet run --project ../UnionTypes.Transpiler -- \
//     --input ./Source --output .

Union<string, int> value = 42;

var example = new Example { Value = "hello" };
Console.WriteLine(example.Describe());

var result = value.Match(s => $"s:{s}", i => $"i:{i}");
Console.WriteLine(result);
