using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using LightCrosshair;

namespace LightCrosshair.Tests
{
    public class ProfileStoreTests
    {
        [Fact]
        public async Task SaveAndLoad_WithBackupRotation_RecoversFromCorruption()
        {
            string dir = Path.Combine(Path.GetTempPath(), "LC_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "profiles.json");

            try
            {
                var listA = new List<CrosshairProfile>
                {
                    new CrosshairProfile { Name = "A", EnumShape = CrosshairShape.Cross, EdgeColor = Color.Lime, InnerColor = Color.Cyan }
                };
                await ProfileStore.SaveAtomicAsync(listA, path);
                Assert.True(File.Exists(path));

                var listB = new List<CrosshairProfile>
                {
                    new CrosshairProfile { Name = "B", EnumShape = CrosshairShape.Circle, EdgeColor = Color.Red, InnerColor = Color.Yellow }
                };
                await ProfileStore.SaveAtomicAsync(listB, path);
                Assert.True(File.Exists(path + ".bak1"));

                // Corrupt main file
                File.WriteAllText(path, "{ invalid json");

                var loaded = await ProfileStore.LoadAsync(path);
                Assert.NotNull(loaded);
                Assert.Single(loaded);
                Assert.True(loaded[0].Name == "A" || loaded[0].Name == "B"); // recovered from a backup successfully
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}

