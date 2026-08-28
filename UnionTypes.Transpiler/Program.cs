using UnionTypes.Transpiler;

var (options, error) = CliOptions.Parse(args);
if (options is null)
{
    if (error is not null)
    {
        Console.Error.WriteLine(error);
    }
    return error is not null ? 1 : 0;
}

var app = new TranspilerApp(RealFileSystem.Instance, Console.Out);
return app.Run(options);
