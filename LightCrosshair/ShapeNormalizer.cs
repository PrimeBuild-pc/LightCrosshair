using System;
using System.Collections.Generic;

namespace LightCrosshair
{
    public static class ShapeNormalizer
    {
        private static readonly Dictionary<string, CrosshairShape> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dot"] = CrosshairShape.Dot,
            ["cross"] = CrosshairShape.Cross,
            ["cross_out"] = CrosshairShape.CrossOutlined,
            ["crossoutlined"] = CrosshairShape.CrossOutlined,
            ["circle"] = CrosshairShape.Circle,
            ["circle_out"] = CrosshairShape.CircleOutlined,
            ["t"] = CrosshairShape.T,
            ["x"] = CrosshairShape.X,
            ["box"] = CrosshairShape.Box,
            ["gapcross"] = CrosshairShape.GapCross,
            ["plus"] = CrosshairShape.GapCross,
            ["custom"] = CrosshairShape.Custom
        };

        public static CrosshairShape ToEnum(string s) => Map.TryGetValue(s, out var e) ? e : CrosshairShape.Cross;
    }
}
