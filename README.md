# CSharpUnionTypes

An experimental preprocessor/transpiler that adds **union-type syntax** to C#. It runs
*before* `dotnet build`, rewrites union-type syntax into normal C#, and emits the result
into a configurable output folder. Original source files are never modified.

```csharp
string | int value = GetValue();
```

becomes

```csharp
global::UnionTypes.Union<string, int> value = GetValue();
```

The transpiler does **not** extend the C# compiler. It is a source-to-source transform
backed by a Roslyn token stream and a small type-syntax parser.

## Projects

| Project | Type | Purpose |
|---|---|---|
| `UnionTypes.Runtime` | Class library | `Union<T1, T2>` discriminated union + future runtime APIs |
| `UnionTypes.Transpiler` | Console app | CLI preprocessor/transpiler + MSBuild targets |
| `UnionTypes.CodeFixes` | Roslyn analyzer | IDE lightbulb: `Union<X,Y>` <-> `X | Y` |
| `UnionTypes.Runtime.Tests` | xUnit | Tests for the union runtime |
| `UnionTypes.Transpiler.Tests` | xUnit | Transformation + CLI + integration tests |
| `UnionTypes.CodeFixes.Tests` | xUnit | Analyzer + code-fix tests |
| `BasicSample` | Console app | Sample using union syntax with MSBuild integration |

Open `UnionTypes.slnx` or build from the command line:

```bash
dotnet build UnionTypes.slnx
dotnet test UnionTypes.slnx
```

## Installation

Reference the runtime, transpiler, and code-fix analyzer, then import the MSBuild targets:

```xml
<PropertyGroup>
  <UnionTypesEnabled>true</UnionTypesEnabled>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\UnionTypes.Runtime\UnionTypes.Runtime.csproj" />
  <ProjectReference Include="..\UnionTypes.Transpiler\UnionTypes.Transpiler.csproj" />
  <ProjectReference Include="..\UnionTypes.CodeFixes\UnionTypes.CodeFixes.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<Import Project="..\UnionTypes.Transpiler\build\UnionTypes.Transpiler.targets" />
```

With this in place, `dotnet build` automatically:

1. Runs the transpiler on `Source/**/*.cs` before `CoreCompile`.
2. Removes the union-syntax source files from compilation.
3. Adds the generated files from `obj/UnionTranspiler/` to compilation.
4. Skips the transpiler on incremental builds (no source changes = no re-run).

Put your union-syntax `.cs` files in a `Source/` subfolder. The generated output
goes to `obj/UnionTranspiler/` and is compiled automatically — no manual steps.

## IDE Integration (Code-Fixes)

The `UnionTypes.CodeFixes` Roslyn analyzer provides two lightbulb actions:

1. **Union → Pipe**: When you write `Union<T1, T2>` in a `Source/` file, the
   lightbulb offers to rewrite it as `T1 | T2` (the union-type syntax).
2. **Pipe → Union**: When the compiler reports a syntax error (`CS1003`/`CS1519`)
   near a `|` in a type position, the lightbulb offers to replace it with
   `global::UnionTypes.Union<T1, T2>`.

This works in **both Visual Studio and VS Code** (via Roslyn analyzer reference).
The analyzer only fires in files under a `Source/` directory — normal C# files
are never affected.

## CLI usage

The transpiler can also be invoked directly:

```bash
dotnet run --project UnionTypes.Transpiler -- \
  --input samples/BasicSample/Source \
  --output samples/BasicSample/obj/UnionTranspiler
```

Options:

| Option | Description |
|---|---|
| `--input <PATH>` | Input directory or single `.cs` file (required). |
| `--output <PATH>` | Output directory. Default: `<input>/obj/UnionTranspiler`. |
| `--dry-run` | Do not write files; report planned actions. |
| `--verbose` | Emit progress details. |
| `--runtime-namespace <NS>` | Fully-qualified runtime namespace (default `UnionTypes`). |
| `-h`, `--help` | Show help. |

Behavior:

- Recursively processes all `*.cs` files under the input.
- Skips anything under `bin/` or `obj/`.
- Preserves the relative directory structure in the output.
- Copies non-transformed files verbatim so the output builds standalone.
- Returns a non-zero exit code when diagnostics are emitted.

## Runtime API: `Union<T1, T2>`

```csharp
using UnionTypes;

Union<string, int> u = "hello";

if (u.IsT1) { /* ... */ }
if (u.TryGetT2(out var i)) { /* ... */ }

string label = u.Match(s => $"s:{s}", n => $"n:{n}");
u.Switch(s => Console.WriteLine(s), n => Console.WriteLine(n));
```

The active variant is tracked by an internal tag, so `null` is handled correctly:

```csharp
Union<string?, int> u = (string?)null;   // IsT1 == true, value is null
```

Implicit conversions exist for both variants. **Identical variant types**
(`Union<T, T>`) throw at static initialization and are rejected by the transpiler
(`UT004`). **Overlapping types** such as `Union<object, string>` are permitted but
ambiguous on assignment — the implicit operators prefer `T2`.

## Supported syntax (Phase 1)

Union types are recognized at the following type positions:

```csharp
// Local variable
string | int value = GetValue();

// Field
private string | int _value;

// Property
public string | int Value { get; init; }

// Parameter
void Print(string | int value) { }

// Return type
string | int GetValue() => "test";

// Generic argument
List<string | int> values;
```

Type variants may be:

```csharp
string | int value;
string | int? value;
List<string> | Dictionary<string, int> value;
(string Name, int Age) | Guid value;
```

The generated output is fully-qualified to avoid `using` conflicts:

```csharp
global::UnionTypes.Union<string, int> value;
```

## Context sensitivity

`|` already exists in C# as the bitwise-OR operator. The transpiler is context
sensitive and only interprets `|` as a union in **type position**. The following are
left unchanged:

```csharp
var flags = first | second;        // bitwise OR expression
var text = "string | int";          // string literal
// string | int                     // comment
M(a | b);                           // call argument expression
```

## Diagnostics

Errors include file, line and column:

| Code | Meaning |
|---|---|
| `UT001` | More than two union variants not yet supported. |
| `UT002` | Union type not supported at this position. |
| `UT003` | Invalid or unparseable union-type syntax. |
| `UT004` | Identical union members not supported. |
| `UT005` | Unsupported type construct inside a union. |

Example:

```
Sample.cs(12,18): error UT001: Union Types mit mehr als zwei Varianten werden noch nicht unterstützt:
string | int | Guid
```

## Known limitations (Phase 1)

- At most **two** union variants (`A | B | C` → `UT001`).
- No union expressions, automatic dispatch, or generated switch expressions.
- No pattern-matching union semantics, no `using` aliases.
- No arrays, pointers, ref structs, or function pointers inside unions (`UT005`).
- No method-overload permutations.
- Identical variant types (`Union<T, T>`) are rejected.
- Overlapping types (e.g. `Union<object, string>`) compile but may be ambiguous.

## Implementation notes

The transpiler uses Roslyn's `SyntaxFactory.ParseTokens` to tokenize the source, then a
small recursive parser walks the token stream at known type-start positions, parsing
type syntax (qualified names, generics, tuples, nullable) and rewriting `Type | Type`
into `global::NS.Union<Type, Type>`. Comments and string/char literals are never touched.
