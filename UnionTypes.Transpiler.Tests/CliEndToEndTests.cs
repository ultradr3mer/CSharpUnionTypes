using System.Diagnostics;
using System.Reflection;
using UnionTypes.Transpiler;

namespace UnionTypes.Transpiler.Tests;

/// <summary>
/// CLI end-to-end tests driving <see cref="TranspilerApp"/> against a real temp directory.
/// </summary>
public class CliEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UnionTypesCliTests-" + Guid.NewGuid().ToString("N"));

    public CliEndToEndTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void TransformsFile_AndCopiesUntransformedFile()
    {
        var input = Path.Combine(_root, "src");
        Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "Union.cs"),
            "string | int value = default;");
        File.WriteAllText(Path.Combine(input, "Plain.cs"),
            "var x = 1 | 2;");

        var output = Path.Combine(_root, "out");
        var exit = RunTranspiler(input, output);
        Assert.Equal(0, exit);

        Assert.True(File.Exists(Path.Combine(output, "Union.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Plain.cs")));

        Assert.Equal(
            "global::UnionTypes.Union<string, int> value = default;",
            File.ReadAllText(Path.Combine(output, "Union.cs")));
        Assert.Equal("var x = 1 | 2;", File.ReadAllText(Path.Combine(output, "Plain.cs")));
    }

    [Fact]
    public void PreservesRelativeDirectoryStructure()
    {
        var input = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(input, "Sub", "Deep"));
        File.WriteAllText(Path.Combine(input, "Top.cs"), "string | int a;");
        File.WriteAllText(Path.Combine(input, "Sub", "Mid.cs"), "string | int b;");
        File.WriteAllText(Path.Combine(input, "Sub", "Deep", "Leaf.cs"), "string | int c;");

        var output = Path.Combine(_root, "out");
        var exit = RunTranspiler(input, output);
        Assert.Equal(0, exit);

        Assert.True(File.Exists(Path.Combine(output, "Top.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Sub", "Mid.cs")));
        Assert.True(File.Exists(Path.Combine(output, "Sub", "Deep", "Leaf.cs")));
    }

    [Fact]
    public void SkipsBinAndObjDirectories()
    {
        var input = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(input, "bin"));
        Directory.CreateDirectory(Path.Combine(input, "obj"));
        Directory.CreateDirectory(Path.Combine(input, "obj", "UnionTranspiler"));
        File.WriteAllText(Path.Combine(input, "Keep.cs"), "string | int a;");
        File.WriteAllText(Path.Combine(input, "bin", "Skip1.cs"), "string | int x;");
        File.WriteAllText(Path.Combine(input, "obj", "Skip2.cs"), "string | int y;");

        var output = Path.Combine(_root, "out");
        var exit = RunTranspiler(input, output);
        Assert.Equal(0, exit);

        Assert.True(File.Exists(Path.Combine(output, "Keep.cs")));
        Assert.False(File.Exists(Path.Combine(output, "bin", "Skip1.cs")));
        Assert.False(File.Exists(Path.Combine(output, "obj", "Skip2.cs")));
    }

    [Fact]
    public void DryRun_DoesNotWriteFiles()
    {
        var input = Path.Combine(_root, "src");
        Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "A.cs"), "string | int a;");

        var output = Path.Combine(_root, "out");
        var exit = RunTranspiler(input, output, dryRun: true);
        Assert.Equal(0, exit);

        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void ReturnsNonZero_WhenDiagnosticsAreEmitted()
    {
        var input = Path.Combine(_root, "src");
        Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "Bad.cs"), "string | int | Guid | bool | double value;");

        var output = Path.Combine(_root, "out");
        var exit = RunTranspiler(input, output);
        Assert.NotEqual(0, exit);
    }

    private int RunTranspiler(string input, string output, bool dryRun = false)
    {
        var opts = new CliOptions
        {
            Input = input,
            Output = output,
            DryRun = dryRun,
        };
        using var writer = new StringWriter();
        var app = new TranspilerApp(RealFileSystem.Instance, writer);
        return app.Run(opts);
    }
}
