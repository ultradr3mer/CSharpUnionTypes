using System.Diagnostics;
using System.Reflection;
using UnionTypes.Transpiler;

namespace UnionTypes.Transpiler.Tests;

/// <summary>
/// Integration test: transpiles a sample project's union syntax, then compiles the
/// generated output with <c>dotnet build</c> referencing the real runtime, asserting
/// the build succeeds and the program produces the expected output.
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UnionTypesIT-" + Guid.NewGuid().ToString("N"));

    public IntegrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Transpile_ThenBuild_ThenRun_Succeeds()
    {
        var runtimeCsproj = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "UnionTypes.Runtime", "UnionTypes.Runtime.csproj"));
        Assert.True(File.Exists(runtimeCsproj), $"Runtime project not found at {runtimeCsproj}");

        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "Program.cs"), """
            using UnionTypes;

            string | int value = "hello";
            var result = value.Match(s => $"s:{s}", i => $"i:{i}");
            System.Console.WriteLine(result);
            """);
        File.WriteAllText(Path.Combine(src, "WithUnion.cs"), """
            namespace Demo;

            public class WithUnion
            {
                public string | int Field;
            }
            """);

        var output = Path.Combine(_root, "out");
        var exit = RunTranspiler(src, output);
        Assert.Equal(0, exit);

        var projectDir = Path.Combine(_root, "project");
        Directory.CreateDirectory(projectDir);
        var csprojPath = Path.Combine(projectDir, "IT.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <AssemblyName>itapp</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="RUNTIME" />
                <Compile Include="OUT\**\*.cs" />
              </ItemGroup>
            </Project>
            """.Replace("RUNTIME", runtimeCsproj).Replace("OUT", Path.GetFullPath(output)));

        var (buildExit, buildOutput) = RunDotNet(projectDir, "build", "-nologo", "-v", "quiet");
        Assert.True(buildExit == 0, $"Build failed:{Environment.NewLine}{buildOutput}");

        var (runExit, runOutput) = RunDotNet(projectDir, "run", "--no-build", "-c", "Debug");
        Assert.True(runExit == 0, $"Run failed:{Environment.NewLine}{runOutput}");
        Assert.Contains("s:hello", runOutput);
    }

    private int RunTranspiler(string input, string output)
    {
        var opts = new CliOptions { Input = input, Output = output };
        using var writer = new StringWriter();
        var app = new TranspilerApp(RealFileSystem.Instance, writer);
        return app.Run(opts);
    }

    private static (int exit, string output) RunDotNet(string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet", string.Join(" ", args))
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(TimeSpan.FromSeconds(120));
        return (p.ExitCode, stdout + Environment.NewLine + stderr);
    }
}
