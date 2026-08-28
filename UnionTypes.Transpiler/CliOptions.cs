namespace UnionTypes.Transpiler;

/// <summary>
/// Parsed command-line options for the union types transpiler.
/// </summary>
public sealed class CliOptions
{
    /// <summary>Input directory or file. Required.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Output directory. Defaults to <c>obj/UnionTranspiler</c> next to input.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>When true, do not write output, only report planned actions.</summary>
    public bool DryRun { get; set; }

    /// <summary>When true, emit verbose progress information.</summary>
    public bool Verbose { get; set; }

    /// <summary>Fully-qualified namespace for the Union runtime type. Defaults to <c>UnionTypes</c>.</summary>
    public string RuntimeNamespace { get; set; } = "UnionTypes";

    /// <summary>Parses the supplied arguments. Returns null with an error message on failure.</summary>
    public static (CliOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var opts = new CliOptions();

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            bool TakeValue(out string value)
            {
                if (i + 1 >= args.Count)
                {
                    value = string.Empty;
                    return false;
                }

                value = args[++i];
                return true;
            }

            switch (arg)
            {
                case "--input":
                    if (!TakeValue(out var input)) return (null, "--input requires a value.");
                    opts.Input = input;
                    break;
                case "--output":
                    if (!TakeValue(out var output)) return (null, "--output requires a value.");
                    opts.Output = output;
                    break;
                case "--dry-run":
                    opts.DryRun = true;
                    break;
                case "--verbose":
                    opts.Verbose = true;
                    break;
                case "--runtime-namespace":
                    if (!TakeValue(out var ns)) return (null, "--runtime-namespace requires a value.");
                    opts.RuntimeNamespace = ns;
                    break;
                case "-h":
                case "--help":
                    return (null, BuildHelp());
                default:
                    return (null, $"Unknown argument '{arg}'.{Environment.NewLine}{BuildHelp()}");
            }
        }

        if (string.IsNullOrWhiteSpace(opts.Input))
        {
            return (null, "--input is required." + Environment.NewLine + BuildHelp());
        }

        return (opts, null);
    }

    /// <summary>Builds a short help text.</summary>
    public static string BuildHelp()
    {
        return """
        Usage: uniontypes-transpiler --input <directory-or-file> --output <directory> [options]

        Options:
          --input <PATH>              Input directory or .cs file (required).
          --output <PATH>             Output directory (default: <input>/obj/UnionTranspiler).
          --dry-run                   Do not write files; report planned actions.
          --verbose                   Emit verbose progress.
          --runtime-namespace <NS>    Fully-qualified runtime namespace (default: UnionTypes).
          -h, --help                  Show this help.
        """;
    }
}
