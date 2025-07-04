namespace NugetLicenseRetriever;

public record PackageInfo
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string License { get; init; }
    public required bool IsTransitivePackage { get; init; }
}
