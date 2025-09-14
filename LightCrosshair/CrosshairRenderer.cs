using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;

namespace LightCrosshair
{

    internal sealed class CrosshairRenderer : IDisposable
    {
        private readonly object _sync = new();
    private Bitmap? _cachedBitmap;
    private string _lastGeometryHash = string.Empty;
    private string _lastColorHash = string.Empty;
    private GeometryPlan? _plan;

    private bool _antiAlias = true;
    // Changing anti-alias does not change geometry; force color/style re-render only
    public bool AntiAlias { get => _antiAlias; set { if (_antiAlias != value) { _antiAlias = value; _lastColorHash = string.Empty; } } }

        // Resource caches
        private readonly Dictionary<(Color,int), Pen> _penCache = new();
        private readonly Dictionary<Color, SolidBrush> _brushCache = new();

        #region Public API
    public Bitmap RenderIfNeeded(CrosshairProfile cfg)
        {
            lock (_sync)
            {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
                string geomHash = ComputeGeometryHash(cfg);
                string colorHash = ComputeColorHash(cfg);

                bool geometryChanged = _cachedBitmap == null || geomHash != _lastGeometryHash;
                bool colorChanged = geometryChanged || colorHash != _lastColorHash;

                if (geometryChanged)
                {
                    _plan = BuildGeometryPlan(cfg);
                }
                if (colorChanged && _plan != null)
                {
                    try
                    {
                        RegenerateBitmap(cfg, _plan);
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(ex, nameof(CrosshairRenderer) + ".RegenerateBitmap");
                        throw;
                    }
                    _lastGeometryHash = geomHash;
                    _lastColorHash = colorHash;
                }
                // Return a clone so UI owns its copy and renderer can freely regenerate/dispose the cache.
                return (Bitmap)_cachedBitmap!.Clone();
            }
        }

        public void Invalidate()
        {
            lock (_sync)
            {
                _lastGeometryHash = string.Empty;
                _lastColorHash = string.Empty;
                _plan = null;
                _cachedBitmap?.Dispose();
                _cachedBitmap = null;
                foreach (var p in _penCache.Values) p.Dispose();
                foreach (var b in _brushCache.Values) b.Dispose();
                _penCache.Clear();
                _brushCache.Clear();
            }
        }
        #endregion

        #region Geometry Planning
        private sealed record GeometryPlan(
            int CanvasSize,
            List<Line> EdgeLines,
            List<Line> InnerLines,
            List<Ellipse> EdgeEllipses,
            List<Ellipse> InnerEllipses,
            List<RectangleF> EdgeRects,
            List<RectangleF> FillRects,
            List<FilledCircle> FilledCircles,
            CrosshairShape ShapeKind
        );

        private readonly struct Line { public readonly float X1,Y1,X2,Y2; public Line(float x1,float y1,float x2,float y2){X1=x1;Y1=y1;X2=x2;Y2=y2;} }
        private readonly struct Ellipse { public readonly float X,Y,W,H; public Ellipse(float x,float y,float w,float h){X=x;Y=y;W=w;H=h;} }
        private readonly struct FilledCircle { public readonly float X,Y,D; public FilledCircle(float x,float y,float d){X=x;Y=y;D=d;} }

