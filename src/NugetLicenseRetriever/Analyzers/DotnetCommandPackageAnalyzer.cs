using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NugetLicenseRetriever.Analyzers;

class DotnetCommandPackageAnalyzer : IPackageAnalyzer
{
    public async ValueTask AnalyzeAsync(string solutionOrProjectPath)
    {
        DotnetListResult result = await RetrievePackages(solutionOrProjectPath);
        foreach (DotnetProject project in result.Projects)
        {
            if (project.Frameworks == null) continue;
            foreach (DotnetFramework framework in project.Frameworks)
            {
                if (framework.TopLevelPackages != null)
                {
                    foreach (DotnetPackage package in framework.TopLevelPackages)
                    {
                        PackageStore.AddOrUpdatePackage(package.Id, package.ResolvedVersion, isTopLevel: true);
                    }
                }
                if (framework.TransitivePackages != null)
                {
                    foreach (DotnetPackage package in framework.TransitivePackages)
                    {
                        PackageStore.AddOrUpdatePackage(package.Id, package.ResolvedVersion, isTopLevel: false);
                    }
                }
            }
        }
    }

    private async ValueTask<DotnetListResult> RetrievePackages(string solutionOrProjectPath)
    {
        // Run dotnet list package command
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = $"""list "{solutionOrProjectPath}" package --include-transitive --format json""",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        string jsonOutput = await process.StandardOutput.ReadToEndAsync();
        string errorOutput = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet list package command failed with exit code {process.ExitCode}: {errorOutput}");
        }

        if (string.IsNullOrWhiteSpace(jsonOutput))
        {
            throw new InvalidOperationException("dotnet list package command returned empty output.");
        }

        DotnetListResult projects = JsonSerializer.Deserialize(jsonOutput, DotnetListResultContext.Default.DotnetListResult)
            ?? throw new InvalidOperationException("Failed to deserialize dotnet list package command output.");

        return projects;
    }
}

public record DotnetListResult(DotnetProject[] Projects);
public record DotnetProject(string Path, DotnetFramework[] Frameworks);
public record DotnetFramework(DotnetPackage[]? TopLevelPackages, DotnetPackage[]? TransitivePackages);
public record DotnetPackage(string Id, string ResolvedVersion);

[JsonSerializable(typeof(DotnetListResult), GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
public partial class DotnetListResultContext : JsonSerializerContext;
