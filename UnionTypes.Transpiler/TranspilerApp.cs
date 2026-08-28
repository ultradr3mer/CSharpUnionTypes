using System.Text;

namespace UnionTypes.Transpiler;

/// <summary>
/// Orchestrates transpilation of a set of C# source files.
/// </summary>
public sealed class TranspilerApp
{
    private readonly IFileSystem _fs;
    private readonly TextWriter _log;

    public TranspilerApp(IFileSystem fileSystem, TextWriter log)
    {
        _fs = fileSystem;
        _log = log;
    }

    /// <summary>Runs the transpiler with the supplied options. Returns the process exit code.</summary>
    public int Run(CliOptions options)
    {
        var inputPath = _fs.GetFullPath(options.Input);
        string outputPath;
        if (!string.IsNullOrWhiteSpace(options.Output))
        {
            outputPath = _fs.GetFullPath(options.Output);
        }
        else if (_fs.DirectoryExists(inputPath))
        {
            outputPath = _fs.Combine(inputPath, "obj", "UnionTranspiler");
        }
        else
        {
            var dir = _fs.GetFullPath(_fs.Combine(inputPath, ".."));
            outputPath = _fs.Combine(dir, "obj", "UnionTranspiler");
        }

        if (!_fs.DirectoryExists(inputPath) && !_fs.FileExists(inputPath))
        {
            _log.WriteLine($"error: input not found: {inputPath}");
            return 2;
        }

        var files = EnumerateInputFiles(inputPath).ToList();
        if (options.Verbose)
        {
            _log.WriteLine($"Input:  {inputPath}");
            _log.WriteLine($"Output: {outputPath}");
            _log.WriteLine($"Files:  {files.Count}");
        }

        var allDiagnostics = new List<Diagnostic>();
        int transformedCount = 0;

        foreach (var file in files)
        {
            var relPath = GetRelativePath(inputPath, file);
            var destPath = _fs.Combine(outputPath, relPath);
            var source = _fs.ReadAllText(file);

            if (source is null)
            {
                _log.WriteLine($"error: could not read {file}");
                allDiagnostics.Add(new Diagnostic("UT000", file, 0, 0, "Could not read file."));
                continue;
            }

            var result = UnionTransformer.Transform(source, file, options.RuntimeNamespace);

            if (result.Diagnostics.Count > 0)
            {
                allDiagnostics.AddRange(result.Diagnostics);
            }

            bool changed = !string.Equals(source, result.Output, StringComparison.Ordinal);
            if (changed)
            {
                transformedCount++;
            }

            if (options.DryRun)
            {
                var label = changed ? "transform" : "copy";
                _log.WriteLine($"[dry-run] {label} {relPath}");
                if (options.Verbose && changed)
                {
                    _log.WriteLine("---");
                    _log.WriteLine(result.Output);
                    _log.WriteLine("---");
                }
                continue;
            }

            var destDir = _fs.GetFullPath(_fs.Combine(destPath, ".."));
            if (!_fs.DirectoryExists(destDir))
            {
                _fs.CreateDirectory(destDir);
            }

            _fs.WriteAllText(destPath, result.Output);
            if (options.Verbose)
            {
                _log.WriteLine($"{(changed ? "transform" : "copy")} {relPath} -> {destPath}");
            }
        }

        if (options.Verbose || options.DryRun)
        {
            _log.WriteLine($"Transformed {transformedCount} of {files.Count} files.");
        }

        if (allDiagnostics.Count > 0)
        {
            foreach (var d in allDiagnostics)
            {
                _log.WriteLine(d.ToString());
            }
            return 1;
        }

        return 0;
    }

    private IEnumerable<string> EnumerateInputFiles(string inputPath)
    {
        if (_fs.FileExists(inputPath))
        {
            if (!IsUnderBinOrObj(inputPath))
            {
                yield return inputPath;
            }
            yield break;
        }

        foreach (var f in _fs.EnumerateFiles(inputPath, "*.cs", recursive: true))
        {
            if (!IsUnderBinOrObj(f))
            {
                yield return f;
            }
        }
    }

    private static bool IsUnderBinOrObj(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var p in parts)
        {
            if (string.Equals(p, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "obj", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private string GetRelativePath(string inputPath, string file)
    {
        if (_fs.DirectoryExists(inputPath))
        {
            return _fs.GetRelativePath(inputPath, file);
        }
        return Path.GetFileName(file);
    }
}