        private GeometryPlan BuildGeometryPlan(CrosshairProfile cfg)
        {
            // Determine canvas side (odd for perfect center alignment)
            int max = Math.Max(cfg.Size, cfg.InnerSize);
            int thicknessMax = Math.Max(Math.Max(cfg.Thickness, cfg.InnerThickness), cfg.EdgeThickness);
            int padding = thicknessMax * 2 + 16;
            int side = Math.Max(64, max + padding);
            if (side % 2 == 0) side++;

            float cx = side / 2f;
            float cy = side / 2f;
            float size = cfg.Size / 2f;

            var shapeKind = cfg.EnumShape; // use canonical enum only
            var edgeLines = new List<Line>();
            var innerLines = new List<Line>();
            var edgeEllipses = new List<Ellipse>();
            var innerEllipses = new List<Ellipse>();
            var edgeRects = new List<RectangleF>();
            var fillRects = new List<RectangleF>();
            var filledCircles = new List<FilledCircle>();

            void AddCross(bool outlined, bool gap)
            {
                float gapSize = gap ? cfg.GapSize : 0;
                // Horizontal
                edgeLines.Add(new Line(cx - size, cy, cx - gapSize, cy));
                edgeLines.Add(new Line(cx + gapSize, cy, cx + size, cy));
                // Vertical
                edgeLines.Add(new Line(cx, cy - size, cx, cy - gapSize));
                edgeLines.Add(new Line(cx, cy + gapSize, cx, cy + size));
                if (outlined && cfg.Thickness > 2)
                {
                    float shrink = 1f;
                    innerLines.Add(new Line(cx - size + shrink, cy, cx - gapSize, cy));
                    innerLines.Add(new Line(cx + gapSize, cy, cx + size - shrink, cy));
                    innerLines.Add(new Line(cx, cy - size + shrink, cx, cy - gapSize));
                    innerLines.Add(new Line(cx, cy + gapSize, cx, cy + size - shrink));
                }
            }

            switch (shapeKind)
            {
                case CrosshairShape.Dot:
                    filledCircles.Add(new FilledCircle(cx - size, cy - size, size * 2));
                    break;
                case CrosshairShape.Cross:
                    AddCross(outlined:false, gap: cfg.GapSize > 0);
                    break;
                case CrosshairShape.CrossOutlined:
                    AddCross(outlined:true, gap: cfg.GapSize > 0);
                    break;
                case CrosshairShape.GapCross:
                    AddCross(outlined:true, gap:true);
                    break;
                case CrosshairShape.Circle:
                    edgeEllipses.Add(new Ellipse(cx - size, cy - size, size * 2, size * 2));
                    break;
                case CrosshairShape.CircleOutlined:
                    edgeEllipses.Add(new Ellipse(cx - size, cy - size, size * 2, size * 2));
                    float innerSize = size - cfg.Thickness;
                    if (innerSize > 0)
                        innerEllipses.Add(new Ellipse(cx - innerSize, cy - innerSize, innerSize * 2, innerSize * 2));
                    break;
                case CrosshairShape.T:
                    // Horizontal full line, vertical only downward
                    edgeLines.Add(new Line(cx - size, cy, cx + size, cy));
                    edgeLines.Add(new Line(cx, cy, cx, cy + size));
                    if (cfg.Thickness > 2)
                    {
                        innerLines.Add(new Line(cx - size + 1, cy, cx + size - 1, cy));
                        innerLines.Add(new Line(cx, cy + 1, cx, cy + size - 1));
                    }
                    break;
                case CrosshairShape.X:
                    edgeLines.Add(new Line(cx - size, cy - size, cx + size, cy + size));
                    edgeLines.Add(new Line(cx - size, cy + size, cx + size, cy - size));
                    if (cfg.Thickness > 2)
                    {
                        double off = cfg.Thickness / 2.0 * 0.707; // ~sqrt(2)/2
                        int o = (int)Math.Ceiling(off);
                        innerLines.Add(new Line(cx - size + o, cy - size + o, cx + size - o, cy + size - o));
                        innerLines.Add(new Line(cx - size + o, cy + size - o, cx + size - o, cy - size + o));
                    }
                    break;
                case CrosshairShape.Box:
                    float half = size;
                    edgeRects.Add(new RectangleF(cx - half, cy - half, half * 2, half * 2)); // Outline rectangle
                    // If inner thickness specified emulate inner outline by inset
                    if (cfg.Thickness > 2)
                    {
                        float inset = cfg.Thickness;
                        innerEllipses.Add(new Ellipse(cx - (half - inset), cy - (half - inset), (half - inset)*2, (half - inset)*2));
                    }
                    break;
                case CrosshairShape.Custom:
                    // Treat 'Custom' as legacy composite fallback – approximate with simple cross + dot center.
                    edgeLines.Add(new Line(cx - size, cy, cx + size, cy));
                    edgeLines.Add(new Line(cx, cy - size, cx, cy + size));
                    float dotR = Math.Max(2, cfg.InnerSize / 4f);
                    filledCircles.Add(new FilledCircle(cx - dotR, cy - dotR, dotR * 2));
                    break;
            }

            return new GeometryPlan(side, edgeLines, innerLines, edgeEllipses, innerEllipses, edgeRects, fillRects, filledCircles, shapeKind);
        }

        #endregion

