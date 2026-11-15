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
            using var path = Geometry(layer);
            if (path.PointCount == 0) continue;
            var bounds = path.GetBounds();
            int padding = (layer.Outline ? layer.OutlineThickness : 0) + (design.AntiAlias ? 1 : 0);
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
            using (var g = Graphics.FromImage(raster))
            {
                g.Clear(Color.Transparent);
                g.TranslateTransform(origin.X, origin.Y);
                bool quarterTurn = Math.Abs(layer.Rotation % 90) < .00001;
                g.SmoothingMode = design.AntiAlias && (layer.Kind is LayerKind.Dot or LayerKind.Ring || !quarterTurn) ? SmoothingMode.AntiAlias : SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.None;
                if (layer.Kind == LayerKind.Image) DrawImage(g, layer);
                else
                {
                    using var path = Geometry(layer);
                    if (path.PointCount == 0) continue;
                    if (layer.Outline)
                    {
                        using var pen = new Pen(ColorTranslator.FromHtml(layer.OutlineColor), layer.OutlineThickness * 2) { LineJoin = LineJoin.Miter };
                        g.DrawPath(pen, path);
                    }
                    using var brush = new SolidBrush(ColorTranslator.FromHtml(layer.Color));
                    g.FillPath(brush, path);
                }
            }
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)layer.Opacity });
            target.DrawImage(raster, new Rectangle(Point.Empty, canvas), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel, attributes);
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
    internal GraphicsPath Geometry(CrosshairLayer layer)
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
                if ((mask & 1) != 0) path.AddRectangle(new Rectangle(start, 1 - gap - length, t, length));
                if ((mask & 2) != 0) path.AddRectangle(new Rectangle(gap, start, length, t));
                if ((mask & 4) != 0) path.AddRectangle(new Rectangle(start, gap, t, length));
                if ((mask & 8) != 0) path.AddRectangle(new Rectangle(1 - gap - length, start, length, t));
                break;
            case LayerKind.Dot: path.AddEllipse(StrokeStart(length), StrokeStart(length), length, length); break;
            case LayerKind.Ring:
                path.FillMode = FillMode.Alternate;
                path.AddEllipse(StrokeStart(length), StrokeStart(length), length, length);
                if (length > 2 * t) path.AddEllipse(StrokeStart(length) + t, StrokeStart(length) + t, length - 2 * t, length - 2 * t);
                break;
            case LayerKind.Box:
                int pos = StrokeStart(length), stroke = Math.Min(t, (length + 1) / 2);
                if (gap == 0)
                {
                    path.FillMode = FillMode.Alternate;
                    path.AddRectangle(new Rectangle(pos, pos, length, length));
                    if (length > 2 * stroke) path.AddRectangle(new Rectangle(pos + stroke, pos + stroke, length - 2 * stroke, length - 2 * stroke));
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
        if (!quarter || layer.Kind == LayerKind.Image) transform.RotateAt((float)layer.Rotation, new(.5f, .5f));
        path.Transform(transform);
        if (quarter && path.PointCount > 0)
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
