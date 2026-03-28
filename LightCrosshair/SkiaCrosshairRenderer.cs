using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace LightCrosshair
{
    internal sealed class SkiaCrosshairRenderer : ICrosshairRenderBackend
    {
        private readonly object _sync = new();
        private readonly CrosshairRenderer _fallback = new();
        private Bitmap? _cachedBitmap;
        private string _lastHash = string.Empty;
        private bool _antiAlias = true;
        private float _dpiScale = 1.0f;

        public bool AntiAlias
        {
            get => _antiAlias;
            set
            {
                if (_antiAlias == value) return;
                _antiAlias = value;
                _lastHash = string.Empty;
                _fallback.AntiAlias = value;
            }
        }

        public float DpiScale
        {
            get => _dpiScale;
            set
            {
                float clamped = Math.Clamp(value, 0.75f, 3.0f);
                if (Math.Abs(_dpiScale - clamped) < 0.001f) return;
                _dpiScale = clamped;
                _lastHash = string.Empty;
                _fallback.DpiScale = clamped;
            }
        }

        public Bitmap RenderIfNeeded(CrosshairProfile cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            lock (_sync)
            {
                if (!SupportsShape(cfg.EnumShape))
                {
                    return _fallback.RenderIfNeeded(cfg);
                }

                string hash = ComputeHash(cfg);
                if (_cachedBitmap == null || !string.Equals(hash, _lastHash, StringComparison.Ordinal))
                {
                    _cachedBitmap?.Dispose();
                    _cachedBitmap = RenderWithSkia(cfg);
                    _lastHash = hash;
                }

                return _cachedBitmap;
            }
        }

        public void Invalidate()
        {
            lock (_sync)
            {
                _lastHash = string.Empty;
                _cachedBitmap?.Dispose();
                _cachedBitmap = null;
                _fallback.Invalidate();
            }
        }

        public void Dispose()
        {
            Invalidate();
            _fallback.Dispose();
        }

        private static bool SupportsShape(CrosshairShape shape)
        {
            return shape == CrosshairShape.Dot
                || shape == CrosshairShape.Cross
                || shape == CrosshairShape.CrossOutlined
                || shape == CrosshairShape.Circle
                || shape == CrosshairShape.CircleOutlined
                || shape == CrosshairShape.GapCross
                || shape == CrosshairShape.T
                || shape == CrosshairShape.X
                || shape == CrosshairShape.Box;
        }

        private Bitmap RenderWithSkia(CrosshairProfile cfg)
        {
            float scale = _dpiScale;
            int stroke = Math.Max(1, (int)Math.Round(cfg.Thickness * scale));
            int innerStroke = Math.Max(1, (int)Math.Round(cfg.InnerThickness * scale));
            int edgeStroke = Math.Max(0, (int)Math.Round(cfg.EdgeThickness * scale));

            int max = Math.Max((int)Math.Round(cfg.Size * scale), (int)Math.Round(cfg.InnerSize * scale));
            int thicknessMax = Math.Max(Math.Max(stroke, innerStroke), edgeStroke);
            // Added larger padding to prevent any outline or stroke from clipping
            int padding = (thicknessMax * 4) + 24;
            int side = Math.Max(64, max + padding);
            if (side % 2 == 0) side++;

            // For correct pixel snapping and avoiding anti-alias blur on small shapes:
            // Odd thickness needs a .5 center coordinate.
            // Even thickness needs a .0 center coordinate.
            float cx = stroke % 2 == 0 ? (float)Math.Floor(side / 2f) + 1.0f : side / 2f;
            float cy = cx;

            float radius = Math.Max(1f, (cfg.Size / 2f) * scale);
            float gap = Math.Max(0f, cfg.GapSize * scale);

            Color mainColor = cfg.OuterColor.A > 0 ? cfg.OuterColor : Color.Red;
            Color outlineColor = cfg.EdgeColor.A > 0 ? cfg.EdgeColor : (cfg.InnerShapeColor.A > 0 ? cfg.InnerShapeColor : Color.Black);

            using var bitmap = new SKBitmap(side, side, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            switch (cfg.EnumShape)
            {
                case CrosshairShape.Dot:
                    DrawDot(canvas, cx, cy, radius, stroke, cfg.OutlineEnabled, mainColor, outlineColor);
                    break;

                case CrosshairShape.Cross:
                    DrawCross(canvas, cx, cy, radius, gap, stroke, cfg.OutlineEnabled, mainColor, outlineColor);
                    break;

                case CrosshairShape.CrossOutlined:
                    DrawCross(canvas, cx, cy, radius, gap, stroke, true, mainColor, outlineColor);
                    break;

                case CrosshairShape.GapCross:
                    DrawCross(canvas, cx, cy, radius, Math.Max(1f, gap), stroke, true, mainColor, outlineColor);
                    break;

                case CrosshairShape.Circle:
                    DrawCircle(canvas, cx, cy, radius, stroke, cfg.OutlineEnabled, mainColor, outlineColor);
                    break;

                case CrosshairShape.CircleOutlined:
                    DrawCircle(canvas, cx, cy, radius, stroke, true, mainColor, outlineColor);
                    break;

                case CrosshairShape.T:
                    DrawT(canvas, cx, cy, radius, stroke, mainColor);
                    break;

                case CrosshairShape.X:
                    DrawX(canvas, cx, cy, radius, stroke, mainColor);
                    break;

                case CrosshairShape.Box:
                    DrawBox(canvas, cx, cy, radius, stroke, mainColor);
                    break;

                default:
                    // Should not happen due to SupportsShape guard.
                    return _fallback.RenderIfNeeded(cfg);
            }

            return ToBitmap(bitmap);
        }

        private void DrawDot(SKCanvas canvas, float cx, float cy, float radius, int stroke, bool drawOutline, Color mainColor, Color outlineColor)
        {
            if (drawOutline)
            {
                using var outlineFill = CreateFillPaint(outlineColor);
                using var mainFill = CreateFillPaint(mainColor);
                canvas.DrawCircle(cx, cy, radius, outlineFill);
                canvas.DrawCircle(cx, cy, Math.Max(1f, radius - Math.Max(1, stroke)), mainFill);
                return;
            }

            using var fill = CreateFillPaint(mainColor);
            canvas.DrawCircle(cx, cy, radius, fill);
        }

        private void DrawCross(SKCanvas canvas, float cx, float cy, float radius, float gap, int stroke, bool drawOutline, Color mainColor, Color outlineColor)
        {
            if (drawOutline)
            {
                // Ensure outlineGrow is even so that stroke + outlineGrow has the same parity as stroke, remaining aligned.
                int outlineGrow = stroke % 2 == 0 ? Math.Max(2, stroke) : Math.Max(2, stroke + 1);
                using var outlinePaint = CreateStrokePaint(outlineColor, stroke + outlineGrow);
                DrawCrossLines(canvas, outlinePaint, cx, cy, radius, gap);
            }

            using var mainPaint = CreateStrokePaint(mainColor, stroke);
            DrawCrossLines(canvas, mainPaint, cx, cy, radius, gap);
        }

        private static void DrawCrossLines(SKCanvas canvas, SKPaint paint, float cx, float cy, float radius, float gap)
        {
            // Floor/Round endpoints to ensure crisp butts.
            // When lines are drawn with SKStrokeCap.Butt, they start/end exactly at the given float coordinate.
            // The absolute X/Y coordinates must be integers to perfectly align with the screen pixel grid and avoid anti-aliasing blur.
            
            float x1 = MathF.Round(cx - radius);
            float x2 = MathF.Round(cx - gap);
            float x3 = MathF.Round(cx + gap);
            float x4 = MathF.Round(cx + radius);

            float y1 = MathF.Round(cy - radius);
            float y2 = MathF.Round(cy - gap);
            float y3 = MathF.Round(cy + gap);
            float y4 = MathF.Round(cy + radius);

            canvas.DrawLine(x1, cy, x2, cy, paint);
            canvas.DrawLine(x3, cy, x4, cy, paint);
            canvas.DrawLine(cx, y1, cx, y2, paint);
            canvas.DrawLine(cx, y3, cx, y4, paint);
        }

        private void DrawCircle(SKCanvas canvas, float cx, float cy, float radius, int stroke, bool drawOutline, Color mainColor, Color outlineColor)
        {
            if (drawOutline)
            {
                int outlineGrow = stroke % 2 == 0 ? Math.Max(2, stroke) : Math.Max(2, stroke + 1);
                using var outlinePaint = CreateStrokePaint(outlineColor, stroke + outlineGrow);
                canvas.DrawCircle(cx, cy, radius, outlinePaint);
            }

            using var mainPaint = CreateStrokePaint(mainColor, stroke);
            canvas.DrawCircle(cx, cy, radius, mainPaint);
        }

        private void DrawT(SKCanvas canvas, float cx, float cy, float radius, int stroke, Color color)
        {
            using var paint = CreateStrokePaint(color, stroke);
            float x1 = MathF.Round(cx - radius);
            float x2 = MathF.Round(cx + radius);
            float y1 = MathF.Round(cy + radius); // bottom leg length

            canvas.DrawLine(x1, cy, x2, cy, paint);
            canvas.DrawLine(cx, MathF.Round(cy), cx, y1, paint);
        }

        private void DrawX(SKCanvas canvas, float cx, float cy, float radius, int stroke, Color color)
        {
            using var paint = CreateStrokePaint(color, stroke);
            // MathF offsets are fine here, but since it's diagonal AA will handle it
            canvas.DrawLine(cx - radius, cy - radius, cx + radius, cy + radius, paint);
            canvas.DrawLine(cx - radius, cy + radius, cx + radius, cy - radius, paint);
        }

        private void DrawBox(SKCanvas canvas, float cx, float cy, float radius, int stroke, Color color)
        {
            using var paint = CreateStrokePaint(color, stroke);
            // Snap the bounding box coordinates to integers to prevent blurry lines
            float left = MathF.Round(cx - radius);
            float top = MathF.Round(cy - radius);
            float right = MathF.Round(cx + radius);
            float bottom = MathF.Round(cy + radius);
            
            var rect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(rect, paint);
        }

        private SKPaint CreateStrokePaint(Color color, float width)
        {
            return new SKPaint
            {
                IsAntialias = true, // _antiAlias rimosso per forzare antialiasing sempre
                Style = SKPaintStyle.Stroke,
                Color = ToSkColor(color),
                StrokeWidth = Math.Max(1f, width),
                StrokeCap = SKStrokeCap.Butt,
                StrokeJoin = SKStrokeJoin.Miter
            };
        }

        private SKPaint CreateFillPaint(Color color)
        {
            return new SKPaint
            {
                IsAntialias = true, // _antiAlias rimosso per forzare antialiasing sempre
                Style = SKPaintStyle.Fill,
                Color = ToSkColor(color)
            };
        }

        private static SKColor ToSkColor(Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }

        private static Bitmap ToBitmap(SKBitmap skBitmap)
        {
            var bmp = new Bitmap(skBitmap.Width, skBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
            try
            {
                var bytes = skBitmap.Bytes;
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        private string ComputeHash(CrosshairProfile cfg)
        {
            var sb = new StringBuilder();
            sb.Append(cfg.EnumShape).Append('|')
              .Append(cfg.Shape).Append('|')
              .Append(cfg.Size).Append('|')
              .Append(cfg.Thickness).Append('|')
              .Append(cfg.EdgeThickness).Append('|')
              .Append(cfg.GapSize).Append('|')
              .Append(cfg.InnerSize).Append('|')
              .Append(cfg.InnerThickness).Append('|')
              .Append(cfg.InnerGapSize).Append('|')
              .Append(cfg.OutlineEnabled ? '1' : '0').Append('|')
              .Append(cfg.OuterColor.ToArgb()).Append('|')
              .Append(cfg.EdgeColor.ToArgb()).Append('|')
              .Append(cfg.InnerColor.ToArgb()).Append('|')
              .Append(cfg.FillColor.ToArgb()).Append('|')
              .Append(cfg.InnerShapeColor.ToArgb()).Append('|')
              .Append(_antiAlias ? '1' : '0').Append('|')
              .Append(_dpiScale.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));

            using var sha = SHA1.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }
    }
}
