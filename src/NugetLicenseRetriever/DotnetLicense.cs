namespace NugetLicenseRetriever;

public record DotnetLicense
{
    public required string Path { get; init; }
    public required PackageInfo LicenseInfo { get; init; }
}
