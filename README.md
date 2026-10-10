# CSharpUnionTypes

> **English summary:** An experimental preprocessor/transpiler that adds union type syntax to C#.
> `string | int value = GetValue();` is rewritten before compilation to
> `global::UnionTypes.Union<string, int> value = GetValue();`. It runs as a source-to-source
> transform before `dotnet build`, supports up to four variants, and never modifies your original files.
> The rest of this README is in German.

Ein experimenteller Preprocessor/Transpiler, der **Union-Type-Syntax** zu C# hinzufügt.

```csharp
string | int value = GetValue();
```

wird vor dem Kompilieren automatisch umgewandelt in:

```csharp
global::UnionTypes.Union<string, int> value = GetValue();
```

Der Transpiler erweitert **nicht** den C#-Compiler. Er läuft als Source-to-Source-Transform
vor `dotnet build` und erzeugt normale `.cs`-Dateien. Originaldateien werden nie verändert.

Unterstützt werden **bis zu vier Varianten**:

```csharp
string | int | bool | Guid value = GetValue();
// → global::UnionTypes.Union<string, int, bool, Guid> value = GetValue();
```

---

## Inhaltsverzeichnis

- [Projektstruktur](#projektstruktur)
- [Schnellstart](#schnellstart)
- [Installation](#installation)
- [MSBuild-Integration](#msbuild-integration)
- [IDE-Integration (Code-Fixes)](#ide-integration-code-fixes)
- [VS Code-Integration (Native Feel)](#vs-code-integration-native-feel)
- [CLI-Verwendung](#cli-verwendung)
- [Runtime-API](#runtime-api)
- [Pattern Matching & Deconstruct](#pattern-matching--deconstruct)
- [Unterstützte Syntax](#unterstützte-syntax)
- [Kontextsensitivität](#kontextsensitivität)
- [Diagnosen](#diagnosen)
- [Bekannte Einschränkungen](#bekannte-einschränkungen)
- [Implementierung](#implementierung)

---

## Projektstruktur

| Projekt | Typ | Zweck |
|---|---|---|
| `UnionTypes.Runtime` | Class library | `Union<T1,T2>`, `Union<T1,T2,T3>`, `Union<T1,T2,T3,T4>` |
| `UnionTypes.Transpiler` | Console app | CLI-Transpiler + MSBuild-Targets |
| `UnionTypes.CodeFixes` | Roslyn analyzer | IDE-Glühbirne: `Union<X,Y>` ↔ `X \| Y` |
| `UnionTypes.Generators` | Source generator | `Deconstruct`-Methoden für Pattern Matching |
| `UnionTypes.Runtime.Tests` | xUnit | 34 Tests für die Union-Runtime |
| `UnionTypes.Transpiler.Tests` | xUnit | 26 Tests (inkl. dotnet-build-Integration) |
| `UnionTypes.CodeFixes.Tests` | xUnit | 7 Analyzer- und CodeFix-Tests |
| `UnionTypes.Generators.Tests` | xUnit | 5 Source-Generator-Tests |
| `BasicSample` | Console app | Beispielprojekt mit Union-Syntax |
| `vscode-extension` | VS Code Extension | Syntax-Highlighting für `\|` in Typ-Position |

```text
src/  UnionTypes.Runtime/         — Union<T1,T2> / <T1,T2,T3> / <T1,T2,T3,T4>
      UnionTypes.Transpiler/      — CLI + Token-basierter Parser
      UnionTypes.CodeFixes/       — Roslyn DiagnosticAnalyzer + CodeFixProvider
      UnionTypes.Generators/      — IIncrementalGenerator (Deconstruct)
      UnionTypes.Transpiler/build/ — UnionTypes.Transpiler.targets (MSBuild)
tests/UnionTypes.Runtime.Tests/   — 34 Tests
      UnionTypes.Transpiler.Tests/— 26 Tests (inkl. dotnet-build-Integration)
      UnionTypes.CodeFixes.Tests/ —  7 Tests
      UnionTypes.Generators.Tests/—  5 Tests
samples/BasicSample/Source/       — Union-Syntax-Quellcode (NICHT gültiges C#)
        BasicSample/BasicSample.csproj — referenziert Runtime + Transpiler + CodeFixes + Generator
```

```bash
dotnet build UnionTypes.slnx
dotnet test  UnionTypes.slnx
```

---

## Schnellstart

1. **Repository klonen und bauen:**

   ```bash
   git clone https://github.com/ultradr3mer/CSharpUnionTypes.git
   cd CSharpUnionTypes
   dotnet build UnionTypes.slnx
   ```

2. **Sample ausführen** — `dotnet build` transformiert `Source/*.cs` automatisch:

   ```bash
   dotnet run --project BasicSample
   ```

   Ausgabe:

   ```
   string: hello
   i:42
   ExampleFunction.NonZeroEnumerable(new int[] { -1, 0, 1, 2, 3 }): 1, 2, 3
   ExampleFunction.NonZeroEnumerable(1): 1
   Describe3("test"): string: test
   Describe3(42): int: 42
   Describe3(true): bool: True
   Describe4(3.14): double: 3.14
   Deconstruct value: s=, i=42
   Deconstruct multi: s3=, i3=0, b3=True
   ```

3. **Union-Syntax ausprobieren** — `BasicSample/Source/ExampleFunction.cs` bearbeiten:

   ```csharp
   public static IEnumerable<int> NonZeroEnumerable(IEnumerable<int> | int input)
   {
       IEnumerable<int> normalized = input.Match(
           enumerable => enumerable,
           i => new int[] { i });
       return normalized.Where(item => item > 0);
   }

   public static string Describe3(string | int | bool input)
   {
       return input.Match(
           s => $"string: {s}",
           i => $"int: {i}",
           b => $"bool: {b}");
   }
   ```

   Beim nächsten `dotnet build` wandelt der Transpiler `IEnumerable<int> | int` in
   `global::UnionTypes.Union<IEnumerable<int>, int>` um und `string | int | bool` in
   `global::UnionTypes.Union<string, int, bool>`.

---

## Installation

In einem konsumierenden Projekt vier Referenzen setzen und die MSBuild-Targets importieren:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <UnionTypesEnabled>true</UnionTypesEnabled>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\UnionTypes.Runtime\UnionTypes.Runtime.csproj" />
    <ProjectReference Include="..\UnionTypes.Transpiler\UnionTypes.Transpiler.csproj" />
    <ProjectReference Include="..\UnionTypes.CodeFixes\UnionTypes.CodeFixes.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\UnionTypes.Generators\UnionTypes.Generators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <Import Project="..\UnionTypes.Transpiler\build\UnionTypes.Transpiler.targets" />

</Project>
```

Union-Syntax-Dateien in das `Source/`-Unterverzeichnis legen. Fertig.

---

## MSBuild-Integration

Sobald die Targets importiert sind, führt `dotnet build` automatisch Folgendes aus:

1. **Vor `CoreCompile`**: Der Transpiler verarbeitet alle `Source/**/*.cs`-Dateien.
2. **Ersetzung**: Union-Syntax-Dateien (`Source/`) werden aus dem Compile-Set entfernt.
3. **Generierung**: Transformierte `.cs`-Dateien aus `obj/UnionTranspiler/` werden dem Compile-Set hinzugefügt.
4. **Inkrementell**: Bei unveränderten Quelldateien wird der Transpiler übersprungen.

```text
Source/ExampleFunction.cs          →  obj/UnionTranspiler/ExampleFunction.cs
  IEnumerable<int> | int input           global::UnionTypes.Union<IEnumerable<int>, int> input
```

### Konfiguration

| Eigenschaft | Standard | Beschreibung |
|---|---|---|
| `UnionTypesEnabled` | `false` | Aktiviert die MSBuild-Integration. |
| `UnionTypesSourceGlob` | `Source\**\*.cs` | Glob für Union-Syntax-Quelldateien. |
| `UnionTypesSourceRoot` | `$(MSBuildProjectDirectory)\Source` | Wurzelverzeichnis der Quellcodes. |
| `UnionTypesOutputDir` | `$(MSBuildProjectDirectory)\obj\UnionTranspiler` | Ausgabeverzeichnis. |
| `UnionTypesRuntimeNamespace` | `UnionTypes` | Namespace des Union-Typs. |

---

## IDE-Integration (Code-Fixes)

Der `UnionTypes.CodeFixes`-Roslyn-Analyzer bietet zwei Glühbirnen-Aktionen, die in
**Visual Studio und VS Code** funktionieren:

### 1. Union → Pipe

Schreibt man `Union<T1, T2>` in einer `Source/`-Datei, erscheint eine Glühbirne,
die anbietet, dies als `T1 | T2` zu schreiben:

```csharp
// Vorher (in Source/Datei.cs):
public Union<IEnumerable<int>, int> NonZeroEnumerable(Union<IEnumerable<int>, int> input)

// Nach Code-Fix:
public IEnumerable<int> | int NonZeroEnumerable(IEnumerable<int> | int input)
```

### 2. Pipe → Union

Schreibt man `T1 | T2` in einer `Source/`-Datei, meldet der Compiler Syntax-Fehler
(`CS1003`/`CS1519`). Die Glühbirne bietet an, dies durch `global::UnionTypes.Union<T1, T2>`
zu ersetzen:

```csharp
// Vorher (in Source/Datei.cs):
public static IEnumerable<int> NonZeroEnumerable(IEnumerable<int> | int input)

// Nach Code-Fix:
public static IEnumerable<int> NonZeroEnumerable(global::UnionTypes.Union<IEnumerable<int>, int> input)
```

Der Analyzer feuert **nur** in Dateien unter einem `Source/`-Verzeichnis — normale C#-Dateien
werden nie beeinflusst.

---

## VS Code-Integration (Native Feel)

Drei Komponenten machen `|` in VS Code fast wie ein natives Sprachfeature wirken lassen:

### 1. `.editorconfig` — Weiche Wellenlinien

In `Source/`-Dateien werden die Compiler-Fehler `CS1003`/`CS1519`/`CS1001`/`CS1002`
auf `suggestion` herabgestuft — statt roter Fehlerwellen erscheinen grüne Punkte:

```ini
# .editorconfig
[Source/**.cs]
dotnet_diagnostic.CS1003.severity = suggestion
dotnet_diagnostic.CS1519.severity = suggestion
```

Funktioniert in VS und VS Code automatisch.

### 2. `.vscode/settings.json` — Auto-Fix beim Speichern

```json
{
    "editor.codeActionsOnSave": {
        "source.unionTypes.pipeToUnion": "explicit"
    }
}
```

Beim Speichern einer `Source/`-Datei wird automatisch der `PipeToUnion`-CodeFix
ausgeführt — `T1 | T2` wird zu `Union<T1, T2>` konvertiert.

### 3. VS Code Extension — Syntax-Highlighting für `|`

Die Extension in `vscode-extension/` färbt das `|` in Typ-Position als
"Union Operator" ein (standardmäßig lila/bold):

```bash
cd vscode-extension
npm install
npm run compile
# In VS Code: F5 zum Starten der Extension-Entwicklungsumgebung
```

Die Extension registriert einen `DocumentSemanticTokensProvider`, der `|`-Zeichen
außerhalb von Strings/Kommentaren in `Source/`-Dateien erkennt und mit der
benutzerdefinierten Token-Klasse `unionOperator` versieht.

---

## CLI-Verwendung

Der Transpiler kann auch direkt aufgerufen werden:

```bash
dotnet run --project UnionTypes.Transpiler -- \
  --input BasicSample/Source \
  --output BasicSample/obj/UnionTranspiler \
  --verbose
```

### Optionen

| Option | Beschreibung |
|---|---|
| `--input <PATH>` | Eingabeverzeichnis oder einzelne `.cs`-Datei (erforderlich). |
| `--output <PATH>` | Ausgabeverzeichnis. Standard: `<input>/obj/UnionTranspiler`. |
| `--dry-run` | Keine Dateien schreiben; geplante Aktionen ausgeben. |
| `--verbose` | Fortschrittsdetails ausgeben. |
| `--runtime-namespace <NS>` | Vollqualifizierter Runtime-Namespace (Standard: `UnionTypes`). |
| `-h`, `--help` | Hilfe anzeigen. |

### Verhalten

- Rekursiv alle `*.cs`-Dateien unter dem Eingabepfad verarbeiten.
- Dateien unter `bin/` und `obj/` überspringen.
- Relative Verzeichnisstruktur im Ausgabepfad beibehalten.
- Nicht-transformierte Dateien werden unverändert kopiert, damit der Output eigenständig kompilierbar ist.
- Exit-Code ≠ 0 bei Diagnosen.

---

## Runtime-API

Es gibt drei Union-Typen mit 2, 3 und 4 Varianten:

| Typ | Varianten |
|---|---|
| `Union<T1, T2>` | 2 |
| `Union<T1, T2, T3>` | 3 |
| `Union<T1, T2, T3, T4>` | 4 |

Alle bieten dieselbe API: `IsTN`, `TryGetTN`, `Match`, `Switch`, implizite Operatoren,
`IEquatable`, `ToString`.

```csharp
using UnionTypes;

// 2-Variant
Union<string, int> u = "hello";
if (u.IsT1) { /* ... */ }
if (u.TryGetT2(out var i)) { /* ... */ }
string label = u.Match(s => $"s:{s}", n => $"n:{n}");

// 3-Variant
Union<string, int, bool> u3 = 42;
string label3 = u3.Match(
    s => $"string:{s}",
    i => $"int:{i}",
    b => $"bool:{b}");

// 4-Variant
Union<string, int, bool, double> u4 = 3.14;
u4.Switch(
    s => Console.WriteLine(s),
    i => Console.WriteLine(i),
    b => Console.WriteLine(b),
    d => Console.WriteLine(d));
```

### Null-Handling

Der aktive Variant wird über einen internen Tag verfolgt, nicht über `is T`.
Dadurch wird `null` korrekt behandelt:

```csharp
Union<string?, int> u = (string?)null;   // IsT1 == true, Wert ist null
```

### Implizite Konvertierungen

Alle Varianten haben implizite Operatoren:

```csharp
Union<string, int> a = "hello";   // → T1
Union<string, int> b = 42;        // → T2

Union<string, int, bool> c = true; // → T3
```

### Identische Typen

`Union<T, T>` wirft bei der statischen Initialisierung eine `InvalidOperationException`
und wird vom Transpiler mit `UT004` abgelehnt. Gleiches gilt für `Union<T1, T2, T3>`
wenn zwei Typen identisch sind.

### Überlappende Typen

`Union<object, string>` ist erlaubt, aber bei der Zuweisung eines `string` ist
mehrdeutig, ob `T1` (`object`) oder `T2` (`string`) gemeint ist. Die impliziten
Operatoren bevorzugen `T2`.

---

## Pattern Matching & Deconstruct

Der `UnionTypes.Generators`-Source-Generator erzeugt automatisch `Deconstruct`-
Extension-Methods für jede `Union<T1,...,TN>`-Instanziierung im Code. Dadurch
wird C#-Deconstruction und Positions-Pattern-Matching möglich:

```csharp
using UnionTypes;

Union<string, int> u = 42;

// Deconstruction
var (s, i) = u;
// s == null (T1 nicht aktiv), i == 42 (T2 aktiv)

// 3-Variant Deconstruction
Union<string, int, bool> u3 = true;
var (s3, i3, b3) = u3;
// s3 == null, i3 == 0, b3 == true
```

Der Generator:
- Scant alle `Union<T1,...,TN>`-Verwendungen im Compilation
- Generiert `Deconstruct(this Union<...>, out T1? t1, out T2? t2, ...)`
- Dedupliziert gleiche Instantiierungen
- Unbound generics (`typeof(Union<,>)`) werden übersprungen
- Generiert in `namespace UnionTypes` (über `using UnionTypes;` verfügbar)

---

## Unterstützte Syntax

Union-Typen werden an den folgenden Typ-Positionen erkannt:

```csharp
// Lokale Variable
string | int value = GetValue();

// Feld
private string | int _value;

// Property
public string | int Value { get; init; }

// Parameter
void Print(string | int value) { }

// Rückgabetyp
string | int GetValue() => "test";

// Generisches Argument
List<string | int> values;
```

### Unterstützte Typ-Varianten

```csharp
string | int value;                                    // 2 Varianten
string | int? value;                                  // Nullable
List<string> | Dictionary<string, int> value;         // verschachtelte Generics
(string Name, int Age) | Guid value;                  // Tuple-Typen
IEnumerable<int> | int input;                         // generischer + einfacher Typ
string | int | bool value;                             // 3 Varianten
string | int | bool | Guid value;                      // 4 Varianten
```

### Generierter Output

Der generierte Code ist vollqualifiziert, um `using`-Konflikte zu vermeiden:

```csharp
global::UnionTypes.Union<string, int> value;
global::UnionTypes.Union<IEnumerable<int>, int> input;
global::UnionTypes.Union<string, int, bool> multi;
global::UnionTypes.Union<string, int, bool, Guid> multi4;
```

---

## Kontextsensitivität

`|` existiert in C# bereits als bitweiser OR-Operator. Der Transpiler ist
kontextsensitiv und interpretiert `|` **nur** in Typ-Position als Union.

Folgendes wird **nicht** verändert:

```csharp
var flags = first | second;        // bitweiser OR-Ausdruck
var text = "string | int";          // String-Literal
// string | int                     // Kommentar
M(a | b);                           // Methoden-Argument-Ausdruck
```

Kommentare, String-Literale und Char-Literale werden nie angetastet.

---

## Diagnosen

Fehlermeldungen enthalten Dateiname, Zeile und Spalte:

| Code | Bedeutung |
|---|---|
| `UT001` | Mehr als vier Union-Varianten noch nicht unterstützt. |
| `UT002` | Union-Typ an dieser Position nicht unterstützt. |
| `UT003` | Ungültige oder nicht parsebare Union-Type-Syntax. |
| `UT004` | Identische Union-Mitglieder nicht unterstützt. |
| `UT005` | Nicht unterstütztes Type-Konstrukt innerhalb einer Union. |

Beispiel:

```
Sample.cs(12,18): error UT001: Union Types mit mehr als vier Varianten werden noch nicht unterstützt:
string | int | Guid | bool | double
```

---

## Bekannte Einschränkungen

- Maximal **vier** Union-Varianten (`A | B | C | D | E` → `UT001`).
- Keine Union-Ausdrücke, kein automatisches Dispatch.
- `input is int` funktioniert nicht (C# `is` prüft Runtime-Typ, nicht Variant-Tag) — `IsT1`/`Match`/`TryGetTN` verwenden.
- `var (s, i) = u;` (Deconstruct) liefert alle Varianten als nullable; die inaktiven sind `null`/`default`. Der aktive Variant muss über `IsTN` geprüft werden.
- Keine `using`-Aliase (`using Value = string | int;`).
- Keine Arrays, Pointer, ref structs oder Function Pointer innerhalb von Unions (`UT005`).
- Keine Methodenüberladungs-Permutationen.
- Identische Variant-Typen (`Union<T, T>`) werden abgelehnt.
- Überlappende Typen (`Union<object, string>`) kompilieren, sind aber ggf. mehrdeutig.
- Die `|`-Syntax ist kein gültiges C# — die IDE zeigt Wellenlinien.
  Die Glühbirne (CodeFix) und `.editorconfig` mildern dies ab (siehe [VS Code-Integration](#vs-code-integration-native-feel)).

---

## Implementierung

### Transpiler (`UnionTypes.Transpiler`)

Der Transpiler verwendet Roslyns `SyntaxFactory.ParseTokens`, um den Source zu
tokenisieren. Ein rekursiver Parser wandert den Token-Stream an bekannten
Typ-Start-Positionen ab, parst Typ-Syntax (qualifizierte Namen, Generics, Tuples,
Nullable) und sammelt alle `Type | Type | ...` Varianten. Bei 2-4 Varianten wird
`global::NS.Union<T1, ..., TN>` generiert, bei >4 Varianten `UT001`.

Kommentare, String- und Char-Literale werden nie verändert — Roslyns Token-Kinds
machen die Unterscheidung trivial.

### MSBuild-Integration (`UnionTypes.Transpiler.targets`)

Ein MSBuild-Target läuft `BeforeTargets="CoreCompile"`, ruft den Transpiler via
`dotnet run` auf, tauscht die Compile-Items aus (Original `Source/` raus,
generierte `obj/UnionTranspiler/` rein) und respektiert inkrementelle Builds
via `Inputs`/`Outputs`.

### CodeFixes (`UnionTypes.CodeFixes`)

Ein `DiagnosticAnalyzer` (Severity `Hidden` + `Unnecessary`) erkennt
`Union<T1, T2>` in `Source/`-Dateien und meldet `UT0101`. Der
`CodeFixProvider` bietet zwei Aktionen:
- `UT0101` → ersetzt `Union<T1,T2>` durch `T1 | T2` (Text-Ersetzung, da `|`
  keine gültige Typ-Syntax ist und nicht als SyntaxNode geparst werden kann).
- `CS1003`/`CS1519` → lokalisiert das `|`-Token, parst linke/rechte Typen
  aus dem Token-Stream und ersetzt durch `global::UnionTypes.Union<T1, T2>`.

### Source Generator (`UnionTypes.Generators`)

Ein `IIncrementalGenerator` scannt das Compilation nach `GenericNameSyntax` mit
Name "Union" und 2-4 Typ-Argumenten, bestätigt via SemanticModel dass es
`UnionTypes.Union` ist, und generiert `Deconstruct`-Extension-Methods in
`namespace UnionTypes`. Dies ermöglicht `var (s, i) = unionValue;`.

### Runtime (`UnionTypes.Runtime`)

Drei `readonly struct`-Typen mit `object? _value` + `byte _tag`.
Der Tag (0=uninitialisiert, 1..N=T1..TN) ermöglicht korrekte `null`-Behandlung
ohne `dynamic`. Alle implementieren `IEquatable` und bieten `IsTN`/`TryGetTN`/
`Match`/`Switch`/implizite Operatoren.

---

## Lizenz

Siehe Projekt-Repository.