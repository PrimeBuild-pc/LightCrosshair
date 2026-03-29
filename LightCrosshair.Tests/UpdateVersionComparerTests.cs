using Xunit;

namespace LightCrosshair.Tests
{
    public class UpdateVersionComparerTests
    {
        [Theory]
        [InlineData("v1.2.3", "1.2.3")]
        [InlineData("1.2.3-beta", "1.2.3")]
        [InlineData("V2.0.0+build5", "2.0.0")]
        [InlineData("  v3.4.5-rc.1  ", "3.4.5")]
        public void NormalizeVersionTag_Strips_Prefix_And_Suffix(string input, string expected)
        {
            string normalized = UpdateVersionComparer.NormalizeVersionTag(input);
            Assert.Equal(expected, normalized);
        }

        [Fact]
        public void CompareVersions_Handles_Prerelease_Tag()
        {
            int? cmp = UpdateVersionComparer.CompareVersions("1.2.3-beta", "1.2.2");
            Assert.True(cmp.HasValue);
            Assert.True(cmp.Value > 0);
        }

        [Fact]
        public void CompareVersions_Returns_Null_For_Invalid()
        {
            int? cmp = UpdateVersionComparer.CompareVersions("not-a-version", "1.2.3");
            Assert.False(cmp.HasValue);
        }

        [Fact]
        public void CompareVersions_Detects_Local_Newer()
        {
            int? cmp = UpdateVersionComparer.CompareVersions("1.1.1", "1.1.2");
            Assert.True(cmp.HasValue);
            Assert.True(cmp.Value < 0);
        }
    }
}
