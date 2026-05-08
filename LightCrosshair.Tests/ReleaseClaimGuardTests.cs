using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace LightCrosshair.Tests
{
    public class ReleaseClaimGuardTests
    {
        [Fact]
        public void PublicDocs_DoNotAdvertise_LiveUnreleasedInstallCommands()
        {
            foreach ((string name, string content) in ReadPublicReleaseDocs())
            {
                AssertNoLiveInstallCommand(name, content, "choco install lightcrosshair");
                AssertNoLiveInstallCommand(name, content, "winget install PrimeBuild.LightCrosshair");
                AssertNoLiveInstallCommand(name, content, "irm https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1 | iex");
            }

        }

        [Fact]
        public void PublicRuntimeClaims_DoNotAdvertise_PresentMon_AsImplementedBackend()
        {
            string claims = CombineFiles(
                "README.md",
                Path.Combine("setup", "chocolatey", "LightCrosshair.nuspec"),
                Path.Combine("docs", "SPECIALK_COMPONENTS_MAPPING.md"));

            Assert.DoesNotContain("ETW/PresentMon", claims, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ETW/PresentMon-style", claims, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PresentMon runtime support", claims, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PresentMon backend", claims, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ETW-style present telemetry", claims, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("optional RTSS fallback", claims, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PresentMon", claims, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("future", claims, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PackagingDocs_Match_FrameworkDependent_DefaultRuntime()
        {
            string buildScript = ReadRepoFile("scripts", "build-release.ps1");
            string packagingDocs = CombineFiles(
                "README.md",
                Path.Combine("setup", "RELEASE_PREP_1.5.0.md"),
                Path.Combine("setup", "LightCrosshair.iss"));
            string nuspec = ReadRepoFile("setup", "chocolatey", "LightCrosshair.nuspec");

            Assert.Matches(new Regex(@"\$selfContainedValue\s*=\s*if\s*\(\s*\$SelfContained\s*\)\s*\{\s*""true""\s*\}\s*else\s*\{\s*""false""\s*\}", RegexOptions.IgnoreCase), buildScript);
            Assert.Contains("--self-contained", buildScript, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("framework-dependent", packagingDocs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".NET 8", packagingDocs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Windows Desktop Runtime", packagingDocs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".NET 8", nuspec, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Windows Desktop Runtime", nuspec, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet-8.0-desktopruntime", nuspec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FrameGenerationUi_Uses_HeuristicEstimate_Wording()
        {
            string xaml = ReadRepoFile("LightCrosshair", "SettingsWindow.xaml");

            Assert.DoesNotContain("Show Generated Frames", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Show frame-generation estimate", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("heuristic", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("suspicion", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("verified provider signal", xaml, StringComparison.OrdinalIgnoreCase);
        }

        private static (string Name, string Content)[] ReadPublicReleaseDocs() =>
            new[]
            {
                ("README.md", ReadRepoFile("README.md")),
                ("setup/RELEASE_PREP_1.5.0.md", ReadRepoFile("setup", "RELEASE_PREP_1.5.0.md")),
                ("setup/chocolatey/LightCrosshair.nuspec", ReadRepoFile("setup", "chocolatey", "LightCrosshair.nuspec")),
            };

        private static void AssertNoLiveInstallCommand(string name, string content, string command)
        {
            Assert.False(
                content.Contains(command, StringComparison.OrdinalIgnoreCase),
                $"{name} must not advertise live install command before final publication: {command}");
        }

        private static string CombineFiles(params string[] relativePaths) =>
            string.Join(
                Environment.NewLine,
                relativePaths.Select(path => ReadRepoFile(path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))));

        private static string ReadRepoFile(params string[] relativeParts)
        {
            string root = FindRepoRoot();
            string path = Path.Combine(new[] { root }.Concat(relativeParts).ToArray());
            return File.ReadAllText(path);
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "LightCrosshair.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find LightCrosshair repository root.");
        }
    }
}
