using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace UnionTypes.Generators.Tests;

public class GeneratorTests
{
    [Fact]
    public async Task Deconstruct_Generated_For_Union2()
    {
        var generatedCode = await RunGeneratorAsync("""
            using UnionTypes;
            namespace TestNs
            {
                public class C
                {
                    public Union<string, int> Field = "hello";
                }
            }
            """);

        Assert.Contains("Deconstruct", generatedCode);
        Assert.Contains("Union<string, int>", generatedCode);
        Assert.Contains("out string? t1", generatedCode);
        Assert.Contains("out int? t2", generatedCode);
        Assert.Contains("TryGetT1", generatedCode);
        Assert.Contains("TryGetT2", generatedCode);
    }

    [Fact]
    public async Task Deconstruct_Generated_For_Union3()
    {
        var generatedCode = await RunGeneratorAsync("""
            using UnionTypes;
            namespace TestNs
            {
                public class C
                {
                    public Union<string, int, bool> Field = "hello";
                }
            }
            """);

        Assert.Contains("Union<string, int, bool>", generatedCode);
        Assert.Contains("out string? t1", generatedCode);
        Assert.Contains("out int? t2", generatedCode);
        Assert.Contains("out bool? t3", generatedCode);
        Assert.Contains("TryGetT3", generatedCode);
    }

    [Fact]
    public async Task Deconstruct_Generated_For_Union4()
    {
        var generatedCode = await RunGeneratorAsync("""
            using UnionTypes;
            using System;
            namespace TestNs
            {
                public class C
                {
                    public Union<string, int, bool, Guid> Field = "hello";
                }
            }
            """);

        Assert.Contains("Union<string, int, bool, Guid>", generatedCode);
        Assert.Contains("out Guid? t4", generatedCode);
        Assert.Contains("TryGetT4", generatedCode);
    }

    [Fact]
    public async Task Deconstruct_Deduplicates_SameInstantiation()
    {
        var generatedCode = await RunGeneratorAsync("""
            using UnionTypes;
            namespace TestNs
            {
                public class A
                {
                    public Union<string, int> Field1 = "x";
                }
                public class B
                {
                    public Union<string, int> Field2 = 1;
                }
            }
            """);

        // Should only have ONE Deconstruct method for Union<string, int>
        var count = generatedCode.Split("public static void Deconstruct").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Deconstruct_NotGenerated_For_NonUnionTypes()
    {
        var generatedCode = await RunGeneratorAsync("""
            using System.Collections.Generic;
            namespace TestNs
            {
                public class C
                {
                    public List<string> Field = new();
                }
            }
            """);

        Assert.DoesNotContain("Deconstruct", generatedCode);
    }

    private static async Task<string> RunGeneratorAsync(string source)
    {
        var generator = new UnionDeconstructGenerator();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Union<,>).Assembly.Location),
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(generator)
            .RunGenerators(compilation)
            .GetRunResult()
            .GeneratedTrees
            .FirstOrDefault()?.ToString() ?? "";
    }
}