using System;
using System.Drawing;

namespace LightCrosshair
{
    public enum CrosshairVisibilityPresetKind
    {
        NeonCyan = 0,
        Lime = 1,
        Magenta = 2,
        Yellow = 3
    }

    internal static class CrosshairVisibilityPreset
    {
        public static void Apply(CrosshairProfile profile, CrosshairVisibilityPresetKind kind)
        {
            ArgumentNullException.ThrowIfNull(profile);

            Color main = kind switch
            {
                CrosshairVisibilityPresetKind.Lime => Color.Lime,
                CrosshairVisibilityPresetKind.Magenta => Color.Magenta,
                CrosshairVisibilityPresetKind.Yellow => Color.Yellow,
                _ => Color.Cyan
            };

            profile.OuterColor = Color.FromArgb(255, main.R, main.G, main.B);
            profile.InnerColor = profile.OuterColor;
            profile.EdgeColor = Color.Black;
            profile.InnerShapeColor = Color.Black;
            profile.OutlineEnabled = true;
        }
    }
}