        #region Rendering
        private void RegenerateBitmap(CrosshairProfile cfg, GeometryPlan plan)
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = new Bitmap(plan.CanvasSize, plan.CanvasSize);
            using var g = Graphics.FromImage(_cachedBitmap);
            g.Clear(Color.Transparent);
            g.SmoothingMode = _antiAlias ? SmoothingMode.AntiAlias : SmoothingMode.None;
            g.PixelOffsetMode = _antiAlias ? PixelOffsetMode.HighQuality : PixelOffsetMode.None;
            g.CompositingMode = CompositingMode.SourceOver;

            // Pens / brushes (lazy)
            int primaryWidth = Math.Max(1, cfg.Thickness);
            // Prefer EdgeColor if provided; fallback to OuterColor for backward compat
            Color strokeColor = cfg.EdgeColor.A > 0 ? cfg.EdgeColor : cfg.OuterColor;
            Pen? edgePen = GetPen(strokeColor, primaryWidth);
            Pen? innerPen = cfg.Thickness > 2 ? GetPen(strokeColor, Math.Max(1, cfg.Thickness - 2)) : null;
            // Inner/composite colors
            Pen? innerShapePen = GetPen(cfg.InnerShapeColor, cfg.InnerThickness);
            // Fill prefers explicit FillColor, otherwise (legacy) InnerColor, else OuterColor
            Brush? fillBrush = GetBrush(cfg.FillColor);
            Color dotColor = cfg.InnerColor.A > 0 ? cfg.InnerColor : strokeColor;
            Brush? dotBrush = GetBrush(dotColor); // fallback for dot/single-color cases

            // Composite handling: draw custom composite from cfg rather than plan
            if (plan.ShapeKind == CrosshairShape.Custom)
            {
                float cx = plan.CanvasSize / 2f;
                float cy = cx;
                float outerR = cfg.Size / 2f;
                float innerR = Math.Max(0, cfg.InnerSize / 2f);

                // For composite lines we need precise ends that don't bleed into the gap.
                // Use FLAT caps (not round) to avoid overlapping the center region.
                Pen MakeFlatPen(Color color, int width)
                {
                    var p = new Pen(color, width)
                    {
                        StartCap = LineCap.Flat,
                        EndCap = LineCap.Flat,
                        LineJoin = LineJoin.Miter
                    };
                    return p;
                }

                void DrawCross(float radius, Color color, int width, int gap)
                {
                    // Simple single-pen cross with center gap to preserve visibility
                    using var pen = MakeFlatPen(color, Math.Max(1, width));
                    // Horizontal
                    g.DrawLine(pen, cx - radius, cy, cx - gap, cy);
                    g.DrawLine(pen, cx + gap, cy, cx + radius, cy);
                    // Vertical
                    g.DrawLine(pen, cx, cy - radius, cx, cy - gap);
                    g.DrawLine(pen, cx, cy + gap, cx, cy + radius);
                }

                void DrawX(float radius, Color color, int width, int gap)
                {
                    // Diagonals with center gap
                    float d = gap * 0.70710678f; // ~ 1/sqrt(2) for equal x/y offsets
                    using var pen = MakeFlatPen(color, Math.Max(1, width));
                    // Top-left to bottom-right
                    g.DrawLine(pen, cx - radius, cy - radius, cx - d, cy - d);
                    g.DrawLine(pen, cx + d, cy + d, cx + radius, cy + radius);
                    // Bottom-left to top-right
                    g.DrawLine(pen, cx - radius, cy + radius, cx - d, cy + d);
                    g.DrawLine(pen, cx + d, cy - d, cx + radius, cy - radius);
                }

                // Outer component
                if (cfg.Shape == "CrossDot")
                {
                    int width = Math.Max(1, cfg.Thickness);
                    int minForCaps = (int)Math.Ceiling(width / 2f);
                    int gap = Math.Max(cfg.GapSize, minForCaps);
                    // Prefer EdgeColor for strokes
                    var crossColor = cfg.EdgeColor.A > 0 ? cfg.EdgeColor : cfg.OuterColor;
                    DrawCross(outerR, crossColor, width, gap);
                }
                else if (cfg.Shape.StartsWith("Circle", StringComparison.OrdinalIgnoreCase))
                {
                    // Outer circle: outline only
                    var outerPenSingle = GetPen(cfg.EdgeColor.A > 0 ? cfg.EdgeColor : cfg.OuterColor, Math.Max(1, cfg.Thickness));
                    if (outerPenSingle != null)
                        g.DrawEllipse(outerPenSingle, cx - outerR, cy - outerR, outerR * 2, outerR * 2);
                }

                // Inner component
                string innerKind = cfg.Shape == "CrossDot" ? "Dot" : cfg.Shape.Replace("Circle", string.Empty);
                switch (innerKind)
                {
                    case "Dot":
                    {
                        var brush = GetBrush(cfg.InnerShapeColor);
                        if (brush != null)
                            g.FillEllipse(brush, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
                        break;
                    }
                    // "Plus" removed; treat as Cross for backward compatibility
                    case "Plus":
                    case "Cross":
                    {
                        int width = Math.Max(1, cfg.InnerThickness);
                        int minForCaps = (int)Math.Ceiling(width / 2f);
                        int gap = Math.Max(cfg.InnerGapSize, minForCaps);
                        DrawCross(innerR, cfg.InnerShapeColor, width, gap);
                        break;
                    }
                    case "X":
                    {
                        int width = Math.Max(1, cfg.InnerThickness);
                        int minForCaps = (int)Math.Ceiling(width / 2f);
                        int gap = Math.Max(cfg.InnerGapSize, minForCaps);
                        DrawX(innerR, cfg.InnerShapeColor, width, gap);
                        break;
                    }
                }
            }
            else
            {
                // Draw primary geometry from plan
                if (edgePen != null)
                {
                    foreach (var l in plan.EdgeLines)
                        g.DrawLine(edgePen, l.X1, l.Y1, l.X2, l.Y2);
                    foreach (var e in plan.EdgeEllipses)
                        g.DrawEllipse(edgePen, e.X, e.Y, e.W, e.H);
                    foreach (var r in plan.EdgeRects)
                        g.DrawRectangle(edgePen, r.X, r.Y, r.Width, r.Height);
                }

                if (innerPen != null)
                {
                    foreach (var l in plan.InnerLines)
                        g.DrawLine(innerPen, l.X1, l.Y1, l.X2, l.Y2);
                    foreach (var e in plan.InnerEllipses)
                        g.DrawEllipse(innerPen, e.X, e.Y, e.W, e.H);
                }

                // Filled circles (used for dot / composite)
                foreach (var fc in plan.FilledCircles)
                {
                    var brush = fillBrush ?? dotBrush;
                    if (brush != null)
                        g.FillEllipse(brush, fc.X, fc.Y, fc.D, fc.D);
                }
            }
        }
        #endregion

