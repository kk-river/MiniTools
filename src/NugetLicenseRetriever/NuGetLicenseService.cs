using System.Collections.Concurrent;
using System.Net.Http;
using System.Xml.Linq;

namespace NugetLicenseRetriever;

public class NuGetLicense
{
    private readonly HttpClient _httpClient = new();
    private readonly XNamespace _ns2013 = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
    private readonly XNamespace _ns2012 = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd";
    private readonly XNamespace _ns2011 = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";
    private readonly Cache _cache = new();

    public async Task<string> GetNuGetLicenseAsync(string packageId, string version)
    {
        string cacheKey = packageId + "-" + version;
        if (_cache.TryGetValue(cacheKey, out string? license))
        {
            return license;
        }

        try
        {
            // NuGet API Format
            // https://api.nuget.org/v3-flatcontainer/{package-id}/{version}/{package-id}.nuspec
            string response = await _httpClient.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{packageId}/{version}/{packageId}.nuspec");
            XDocument doc = XDocument.Parse(response);

            string? licenseValue = doc.Descendants(_ns2013 + "metadata")
                .Select(x => x?.Element(_ns2013 + "license")?.Value)
                .FirstOrDefault()
                    ?? doc.Descendants(_ns2012 + "metadata")
                    .Select(x => x?.Element(_ns2012 + "license")?.Value)
                    .FirstOrDefault()
                        ?? doc.Descendants(_ns2011 + "metadata")
                        .Select(x => x?.Element(_ns2011 + "license")?.Value)
                        .FirstOrDefault();

            if (licenseValue is null)
            {
                string? licenseUrl = doc.Descendants(_ns2013 + "metadata")
                    .Select(x => x?.Element(_ns2013 + "licenseUrl")?.Value)
                    .FirstOrDefault()
                        ?? doc.Descendants(_ns2012 + "metadata")
                        .Select(x => x?.Element(_ns2012 + "licenseUrl")?.Value)
                        .FirstOrDefault()
                            ?? doc.Descendants(_ns2011 + "metadata")
                            .Select(x => x?.Element(_ns2011 + "licenseUrl")?.Value)
                            .FirstOrDefault();
                licenseValue = licenseUrl ?? "";
            }

            string value = licenseValue ?? "";
            _cache.TryAdd(cacheKey, value);
            return value;
        }
        catch (Exception)
        {
            // Return empty string on error and cache it to avoid repeated failures
            string value = "";
            _cache.TryAdd(cacheKey, value);
            return value;
        }
    }

    private class Cache()
    {
        private readonly ConcurrentDictionary<string, string> _cache = new();

        public bool TryGetValue(string key, out string value) => _cache.TryGetValue(key, out value!);

        public bool TryAdd(string key, string value) => _cache.TryAdd(key, value);
    }
}
