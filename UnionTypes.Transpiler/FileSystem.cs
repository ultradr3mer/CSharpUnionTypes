namespace UnionTypes.Transpiler;

/// <summary>
/// Abstraction over file system operations to keep the transpiler testable.
/// </summary>
public interface IFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string? ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);
    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);
    string GetFullPath(string path);
    string Combine(params string[] paths);
    string GetRelativePath(string relativeTo, string path);
    void CreateDirectory(string path);
}

/// <summary>
/// Default <see cref="IFileSystem"/> implementation backed by the real OS file system.
/// </summary>
public sealed class RealFileSystem : IFileSystem
{
    public static readonly RealFileSystem Instance = new();

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    public string? ReadAllText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        => File.Copy(sourcePath, destinationPath, overwrite);

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(directoryPath, searchPattern, option);
    }

    public string GetFullPath(string path) => Path.GetFullPath(path);
    public string Combine(params string[] paths) => Path.Combine(paths);
    public string GetRelativePath(string relativeTo, string path) => Path.GetRelativePath(relativeTo, path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
