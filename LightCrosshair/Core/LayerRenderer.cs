using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class LayerRenderer(string imagesDirectory)
{
    // Physical pixel convention: the monitor center pixel is left+width/2, top+height/2.
    // A stroke occupies [-floor((thickness-1)/2), ..., ceil((thickness-1)/2)].
    // Thus odd widths center on that pixel; even widths have one extra pixel down/right.
    // Paths use integer pixel boundaries; quarter-turn arms are permuted before rasterization
    // so rotating an even width never reverses its down/right bias.
    internal static int StrokeStart(int thickness) => -((thickness - 1) / 2);
    public Rectangle Bounds(CrosshairDesign design)
    {
        RectangleF total = RectangleF.Empty;
        foreach (var layer in design.Layers.Where(l => l.Visible && l.Opacity > 0))
        {
            using var path = Geometry(layer, layer.FillColor is not null);
            if (path.PointCount == 0) continue;
            var bounds = path.GetBounds();
            int padding = (layer.Outline ? (int)Math.Ceiling(layer.OutlineThickness * Math.Sqrt(2)) : 0) + (design.AntiAlias ? 1 : 0);
            bounds.Inflate(padding, padding);
            total = total.IsEmpty ? bounds : RectangleF.Union(total, bounds);
        }
        if (total.IsEmpty) return new(0, 0, 1, 1);
        int left = (int)Math.Floor(total.Left), top = (int)Math.Floor(total.Top);
        return Rectangle.FromLTRB(left, top, (int)Math.Ceiling(total.Right), (int)Math.Ceiling(total.Bottom));
    }
    public Bitmap Render(CrosshairDesign design, int canvasSize, string? background = null)
    {
        var bitmap = new Bitmap(canvasSize, canvasSize, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(background is null ? Color.Transparent : ColorTranslator.FromHtml(background));
        Draw(graphics, design, bitmap.Size, new(canvasSize / 2, canvasSize / 2));
        return bitmap;
    }
    public void Draw(Graphics target, CrosshairDesign design, Size canvas, Point origin)
    {
        foreach (var layer in design.Layers.Where(l => l.Visible && l.Opacity > 0))
        {
            // Draw a layer fully opaque before compositing once: translucent outlines cannot
            // darken the fill or double the alpha at the overlap between arms.
            using var raster = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppPArgb);
            bool upright = Math.Abs(layer.Rotation % 90) < .00001 && layer.Kind != LayerKind.Image;
            if (upright) Rasterize(raster, layer, design.AntiAlias, origin);
            else using (var g = Graphics.FromImage(raster))
            {
                g.Clear(Color.Transparent);
                g.TranslateTransform(origin.X, origin.Y);
                bool quarterTurn = Math.Abs(layer.Rotation % 90) < .00001;
                bool roundedArms = design.AntiAlias && layer.Kind == LayerKind.Arms && layer.Thickness > 1;
                g.SmoothingMode = design.AntiAlias && (layer.Kind is LayerKind.Dot or LayerKind.Ring || !quarterTurn || roundedArms) ? SmoothingMode.AntiAlias : SmoothingMode.None;
                // Geometry uses pixel edges (pixel x covers [x, x+1]); Half makes GDI+ agree, so curves and AA stay centered.
                g.PixelOffsetMode = PixelOffsetMode.Half;
                if (layer.Kind == LayerKind.Image) DrawImage(g, layer);
                else
                {
                    using var path = Geometry(layer, smooth: design.AntiAlias);
                    if (layer.FillColor is not null && layer.Kind is LayerKind.Box or LayerKind.Ring)
                    {
                        using var interior = Geometry(layer, true);
                        using var fill = new SolidBrush(ColorTranslator.FromHtml(layer.FillColor));
                        g.FillPath(fill, interior);
                    }
                    if (path.PointCount == 0 && layer.FillColor is null) continue;
                    if (layer.Outline && path.PointCount > 0)
                    {
                        using var pen = new Pen(ColorTranslator.FromHtml(layer.OutlineColor), layer.OutlineThickness * 2) { LineJoin = design.AntiAlias ? LineJoin.Round : LineJoin.Miter };
                        g.DrawPath(pen, path);
                    }
                    using var brush = new SolidBrush(ColorTranslator.FromHtml(layer.Color));
                    if (path.PointCount > 0) g.FillPath(brush, path);
                }
            }
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)layer.Opacity });
            target.DrawImage(raster, new Rectangle(Point.Empty, canvas), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel, attributes);
        }
    }
    // Upright shapes are rasterized here instead of by GDI+, whose curve and antialias rasterization is
    // not mirror-symmetric at these sizes (a 5 px dot or a rounded arm end came out lopsided by a pixel).
    // Each pixel samples a signed distance on a symmetric grid: 1 sample (crisp) or 4x4 (smooth edges).
    private void Rasterize(Bitmap raster, CrosshairLayer layer, bool smooth, Point origin)
    {
        var shape = Shape.For(layer, smooth);
        if (shape.Empty) return;
        float outline = layer.Outline ? layer.OutlineThickness : 0;
        var reach = shape.Bounds; reach.Inflate(outline + 1, outline + 1);
        int x0 = Math.Max(0, (int)Math.Floor(reach.Left) + origin.X + layer.OffsetX), x1 = Math.Min(raster.Width, (int)Math.Ceiling(reach.Right) + origin.X + layer.OffsetX);
        int y0 = Math.Max(0, (int)Math.Floor(reach.Top) + origin.Y + layer.OffsetY), y1 = Math.Min(raster.Height, (int)Math.Ceiling(reach.Bottom) + origin.Y + layer.OffsetY);
        if (x0 >= x1 || y0 >= y1) return;
        Color stroke = ColorTranslator.FromHtml(layer.Color), edge = ColorTranslator.FromHtml(layer.OutlineColor);
        Color? fill = layer.FillColor is not null && layer.Kind is LayerKind.Box or LayerKind.Ring ? ColorTranslator.FromHtml(layer.FillColor) : null;
        int samples = smooth ? 4 : 1;
        var data = raster.LockBits(new Rectangle(0, 0, raster.Width, raster.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var row = new int[x1 - x0];
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int inStroke = 0, inEdge = 0, inFill = 0;
                    // Distances are 1-Lipschitz and the 4x4 samples lie within 0.53 px of the center:
                    // away from every edge, the center alone decides the pixel.
                    int grid = samples;
                    if (grid > 1)
                    {
                        float cx = x - origin.X - layer.OffsetX + .5f, cy = y - origin.Y - layer.OffsetY + .5f;
                        float dc = shape.Distance(cx, cy);
                        bool near = Math.Abs(dc) < .6f || (outline > 0 && Math.Abs(dc - outline) < .6f) || (fill is not null && Math.Abs(shape.InteriorDistance(cx, cy)) < .6f);
                        if (!near) grid = 1;
                    }
                    for (int sy = 0; sy < grid; sy++)
                        for (int sx = 0; sx < grid; sx++)
                        {
                            float px = x - origin.X - layer.OffsetX + (sx + .5f) / grid, py = y - origin.Y - layer.OffsetY + (sy + .5f) / grid;
                            float d = shape.Distance(px, py);
                            if (d <= 0) inStroke++;
                            else if (d <= outline) inEdge++;
                            if (fill is not null && shape.InteriorDistance(px, py) <= 0) inFill++;
                        }
                    float n = grid * grid, a = inStroke / n, dilated = (inStroke + inEdge) / n, f = inFill / n;
                    // Back to front, premultiplied: interior fill, outline (the shape grown by its width), stroke.
                    float r = 0, g = 0, b = 0, alpha = 0;
                    void Over(Color c, float cover) { r = c.R * cover + r * (1 - cover); g = c.G * cover + g * (1 - cover); b = c.B * cover + b * (1 - cover); alpha = cover + alpha * (1 - cover); }
                    if (fill is { } fc && f > 0) Over(fc, f);
                    if (outline > 0 && dilated > 0) Over(edge, dilated);
                    if (a > 0) Over(stroke, a);
                    row[x - x0] = alpha <= 0 ? 0 : (int)Math.Round(alpha * 255) << 24 | (int)Math.Round(r) << 16 | (int)Math.Round(g) << 8 | (int)Math.Round(b);
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, data.Scan0 + y * data.Stride + x0 * 4, row.Length);
            }
        }
        finally { raster.UnlockBits(data); }
    }

    /// <summary>Signed distance (negative inside) of an upright layer, in pixel-edge coordinates relative to its offset.</summary>
    private sealed class Shape
    {
        private readonly List<RectangleF> _rects = [];
        private float _round, _cx, _cy, _outerRadius = -1, _innerRadius = -1;
        private bool _euclid, _ringInterior;
        private RectangleF? _interior;
        internal bool Empty => _rects.Count == 0 && _outerRadius < 0;
        internal RectangleF Bounds { get; private set; }

        internal static Shape For(CrosshairLayer layer, bool smooth)
        {
            var s = new Shape { _euclid = smooth };
            int length = layer.Length, t = layer.Thickness, start = StrokeStart(t), gap = layer.Gap;
            switch (layer.Kind)
            {
                case LayerKind.Arms:
                    int mask = (int)layer.Arms, turns = ((int)Math.Round(layer.Rotation / 90) % 4 + 4) % 4;
                    mask = ((mask << turns) | (mask >> (4 - turns))) & 15;
                    int near = gap + 1 - (t & 1);
                    if ((mask & 1) != 0) s._rects.Add(new(start, 1 - gap - length, t, length));
                    if ((mask & 2) != 0) s._rects.Add(new(near, start, length, t));
                    if ((mask & 4) != 0) s._rects.Add(new(start, near, t, length));
                    if ((mask & 8) != 0) s._rects.Add(new(1 - gap - length, start, length, t));
                    if (smooth && t > 1) s._round = Math.Min(t, length) / 2f;
                    if (smooth && t > 1 && gap == 0 && s._rects.Count > 1) s._rects.Add(new(start, start, t, t));
                    break;
                case LayerKind.Dot:
                case LayerKind.Ring:
                    s._cx = s._cy = StrokeStart(length) + length / 2f;
                    s._outerRadius = length / 2f; s._euclid = true;
                    if (layer.Kind == LayerKind.Ring)
                    {
                        if (length > 2 * t) s._innerRadius = length / 2f - t;
                        s._ringInterior = true;
                    }
                    break;
                case LayerKind.Box:
                    int pos = StrokeStart(length), stroke = Math.Min(t, (length + 1) / 2);
                    s._interior = new RectangleF(pos, pos, length, length);
                    if (gap == 0)
                    {
                        s._rects.Add(new(pos, pos, length, stroke)); s._rects.Add(new(pos, pos + length - stroke, length, stroke));
                        s._rects.Add(new(pos, pos, stroke, length)); s._rects.Add(new(pos + length - stroke, pos, stroke, length));
                    }
                    else
                    {
                        int corner = Math.Max(0, (length - gap) / 2);
                        if (corner > 0)
                        {
                            foreach (int x in new[] { pos, pos + length - corner })
                            { s._rects.Add(new(x, pos, corner, stroke)); s._rects.Add(new(x, pos + length - stroke, corner, stroke)); }
                            foreach (int y in new[] { pos, pos + length - corner })
                            { s._rects.Add(new(pos, y, stroke, corner)); s._rects.Add(new(pos + length - stroke, y, stroke, corner)); }
                        }
                    }
                    break;
            }
            if (s._outerRadius >= 0) s.Bounds = new(s._cx - s._outerRadius, s._cy - s._outerRadius, 2 * s._outerRadius, 2 * s._outerRadius);
            else if (s._rects.Count > 0) s.Bounds = s._rects.Aggregate(RectangleF.Union);
            return s;
        }

        internal float Distance(float x, float y)
        {
            if (_outerRadius >= 0)
            {
                float r = MathF.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy));
                return _innerRadius >= 0 ? Math.Max(r - _outerRadius, _innerRadius - r) : r - _outerRadius;
            }
            float best = float.MaxValue;
            foreach (var rect in _rects) best = Math.Min(best, Box(rect, x, y, _round));
            return best;
        }

        internal float InteriorDistance(float x, float y)
        {
            if (_ringInterior) return MathF.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy)) - _outerRadius;
            return _interior is { } rect ? Box(rect, x, y, 0) : float.MaxValue;
        }

        // Rounded (Euclidean) or square (Chebyshev, like a mitered pen) distance to an axis-aligned box.
        private float Box(RectangleF rect, float x, float y, float round)
        {
            float hx = rect.Width / 2 - round, hy = rect.Height / 2 - round;
            float qx = Math.Abs(x - (rect.X + rect.Width / 2)) - hx, qy = Math.Abs(y - (rect.Y + rect.Height / 2)) - hy;
            if (!_euclid) return Math.Max(qx, qy);
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return MathF.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - round;
        }
    }

    private void DrawImage(Graphics g, CrosshairLayer layer)
    {
        using var image = Image.FromFile(Path.Combine(imagesDirectory, layer.ImageFile!));
        int width = Math.Max(1, (int)Math.Round(image.Width * layer.ImageScale));
        int height = Math.Max(1, (int)Math.Round(image.Height * layer.ImageScale));
        var state = g.Save();
        g.TranslateTransform(layer.OffsetX + .5f, layer.OffsetY + .5f);
        g.RotateTransform((float)layer.Rotation); g.TranslateTransform(-.5f, -.5f);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        var rect = new Rectangle(StrokeStart(width), StrokeStart(height), width, height);
        if (layer.Outline)
        {
            Color c = ColorTranslator.FromHtml(layer.OutlineColor);
            using var attr = new ImageAttributes();
            attr.SetColorMatrix(new ColorMatrix(new[] { new float[5], new float[5], new float[5], new float[] { 0, 0, 0, 1, 0 }, new float[] { c.R / 255f, c.G / 255f, c.B / 255f, 0, 1 } }));
            int radius = layer.OutlineThickness;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x != 0 || y != 0) g.DrawImage(image, new Rectangle(rect.X + x, rect.Y + y, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attr);
        }
        g.DrawImage(image, rect);
        g.Restore(state);
    }
    /// <summary>The shape's true center: a pixel center (.5) for odd strokes/sizes, the pixel corner (1) for even ones.</summary>
    internal static float Center(CrosshairLayer layer) => ((layer.Kind == LayerKind.Arms ? layer.Thickness : layer.Length) & 1) == 1 ? .5f : 1f;
    internal GraphicsPath Geometry(CrosshairLayer layer, bool filled = false, bool smooth = false)
    {
        var path = new GraphicsPath(FillMode.Winding);
        int length = layer.Length, t = layer.Thickness, start = StrokeStart(t), gap = layer.Gap;
        bool quarter = Math.Abs(layer.Rotation % 90) < .00001;
        switch (layer.Kind)
        {
            case LayerKind.Arms:
                int mask = (int)layer.Arms;
                if (quarter)
                {
                    int turns = ((int)Math.Round(layer.Rotation / 90) % 4 + 4) % 4;
                    mask = ((mask << turns) | (mask >> (4 - turns))) & 15;
                }
                // An even stroke is centered on the pixel corner (start + t/2 = 1), so the right/bottom
                // arms start one pixel later to keep every arm the same distance from that center.
                int near = gap + 1 - (t & 1);
                var arms = new List<Rectangle>(4);
                if ((mask & 1) != 0) arms.Add(new Rectangle(start, 1 - gap - length, t, length));
                if ((mask & 2) != 0) arms.Add(new Rectangle(near, start, length, t));
                if ((mask & 4) != 0) arms.Add(new Rectangle(start, near, t, length));
                if ((mask & 8) != 0) arms.Add(new Rectangle(1 - gap - length, start, length, t));
                foreach (var arm in arms)
                {
                    // Smooth edges: rounded arm ends (radius half the stroke), antialiased when drawn.
                    if (smooth && t > 1) path.AddRoundedRectangle(arm, new SizeF(Math.Min(t, length), Math.Min(t, length)));
                    else path.AddRectangle(arm);
                }
                // Touching arms (gap 0) keep a solid center instead of four rounded notches.
                if (smooth && t > 1 && gap == 0 && arms.Count > 1) path.AddRectangle(new Rectangle(start, start, t, t));
                break;
            case LayerKind.Dot: path.AddEllipse(StrokeStart(length), StrokeStart(length), length, length); break;
            case LayerKind.Ring:
                path.FillMode = FillMode.Alternate;
                path.AddEllipse(StrokeStart(length), StrokeStart(length), length, length);
                if (!filled && length > 2 * t) path.AddEllipse(StrokeStart(length) + t, StrokeStart(length) + t, length - 2 * t, length - 2 * t);
                break;
            case LayerKind.Box:
                int pos = StrokeStart(length), stroke = Math.Min(t, (length + 1) / 2);
                if (filled || gap == 0)
                {
                    path.FillMode = FillMode.Alternate;
                    path.AddRectangle(new Rectangle(pos, pos, length, length));
                    if (!filled && length > 2 * stroke) path.AddRectangle(new Rectangle(pos + stroke, pos + stroke, length - 2 * stroke, length - 2 * stroke));
                }
                else
                {
                    int corner = Math.Max(0, (length - gap) / 2);
                    if (corner > 0)
                    {
                        foreach (int x in new[] { pos, pos + length - corner })
                        {
                            path.AddRectangle(new Rectangle(x, pos, corner, stroke));
                            path.AddRectangle(new Rectangle(x, pos + length - stroke, corner, stroke));
                        }
                        foreach (int y in new[] { pos, pos + length - corner })
                        {
                            path.AddRectangle(new Rectangle(pos, y, stroke, corner));
                            path.AddRectangle(new Rectangle(pos + length - stroke, y, stroke, corner));
                        }
                    }
                }
                break;
            case LayerKind.Image:
                using (var image = Image.FromFile(Path.Combine(imagesDirectory, layer.ImageFile!)))
                {
                    int w = Math.Max(1, (int)Math.Round(image.Width * layer.ImageScale)), h = Math.Max(1, (int)Math.Round(image.Height * layer.ImageScale));
                    path.AddRectangle(new Rectangle(StrokeStart(w), StrokeStart(h), w, h));
                }
                break;
        }
        using var transform = new Matrix();
        transform.Translate(layer.OffsetX, layer.OffsetY);
        if (layer.Kind == LayerKind.Image) transform.RotateAt((float)layer.Rotation, new(.5f, .5f));
        else if (!quarter) transform.RotateAt((float)layer.Rotation, new(Center(layer), Center(layer)));
        path.Transform(transform);
        if (quarter && layer.Kind == LayerKind.Image && path.PointCount > 0)
        {
            // GDI+ matrices can yield -6.0000005 for an exact quarter turn.
            // Snap these coordinates before floor/ceiling so the DIB stays tightly sized.
            var points = path.PathPoints;
            for (int i = 0; i < points.Length; i++) points[i] = new(MathF.Round(points[i].X), MathF.Round(points[i].Y));
            var snapped = new GraphicsPath(points, path.PathTypes, path.FillMode);
            path.Dispose(); return snapped;
        }
        return path;
    }
}
