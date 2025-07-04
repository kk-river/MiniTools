using Microsoft.Win32;
using NugetLicenseRetriever.Analyzers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace NugetLicenseRetriever.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<DotnetLicense> _allPackages = [];
    private readonly ObservableCollection<PackageInfo> _uniquePackages = [];
    private readonly NuGetLicense _nugetLicense = new();

    public MainWindow()
    {
        InitializeComponent();
        AllPackagesDataGrid.ItemsSource = _allPackages;
        UniquePackagesDataGrid.ItemsSource = _uniquePackages;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Solution or Project File",
            Filter = "Solution Files (*.sln)|*.sln|Project Files (*.csproj)|*.csproj|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            FilePathTextBox.Text = openFileDialog.FileName;
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        string projectPath = FilePathTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(projectPath))
        {
            ShowMessage("Please select a solution or project file.", "Warning", MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(projectPath))
        {
            ShowMessage($"File '{projectPath}' not found.", "Error", MessageBoxImage.Error);
            return;
        }

        await AnalyzePackagesAsync(projectPath);
    }

    private async Task AnalyzePackagesAsync(string projectPath)
    {
        try
        {
            SetAnalyzing(true);
            StatusTextBlock.Text = $"Analyzing packages for: {Path.GetFileName(projectPath)}";

            _allPackages.Clear();
            _uniquePackages.Clear();

            // Run dotnet list package command
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"list \"{projectPath}\" package --include-transitive --format json",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string jsonOutput = await process.StandardOutput.ReadToEndAsync();
            string errorOutput = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                ShowMessage($"Error running dotnet command:\n{errorOutput}", "Error", MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                ShowMessage("No output received from dotnet list package command.", "Warning", MessageBoxImage.Warning);
                return;
            }

            // Parse JSON and process packages
            var packages = JsonSerializer.Deserialize(jsonOutput, DotnetListResultContext.Default.DotnetCliListPackages);
            if (packages == null)
            {
                ShowMessage("Failed to parse package information.", "Error", MessageBoxImage.Error);
                return;
            }

            await ProcessPackagesAsync(packages);

            StatusTextBlock.Text = $"Analysis complete. Found {_allPackages.Count} package references ({_uniquePackages.Count} unique packages)";
            ExportButton.IsEnabled = _allPackages.Count > 0;
        }
        catch (Exception ex)
        {
            ShowMessage($"An error occurred during analysis:\n{ex.Message}", "Error", MessageBoxImage.Error);
            StatusTextBlock.Text = "Analysis failed";
        }
        finally
        {
            SetAnalyzing(false);
        }
    }

    private async Task ProcessPackagesAsync(DotnetCliListPackages packages)
    {
        var licenses = new List<DotnetLicense>();

        foreach (var project in packages.Projects)
        {
            string projectName = project.Path;

            foreach (var framework in project.Frameworks)
            {
                // Process top-level packages
                if (framework.TopLevelPackages != null)
                {
                    foreach (var package in framework.TopLevelPackages)
                    {
                        var licenseInfo = new DotnetLicense
                        {
                            Path = projectName,
                            LicenseInfo = new PackageInfo
                            {
                                Id = package.Id,
                                Version = package.ResolvedVersion,
                                License = await _nugetLicense.GetNuGetLicenseAsync(package.Id, package.ResolvedVersion),
                                IsTransitivePackage = false,
                            }
                        };
                        licenses.Add(licenseInfo);
                    }
                }

                // Process transitive packages
                if (framework.TransitivePackages != null)
                {
                    var semaphore = new SemaphoreSlim(3); // Limit concurrent requests
                    var tasks = framework.TransitivePackages.Select(async package =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var licenseInfo = new DotnetLicense
                            {
                                Path = projectName,
                                LicenseInfo = new PackageInfo
                                {
                                    Id = package.Id,
                                    Version = package.ResolvedVersion,
                                    License = await _nugetLicense.GetNuGetLicenseAsync(package.Id, package.ResolvedVersion),
                                    IsTransitivePackage = true,
                                }
                            };
                            return licenseInfo;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    var transitiveResults = await Task.WhenAll(tasks);
                    licenses.AddRange(transitiveResults);
                }
            }
        }

        // Update UI on main thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var license in licenses)
            {
                _allPackages.Add(license);
            }

            var uniqueLicenses = licenses
                .Select(x => x.LicenseInfo)
                .DistinctBy(x => x.Id + x.Version)
                .OrderBy(x => x.Id);

            foreach (var license in uniqueLicenses)
            {
                _uniquePackages.Add(license);
            }
        });
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Title = "Export License Information",
            Filter = "Markdown Files (*.md)|*.md|CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "md",
            FileName = "package-licenses"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                ExportToFile(saveFileDialog.FileName);
                ShowMessage($"License information exported to:\n{saveFileDialog.FileName}", "Export Complete", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to export file:\n{ex.Message}", "Export Error", MessageBoxImage.Error);
            }
        }
    }

    private void ExportToFile(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        StringBuilder content = new();

        switch (extension)
        {
            case ".md":
                ExportMarkdown(content);
                break;
            case ".csv":
                ExportCsv(content);
                break;
            default:
                ExportText(content);
                break;
        }

        File.WriteAllText(fileName, content.ToString(), Encoding.UTF8);
    }

    private void ExportMarkdown(StringBuilder content)
    {
        content.AppendLine("# NuGet Package License Report");
        content.AppendLine();
        content.AppendLine("## All Packages with Project Paths");
        content.AppendLine();
        content.AppendLine("| Project Path | Package ID | Version | License | Is Transitive |");
        content.AppendLine("| ------------ | ---------- | ------- | ------- | ------------- |");

        foreach (var license in _allPackages)
        {
            content.AppendLine($"| {license.Path} | {license.LicenseInfo.Id} | {license.LicenseInfo.Version} | {license.LicenseInfo.License} | {license.LicenseInfo.IsTransitivePackage} |");
        }

        content.AppendLine();
        content.AppendLine("## Unique Packages");
        content.AppendLine();
        content.AppendLine("| Package ID | Version | License |");
        content.AppendLine("| ---------- | ------- | ------- |");

        foreach (var license in _uniquePackages)
        {
            content.AppendLine($"| {license.Id} | {license.Version} | {license.License} |");
        }
    }

    private void ExportCsv(StringBuilder content)
    {
        content.AppendLine("Project Path,Package ID,Version,License,Is Transitive");
        foreach (var license in _allPackages)
        {
            content.AppendLine($"\"{license.Path}\",\"{license.LicenseInfo.Id}\",\"{license.LicenseInfo.Version}\",\"{license.LicenseInfo.License}\",{license.LicenseInfo.IsTransitivePackage}");
        }
    }

    private void ExportText(StringBuilder content)
    {
        content.AppendLine("NuGet Package License Report");
        content.AppendLine("==============================");
        content.AppendLine();

        foreach (var license in _allPackages)
        {
            content.AppendLine($"Project: {license.Path}");
            content.AppendLine($"Package: {license.LicenseInfo.Id} v{license.LicenseInfo.Version}");
            content.AppendLine($"License: {license.LicenseInfo.License}");
            content.AppendLine($"Transitive: {license.LicenseInfo.IsTransitivePackage}");
            content.AppendLine();
        }
    }

    private void SetAnalyzing(bool isAnalyzing)
    {
        AnalyzeButton.IsEnabled = !isAnalyzing;
        BrowseButton.IsEnabled = !isAnalyzing;
        ExportButton.IsEnabled = !isAnalyzing && _allPackages.Count > 0;
        AnalysisProgressBar.Visibility = isAnalyzing ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ShowMessage(string message, string title, MessageBoxImage icon)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }
}