        #region Helpers
        private Pen? GetPen(Color c, int width)
        {
            if (c.A == 0 || width <= 0) return null;
            var key = (c, width);
            if (!_penCache.TryGetValue(key, out var pen))
            {
                pen = new Pen(c, width)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                _penCache[key] = pen;
            }
            return pen;
        }

        private SolidBrush? GetBrush(Color c)
        {
            if (c.A == 0) return null;
            if (!_brushCache.TryGetValue(c, out var b))
            {
                b = new SolidBrush(c);
                _brushCache[c] = b;
            }
            return b;
        }

        private string ComputeGeometryHash(CrosshairProfile p)
        {
            var sb = new StringBuilder();
            sb.Append(p.EnumShape).Append('|')
              .Append(p.Shape).Append('|')
              .Append(p.Size).Append('|')
              .Append(p.Thickness).Append('|')
              .Append(p.EdgeThickness).Append('|')
              .Append(p.GapSize).Append('|')
              .Append(p.InnerSize).Append('|')
              .Append(p.InnerThickness).Append('|')
              .Append(p.InnerGapSize);
            using var sha = SHA1.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

                private string ComputeColorHash(CrosshairProfile p)
                {
                        var sb = new StringBuilder();
                        sb.Append(p.EdgeColor.ToArgb()).Append('|')
                            .Append(p.InnerColor.ToArgb()).Append('|')
                            .Append(p.OuterColor.ToArgb()).Append('|')
                            .Append(p.FillColor.ToArgb()).Append('|')
                            .Append(p.InnerShapeColor.ToArgb()).Append('|')
                            .Append(p.InnerShapeFillColor.ToArgb()).Append('|')
                            .Append(_antiAlias ? '1' : '0');
                        using var sha = SHA1.Create();
                        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
                }
        #endregion

        public void Dispose() => Invalidate();
    }
}

