using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ConsoleAppFramework;
using Microsoft.Win32.SafeHandles;

namespace AppRunner;

public class Commands
{
    private bool _enableEnvironmentVariablesExpansion;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="exec">Path to executable.</param>
    /// <param name="optionsFile">Path to parameter file.</param>
    [Command("")]
    public void Root([Argument] string exec, bool enableEnvironmentVariablesExpansion, string? optionsFile = null)
    {
        _enableEnvironmentVariablesExpansion = enableEnvironmentVariablesExpansion;

        CommandOptions? options = Path.GetExtension(optionsFile) switch
        {
            ".json" => ReadJson(optionsFile!), //If optionsFile is null, Path.GetExtension returns null.
            ".yaml" => ReadYaml(optionsFile!),
            _ => null,
        };

        if (options is null)
        {
            Process.Start(exec);
            return;
        }

        if (options.WorkingDirectory is not null)
        {
            if (!Directory.Exists(options.WorkingDirectory)) { throw new DirectoryNotFoundException("Working directory does not exist."); }

            Environment.CurrentDirectory = options.WorkingDirectory;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = exec,
            WorkingDirectory = options.WorkingDirectory,
            Arguments = options?.ParamString
        };

        Console.WriteLine($"Starting process: {exec} {options?.ParamString}");
        Process.Start(startInfo);
    }

    private CommandOptions? ReadJson(string optionsFile)
        => JsonSerializer.Deserialize(ReadAllText(optionsFile), OptionsContext.Default.CommandOptions);

    //TODO: Implement YAML deserialization.
    private static CommandOptions? ReadYaml(string optionsFile)
    {
        throw new NotImplementedException();
    }

    private string ReadAllText(string path)
    {
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        int length = unchecked((int)RandomAccess.GetLength(handle));

        Span<byte> span = length < 1024 ? stackalloc byte[length] : new byte[length];

        int readLength = RandomAccess.Read(handle, span, 0);
        if (readLength != length) { throw new IOException("Failed to read the entire file."); }

        string jsonString = Encoding.UTF8.GetString(span);
        if (_enableEnvironmentVariablesExpansion)
        {
            jsonString = Environment.ExpandEnvironmentVariables(jsonString);
        }

        return jsonString;
    }

    //TODO: Implement Template generation.
    [Command("gen")]
    public void Generate([Argument] FileType fileType)
    {
        switch (fileType)
        {
            case FileType.Json:
                Console.WriteLine("Generating JSON file...");
                break;
            case FileType.Yaml:
                Console.WriteLine("Generating YAML file...");
                break;
            default:
                Console.WriteLine("Invalid file type.");
                break;
        }
    }
}
