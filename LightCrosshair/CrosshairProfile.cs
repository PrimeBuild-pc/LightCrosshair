using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LightCrosshair
{
    public class CrosshairProfile
    {
        // Profile name
        public string Name { get; set; } = "Default";

        // Shape properties
        public string Shape { get; set; } = "Cross";

        // Primary shape properties (outer shape in combined shapes)
        public int Size { get; set; } = 20;
        public int Thickness { get; set; } = 2;
        public int GapSize { get; set; } = 4; // For Plus shape

        // Secondary shape properties (inner shape in combined shapes)
        public int InnerSize { get; set; } = 10;
        public int InnerThickness { get; set; } = 2;
        public int InnerGapSize { get; set; } = 4; // For Plus shape when used as inner shape

        // These properties will be used instead of the ones below for backward compatibility

        // Color properties for secondary shape
        [JsonIgnore]
        public Color InnerShapeEdgeColor { get; set; } = Color.Red;

        [JsonConverter(typeof(ColorJsonConverter))]
        public string InnerShapeEdgeColorJson
        {
            get => ColorToJson(InnerShapeEdgeColor);
            set => InnerShapeEdgeColor = JsonToColor(value);
        }

        [JsonIgnore]
        public Color InnerShapeInnerColor { get; set; } = Color.White;

        [JsonConverter(typeof(ColorJsonConverter))]
        public string InnerShapeInnerColorJson
        {
            get => ColorToJson(InnerShapeInnerColor);
            set => InnerShapeInnerColor = JsonToColor(value);
        }

        // Color properties
        [JsonIgnore]
        public Color EdgeColor { get; set; } = Color.Red;

        [JsonPropertyName("EdgeColorSerialized")]
        public string EdgeColorSerialized
        {
            get => $"{EdgeColor.R},{EdgeColor.G},{EdgeColor.B}";
            set
            {
                try
                {
                    var parts = value.Split(',');
                    if (parts.Length == 3 &&
                        int.TryParse(parts[0], out int r) &&
                        int.TryParse(parts[1], out int g) &&
                        int.TryParse(parts[2], out int b))
                    {
                        EdgeColor = Color.FromArgb(r, g, b);
                    }
                }
                catch
                {
                    EdgeColor = Color.Red;
                }
            }
        }

        [JsonIgnore]
        public Color InnerColor { get; set; } = Color.Orange;

        [JsonPropertyName("InnerColorSerialized")]
        public string InnerColorSerialized
        {
            get => $"{InnerColor.R},{InnerColor.G},{InnerColor.B}";
            set
            {
                try
                {
                    var parts = value.Split(',');
                    if (parts.Length == 3 &&
                        int.TryParse(parts[0], out int r) &&
                        int.TryParse(parts[1], out int g) &&
                        int.TryParse(parts[2], out int b))
                    {
                        InnerColor = Color.FromArgb(r, g, b);
                    }
                }
                catch
                {
                    InnerColor = Color.Orange;
                }
            }
        }

        [JsonIgnore]
        public Color FillColor { get; set; } = Color.Transparent;

        [JsonPropertyName("FillColorSerialized")]
        public string FillColorSerialized
        {
            get => $"{FillColor.R},{FillColor.G},{FillColor.B},{FillColor.A}";
            set
            {
                try
                {
                    var parts = value.Split(',');
                    if (parts.Length == 4 &&
                        int.TryParse(parts[0], out int r) &&
                        int.TryParse(parts[1], out int g) &&
                        int.TryParse(parts[2], out int b) &&
                        int.TryParse(parts[3], out int a))
                    {
                        FillColor = Color.FromArgb(a, r, g, b);
                    }
                }
                catch
                {
                    FillColor = Color.Transparent;
                }
            }
        }

        // Hotkey
        public Keys HotKey { get; set; } = Keys.None;

        // Static methods for profile management
        private static readonly string ProfilesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LightCrosshair", "Profiles");

        public static List<CrosshairProfile> LoadProfiles()
        {
            var profiles = new List<CrosshairProfile>();

            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(ProfilesDirectory))
                {
                    Directory.CreateDirectory(ProfilesDirectory);

                    // Create default profile
                    var defaultProfile = new CrosshairProfile();
                    defaultProfile.Save();
                    profiles.Add(defaultProfile);
                    return profiles;
                }

                // Load all profile files
                foreach (var file in Directory.GetFiles(ProfilesDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var profile = JsonSerializer.Deserialize<CrosshairProfile>(json);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch
                    {
                        // Skip invalid profiles
                    }
                }

                // If no profiles were loaded, create a default one
                if (profiles.Count == 0)
                {
                    var defaultProfile = new CrosshairProfile();
                    defaultProfile.Save();
                    profiles.Add(defaultProfile);
                }
            }
            catch
            {
                // If loading fails, return a default profile
                profiles.Add(new CrosshairProfile());
            }

            return profiles;
        }

        public void Save()
        {
            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(ProfilesDirectory))
                {
                    Directory.CreateDirectory(ProfilesDirectory);
                }

                // Sanitize name for filename
                string safeName = string.Join("_", Name.Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = "Profile_" + Guid.NewGuid().ToString().Substring(0, 8);
                }

                // Save profile to file
                string filePath = Path.Combine(ProfilesDirectory, safeName + ".json");
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Silently fail if saving fails
            }
        }

        public static bool DeleteProfile(string name)
        {
            try
            {
                string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(ProfilesDirectory, safeName + ".json");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
            }
            catch
            {
                // Silently fail if deletion fails
            }

            return false;
        }

        // Clone method for creating copies
        public CrosshairProfile Clone()
        {
            return new CrosshairProfile
            {
                Name = this.Name,
                Shape = this.Shape,
                Size = this.Size,
                InnerSize = this.InnerSize,
                Thickness = this.Thickness,
                InnerThickness = this.InnerThickness,
                GapSize = this.GapSize,
                InnerGapSize = this.InnerGapSize,
                EdgeColor = this.EdgeColor,
                InnerColor = this.InnerColor,
                FillColor = this.FillColor,
                InnerShapeEdgeColor = this.InnerShapeEdgeColor,
                InnerShapeInnerColor = this.InnerShapeInnerColor,
                HotKey = this.HotKey
            };
        }

        // Helper methods for color serialization
        private string ColorToJson(Color color)
        {
            return $"{color.A},{color.R},{color.G},{color.B}";
        }

        private Color JsonToColor(string json)
        {
            try
            {
                var parts = json.Split(',');
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out int a) &&
                    int.TryParse(parts[1], out int r) &&
                    int.TryParse(parts[2], out int g) &&
                    int.TryParse(parts[3], out int b))
                {
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // Fallback to default color on error
            }

            return Color.Red; // Default color
        }
    }
}
