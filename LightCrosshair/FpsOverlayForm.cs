using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LightCrosshair
{
    internal sealed class FpsOverlayForm : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        private FpsMetricsSnapshot _snapshot;
        private string _source = "None";
        private string _status = "Idle";

        // GDI Object Cache
        private float _lastScale = -1f;
        private string? _lastOverlayColor;
        private string? _lastBgColor;

        private Font? _font;
        private SolidBrush? _textBrush;
        private SolidBrush? _bgBrush;
        private Pen? _outlinePen;
        private Pen? _gridPen;
        private Pen? _targetPen;
        private Pen? _linePen;

        // String Cache
        private readonly List<string> _cachedLines = new List<string>(10);
        private double _lastInstantFps = -1;
        private double _lastAverageFps = -1;
        private double _lastOnePercentLowFps = -1;
        private int _lastGeneratedFrameCount = -1;
        private double _lastFrameTimeMs = -1;
        private bool _lastHasData;
        private string? _lastStatus;
        private string? _lastSource;
        private bool _lastShow1Percent;
        private bool _lastShowGen;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        public FpsOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;

            Rectangle bounds = Rectangle.Empty;
            foreach (var screen in Screen.AllScreens)
            {
                bounds = Rectangle.Union(bounds, screen.Bounds);
            }

            Bounds = bounds;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
        }

        public void UpdateState(FpsMetricsSnapshot snapshot, string source, string status)
        {
            _snapshot = snapshot;
            _source = source;
            _status = status;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font?.Dispose();
                _textBrush?.Dispose();
                _bgBrush?.Dispose();
                _outlinePen?.Dispose();
                _gridPen?.Dispose();
                _targetPen?.Dispose();
                _linePen?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var cfg = CrosshairConfig.Instance;
            if (!cfg.EnableFpsOverlay) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = Math.Clamp(cfg.FpsOverlayScale / 100f, 0.5f, 3f);
            float x = cfg.FpsOverlayX - Bounds.Left;
            float y = cfg.FpsOverlayY - Bounds.Top;
            float padding = 6f * scale;

            if (_lastScale != scale || _lastOverlayColor != cfg.FpsOverlayColorSerialized || _lastBgColor != cfg.FpsOverlayBgColorSerialized)
            {
                var newFont = new Font("Consolas", 12f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                var newColor = ParseRgb(cfg.FpsOverlayColorSerialized, Color.White);
                var newTextBrush = new SolidBrush(newColor);
                var newBgBrush = new SolidBrush(ParseRgba(cfg.FpsOverlayBgColorSerialized, Color.FromArgb(128, 0, 0, 0)));
                var newOutlinePen = new Pen(Color.FromArgb(140, 255, 255, 255), Math.Max(1f, 1f * scale));
                var newGridPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f);
                var newTargetPen = new Pen(Color.FromArgb(160, 0, 255, 0), 1f);
                var newLinePen = new Pen(newColor, 2f);

                var oldFont = _font;
                var oldTextBrush = _textBrush;
                var oldBgBrush = _bgBrush;
                var oldOutlinePen = _outlinePen;
                var oldGridPen = _gridPen;
                var oldTargetPen = _targetPen;
                var oldLinePen = _linePen;

                _font = newFont;
                _textBrush = newTextBrush;
                _bgBrush = newBgBrush;
                _outlinePen = newOutlinePen;
                _gridPen = newGridPen;
                _targetPen = newTargetPen;
                _linePen = newLinePen;

                oldFont?.Dispose();
                oldTextBrush?.Dispose();
                oldBgBrush?.Dispose();
                oldOutlinePen?.Dispose();
                oldGridPen?.Dispose();
                oldTargetPen?.Dispose();
                oldLinePen?.Dispose();

                _lastScale = scale;
                _lastOverlayColor = cfg.FpsOverlayColorSerialized;
                _lastBgColor = cfg.FpsOverlayBgColorSerialized;

                // Force line rebuild on config change
                _lastInstantFps = -1;
            }

            var lines = BuildLines(cfg);
            if (_font == null || _textBrush == null || _bgBrush == null || _outlinePen == null || _gridPen == null || _targetPen == null || _linePen == null)
                return;

            float lineHeight = _font.GetHeight(e.Graphics) + (2f * scale);
            float textWidth = lines.Select(line => e.Graphics.MeasureString(line, _font).Width).DefaultIfEmpty(0f).Max();
            float textHeight = lines.Count * lineHeight;

            bool showGraph = cfg.ShowFrametimeGraph && _snapshot.HasData && _snapshot.RecentFrameTimesMs.Length > 2;
            float graphWidth = showGraph ? 220f * scale : 0f;
            float graphHeight = showGraph ? 80f * scale : 0f;
            float graphGap = showGraph ? 8f * scale : 0f;

            float panelWidth = Math.Max(textWidth, graphWidth) + padding * 2f;
            float panelHeight = textHeight + (showGraph ? graphGap + graphHeight : 0f) + padding * 2f;

            x = Math.Clamp(x, 0, Math.Max(0, ClientSize.Width - panelWidth));
            y = Math.Clamp(y, 0, Math.Max(0, ClientSize.Height - panelHeight));

            var panelRect = new RectangleF(x, y, panelWidth, panelHeight);
            e.Graphics.FillRectangle(_bgBrush, panelRect);
            e.Graphics.DrawRectangle(_outlinePen, panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height);

            float textX = x + padding;
            float textY = y + padding;
            foreach (string line in lines)
            {
                e.Graphics.DrawString(line, _font, _textBrush, textX, textY);
                textY += lineHeight;
            }

            if (!showGraph) return;

            var graphRect = new RectangleF(x + padding, Math.Max(y + padding, textY) + graphGap, graphWidth, graphHeight);
            DrawFrameTimeGraph(e.Graphics, graphRect, _textBrush.Color, _snapshot.RecentFrameTimesMs, _snapshot.SampleCount);
        }

        private List<string> BuildLines(CrosshairConfig cfg)
        {
            if (_lastHasData == _snapshot.HasData && 
                _lastStatus == _status &&
                _lastSource == _source &&
                _lastInstantFps == _snapshot.InstantFps &&
                _lastAverageFps == _snapshot.AverageFps &&
                _lastOnePercentLowFps == _snapshot.OnePercentLowFps &&
                _lastGeneratedFrameCount == _snapshot.GeneratedFrameCount &&
                _lastShow1Percent == cfg.Show1PercentLows &&
                _lastShowGen == cfg.ShowGenFrames &&
                Math.Abs(_lastFrameTimeMs - _snapshot.LatestFrameTimeMs) < 0.01)
            {
                return _cachedLines;
            }

            _lastHasData = _snapshot.HasData;
            _lastStatus = _status;
            _lastSource = _source;
            _lastInstantFps = _snapshot.InstantFps;
            _lastAverageFps = _snapshot.AverageFps;
            _lastOnePercentLowFps = _snapshot.OnePercentLowFps;
            _lastGeneratedFrameCount = _snapshot.GeneratedFrameCount;
            _lastFrameTimeMs = _snapshot.LatestFrameTimeMs;
            _lastShow1Percent = cfg.Show1PercentLows;
            _lastShowGen = cfg.ShowGenFrames;

            _cachedLines.Clear();
            if (!_snapshot.HasData)
            {
                _cachedLines.Add("FPS: --");
                _cachedLines.Add(_status);
                return _cachedLines;
            }

            _cachedLines.Add($"FPS: {_snapshot.InstantFps:0}");
            _cachedLines.Add($"AVG: {_snapshot.AverageFps:0}");

            if (cfg.Show1PercentLows)
            {
                _cachedLines.Add($"1% LOW: {_snapshot.OnePercentLowFps:0}");
            }

            if (cfg.ShowGenFrames)
            {
                _cachedLines.Add($"GEN: {_snapshot.GeneratedFrameCount}");
            }

            _cachedLines.Add($"FT: {_snapshot.LatestFrameTimeMs:0.0} ms");
            _cachedLines.Add($"SRC: {_source}");
            
            return _cachedLines;
        }

        private void DrawFrameTimeGraph(Graphics g, RectangleF graphRect, Color lineColor, double[] values, int sampleCountLimit)
        {
            if (_gridPen == null || _targetPen == null || _linePen == null) return;
            g.DrawRectangle(_gridPen, graphRect.X, graphRect.Y, graphRect.Width, graphRect.Height);

            // 16.67 ms line for 60 FPS reference.
            const double targetMs = 16.67;
            double maxValue = Math.Max(20, Percentile(values, sampleCountLimit, 0.95) * 1.2);
            float targetY = graphRect.Bottom - (float)Math.Min(1.0, targetMs / maxValue) * graphRect.Height;
            g.DrawLine(_targetPen, graphRect.Left, targetY, graphRect.Right, targetY);

            int sampleCount = Math.Min(120, sampleCountLimit);
            if (sampleCount < 2) return;

            int offset = sampleCountLimit - sampleCount;
            var points = new PointF[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                double value = Math.Clamp(values[offset + i], 0, maxValue);
                float nx = sampleCount == 1 ? 0 : i / (float)(sampleCount - 1);
                float x = graphRect.Left + nx * graphRect.Width;
                float y = graphRect.Bottom - (float)(value / maxValue) * graphRect.Height;
                points[i] = new PointF(x, y);
            }

            g.DrawLines(_linePen, points);
        }

        private static double Percentile(double[] data, int validCount, double percentile)
        {
            if (validCount == 0 || data.Length == 0) return 0;
            // Since data might be larger than validCount, we should only copy validCount items to avoid sorting empty zeros
            var ordered = new double[validCount];
            Array.Copy(data, ordered, validCount);
            Array.Sort(ordered);
            double index = (ordered.Length - 1) * percentile;
            int low = (int)Math.Floor(index);
            int high = (int)Math.Ceiling(index);
            if (low == high) return ordered[low];
            double fraction = index - low;
            return ordered[low] + (ordered[high] - ordered[low]) * fraction;
        }

        private static Color ParseRgb(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var parts = value.Split(',');
            if (parts.Length != 3) return fallback;
            if (byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
            {
                return Color.FromArgb(r, g, b);
            }

            return fallback;
        }

        private static Color ParseRgba(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var parts = value.Split(',');
            if (parts.Length == 3 &&
                byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r3) &&
                byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g3) &&
                byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b3))
            {
                return Color.FromArgb(128, r3, g3, b3);
            }

            if (parts.Length == 4 &&
                byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b) &&
                byte.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte a))
            {
                return Color.FromArgb(a, r, g, b);
            }

            return fallback;
        }
    }
}