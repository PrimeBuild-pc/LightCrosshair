using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal static class PresetCatalog
{
    public static IReadOnlyList<PresetEntry> Entries { get; } = Build();
    private static IReadOnlyList<PresetEntry> Build()
    {
        var presets = new List<PresetEntry>();
        Add("Classic Cross", [Arms(8, 2, 4)], "cross", "minimal");
        Add("Micro Dot", [Shape(LayerKind.Dot, 2)], "dot", "minimal");
        Add("Fine Cross", [Arms(6, 1, 3)], "cross", "minimal");
        Add("Compact Cross", [Arms(4, 2, 2)], "cross", "minimal");
        Add("Long Cross", [Arms(16, 1, 5)], "cross", "minimal");
        Add("Bold Cross", [Arms(10, 4, 4, true)], "cross", "outlined", "high-visibility");
        Add("Open Cross", [Arms(6, 2, 10)], "cross", "minimal");
        Add("Center Cross", [Arms(5, 1, 0)], "cross", "minimal");
        Add("Cross and Dot", [Arms(8, 1, 5), Shape(LayerKind.Dot, 3)], "cross", "dot");
        Add("Outlined Cross", [Arms(8, 2, 4, true)], "cross", "outlined");
        Add("Small T", [T(6, 1, 3)], "t", "minimal");
        Add("Wide T", [T(12, 2, 4)], "t");
        Add("T and Dot", [T(8, 2, 5), Shape(LayerKind.Dot, 2)], "t", "dot");
        Add("Fine X", [X(7, 1, 3)], "x", "minimal");
        Add("Open X", [X(10, 2, 7)], "x");
        Add("Outlined X", [X(9, 2, 4, true)], "x", "outlined");
        Add("X and Dot", [X(8, 1, 5), Shape(LayerKind.Dot, 3)], "x", "dot");
        Add("Small Ring", [Shape(LayerKind.Ring, 12)], "circle", "minimal");
        Add("Fine Ring", [Shape(LayerKind.Ring, 24)], "circle", "minimal");
        Add("Ring and Dot", [Shape(LayerKind.Ring, 20), Shape(LayerKind.Dot, 3)], "circle", "dot");
        Add("Double Ring", [Shape(LayerKind.Ring, 26), Shape(LayerKind.Ring, 14)], "circle");
        Add("Ring and Cross", [Shape(LayerKind.Ring, 30), Arms(6, 1, 3)], "circle", "cross");
        Add("Outlined Ring", [Shape(LayerKind.Ring, 22, 2, true)], "circle", "outlined");
        Add("Small Box", [Shape(LayerKind.Box, 12)], "box", "minimal");
        Add("Bracket Box", [Box(26, 1, 12)], "box", "minimal");
        Add("Bold Brackets", [Box(30, 3, 14)], "box", "high-visibility");
        Add("Box and Dot", [Shape(LayerKind.Box, 20), Shape(LayerKind.Dot, 3)], "box", "dot");
        Add("Outlined Dot", [Shape(LayerKind.Dot, 5, 1, true)], "dot", "outlined");
        Add("Large Dot", [Shape(LayerKind.Dot, 8)], "dot", "high-visibility");
        Add("Vertical Guide", [new() { Arms = ArmMask.Top | ArmMask.Bottom, Length = 10, Thickness = 1, Gap = 5 }], "cross", "minimal");
        return presets.AsReadOnly();

        void Add(string name, List<CrosshairLayer> layers, params string[] tags)
        {
            string id = "preset-" + name.ToLowerInvariant().Replace(' ', '-');
            string[] palette = ["#FFFFFF", "#00FF00", "#FFFF00", "#00FFFF", "#FF00FF", "#FF0000"];
            string color = palette[presets.Count % palette.Length];
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].Id = id + "-" + i; layers[i].Name = layers[i].Kind.ToString();
                layers[i].Color = color;
            }
            presets.Add(new() { Design = new() { Id = id, Name = name, AntiAlias = false, SourcePresetId = id, UpdatedAt = default, Layers = layers }, Tags = tags.ToList() });
        }
    }
    private static CrosshairLayer Arms(int length, int thickness, int gap, bool outline = false) => new() { Length = length, Thickness = thickness, Gap = gap, Outline = outline, Color = "#00FFFF" };
    private static CrosshairLayer T(int length, int thickness, int gap) { var l = Arms(length, thickness, gap); l.Arms &= ~ArmMask.Top; return l; }
    private static CrosshairLayer X(int length, int thickness, int gap, bool outline = false) { var l = Arms(length, thickness, gap, outline); l.Rotation = 45; return l; }
    private static CrosshairLayer Shape(LayerKind kind, int length, int thickness = 1, bool outline = false) => new() { Kind = kind, Length = length, Thickness = thickness, Gap = 0, Outline = outline };
    private static CrosshairLayer Box(int length, int thickness, int gap) => new() { Kind = LayerKind.Box, Length = length, Thickness = thickness, Gap = gap };
}
