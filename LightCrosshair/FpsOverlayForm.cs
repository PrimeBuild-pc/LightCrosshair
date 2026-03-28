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

        private FpsMetricsSnapshot _liveSnapshot;
        private FpsMetricsSnapshot _textSnapshot;
        private string _displaySource = "None";
        private string _displayStatus = "Idle";
        private long _lastTextRefreshTick = long.MinValue;
        private Rectangle _lastOverlayBounds = Rectangle.Empty;
        private double _graphScaleMaxMs;
        private readonly PointF[] _pointBuffer = new PointF[120];
        private readonly double[] _percentileBuffer = new double[1000];
        private readonly int[] _graphIndexBuffer = new int[120];
        private readonly double[] _graphTimeBuffer = new double[120];

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
        private bool _lastGeneratedFrameAvailability;
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
            _liveSnapshot = snapshot;

            long now = Environment.TickCount64;
            bool shouldRefreshText =
                _lastTextRefreshTick == long.MinValue ||
                now - _lastTextRefreshTick >= SystemFpsMonitor.PreferredUiTextRefreshMs ||
                (_textSnapshot.HasData != snapshot.HasData) ||
                !string.Equals(_displaySource, source, StringComparison.Ordinal) ||
                !string.Equals(_displayStatus, status, StringComparison.Ordinal);

            if (shouldRefreshText)
            {
                _textSnapshot = snapshot;
                _displaySource = source;
                _displayStatus = status;
                _lastTextRefreshTick = now;

                // Force text line rebuild when we swap to a fresher text snapshot.
                _lastInstantFps = -1;
            }

            InvalidateOverlayRegion();
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

            EnsureRenderResources(cfg, scale);

            var lines = BuildLines(cfg);
            if (lines.Count == 0 || _font == null || _textBrush == null || _bgBrush == null || _outlinePen == null || _gridPen == null || _targetPen == null || _linePen == null)
                return;

            float lineHeight = _font.GetHeight(e.Graphics) + (2f * scale);
            float textWidth = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                float w = e.Graphics.MeasureString(lines[i], _font).Width;
                if (w > textWidth) textWidth = w;
            }
            float textHeight = lines.Count * lineHeight;

            bool showGraph = cfg.ShowFrametimeGraph && _liveSnapshot.HasData && _liveSnapshot.SampleCount > 2;
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

            var newBounds = Rectangle.Ceiling(panelRect);
            newBounds.Inflate(2, 2);
            _lastOverlayBounds = Rectangle.Intersect(ClientRectangle, newBounds);

            if (!showGraph) return;

            var graphRect = new RectangleF(x + padding, Math.Max(y + padding, textY) + graphGap, graphWidth, graphHeight);
            double graphTimeWindowMs = Math.Clamp((double)cfg.GraphTimeWindowMs, 1000.0, 5000.0);
            DrawFrameTimeGraph(e.Graphics, graphRect, _liveSnapshot.RecentFrameTimesMs, _liveSnapshot.SampleCount, graphTimeWindowMs);
        }

        private void EnsureRenderResources(CrosshairConfig cfg, float scale)
        {
            if (_lastScale == scale && _lastOverlayColor == cfg.FpsOverlayColorSerialized && _lastBgColor == cfg.FpsOverlayBgColorSerialized)
            {
                return;
            }

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

            // Force line rebuild and reset graph range cache on style change.
            _lastInstantFps = -1;
            _graphScaleMaxMs = 0;
        }

        private void InvalidateOverlayRegion()
        {
            var nextBounds = ComputeOverlayBounds();
            Rectangle invalidateRect;

            if (_lastOverlayBounds.IsEmpty)
            {
                invalidateRect = nextBounds.IsEmpty ? ClientRectangle : nextBounds;
            }
            else if (nextBounds.IsEmpty)
            {
                invalidateRect = _lastOverlayBounds;
            }
            else
            {
                invalidateRect = Rectangle.Union(_lastOverlayBounds, nextBounds);
            }

            _lastOverlayBounds = nextBounds;

            if (!invalidateRect.IsEmpty)
            {
                Invalidate(invalidateRect);
            }
        }

        private Rectangle ComputeOverlayBounds()
        {
            var cfg = CrosshairConfig.Instance;
            if (!cfg.EnableFpsOverlay)
            {
                return Rectangle.Empty;
            }

            float scale = Math.Clamp(cfg.FpsOverlayScale / 100f, 0.5f, 3f);
            EnsureRenderResources(cfg, scale);
            if (_font == null)
            {
                return Rectangle.Empty;
            }

            var lines = BuildLines(cfg);
            int lineHeight = (int)Math.Ceiling(_font.GetHeight() + (2f * scale));
            
            int textWidth = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                int w = TextRenderer.MeasureText(lines[i], _font, Size.Empty, TextFormatFlags.NoPadding).Width;
                if (w > textWidth) textWidth = w;
            }
            
            int textHeight = lines.Count * lineHeight;

            bool showGraph = cfg.ShowFrametimeGraph && _liveSnapshot.HasData && _liveSnapshot.SampleCount > 2;
            float graphWidth = showGraph ? 220f * scale : 0f;
            float graphHeight = showGraph ? 80f * scale : 0f;
            float graphGap = showGraph ? 8f * scale : 0f;
            float padding = 6f * scale;

            float panelWidth = Math.Max(textWidth, graphWidth) + padding * 2f;
            float panelHeight = textHeight + (showGraph ? graphGap + graphHeight : 0f) + padding * 2f;

            float x = cfg.FpsOverlayX - Bounds.Left;
            float y = cfg.FpsOverlayY - Bounds.Top;
            x = Math.Clamp(x, 0, Math.Max(0, ClientSize.Width - panelWidth));
            y = Math.Clamp(y, 0, Math.Max(0, ClientSize.Height - panelHeight));

            var panel = Rectangle.Ceiling(new RectangleF(x, y, panelWidth, panelHeight));
            panel.Inflate(2, 2);
            return Rectangle.Intersect(ClientRectangle, panel);
        }

        private List<string> BuildLines(CrosshairConfig cfg)
        {
            if (_lastHasData == _textSnapshot.HasData && 
                _lastStatus == _displayStatus &&
                _lastSource == _displaySource &&
                _lastInstantFps == _textSnapshot.InstantFps &&
                _lastAverageFps == _textSnapshot.AverageFps &&
                _lastOnePercentLowFps == _textSnapshot.OnePercentLowFps &&
                _lastGeneratedFrameCount == _textSnapshot.GeneratedFrameCount &&
                _lastGeneratedFrameAvailability == _textSnapshot.IsGeneratedFrameDataAvailable &&
                _lastShow1Percent == cfg.Show1PercentLows &&
                _lastShowGen == cfg.ShowGenFrames &&
                Math.Abs(_lastFrameTimeMs - _textSnapshot.LatestFrameTimeMs) < 0.01)
            {
                return _cachedLines;
            }

            _lastHasData = _textSnapshot.HasData;
            _lastStatus = _displayStatus;
            _lastSource = _displaySource;
            _lastInstantFps = _textSnapshot.InstantFps;
            _lastAverageFps = _textSnapshot.AverageFps;
            _lastOnePercentLowFps = _textSnapshot.OnePercentLowFps;
            _lastGeneratedFrameCount = _textSnapshot.GeneratedFrameCount;
            _lastGeneratedFrameAvailability = _textSnapshot.IsGeneratedFrameDataAvailable;
            _lastFrameTimeMs = _textSnapshot.LatestFrameTimeMs;
            _lastShow1Percent = cfg.Show1PercentLows;
            _lastShowGen = cfg.ShowGenFrames;

            _cachedLines.Clear();
            if (!_textSnapshot.HasData)
            {
                _cachedLines.Add("FPS: --");
                _cachedLines.Add(_displayStatus);
                return _cachedLines;
            }

            _cachedLines.Add($"FPS: {_textSnapshot.InstantFps:0}");
            _cachedLines.Add($"AVG: {_textSnapshot.AverageFps:0}");

            if (cfg.Show1PercentLows)
            {
                _cachedLines.Add($"1% LOW: {_textSnapshot.OnePercentLowFps:0}");
            }

            if (cfg.ShowGenFrames)
            {
                if (!_textSnapshot.IsGeneratedFrameDataAvailable)
                {
                    _cachedLines.Add("GEN: N/A");
                }
                else if (_textSnapshot.GeneratedFrameCount == 0)
                {
                    _cachedLines.Add("GEN: OFF");
                }
                else
                {
                    _cachedLines.Add($"GEN: {_textSnapshot.GeneratedFrameCount}");
                }
            }

            _cachedLines.Add($"FT: {_textSnapshot.LatestFrameTimeMs:0.0} ms");
            _cachedLines.Add($"SRC: {_displaySource}");
            
            return _cachedLines;
        }

        private void DrawFrameTimeGraph(Graphics g, RectangleF graphRect, double[] values, int sampleCountLimit, double graphTimeWindowMs)
        {
            if (_gridPen == null || _targetPen == null || _linePen == null) return;
            g.DrawRectangle(_gridPen, graphRect.X, graphRect.Y, graphRect.Width, graphRect.Height);

            int validCount = Math.Min(sampleCountLimit, values.Length);
            if (validCount < 2) return;

            int windowStart = FrameTimeGraphMath.FindTimeWindowStart(values, validCount, graphTimeWindowMs);
            int windowCount = validCount - windowStart;
            if (windowCount < 2) return;

            int pointCount = FrameTimeGraphMath.BuildTimeWindowSamples(
                values,
                windowStart,
                windowCount,
                _graphIndexBuffer,
                _graphTimeBuffer,
                out double totalWindowMs);

            if (pointCount < 2 || totalWindowMs <= 0.001) return;

            // Autoscale using only visible points and smooth transitions with hysteresis.
            double visibleP95 = FrameTimeGraphMath.PercentileWindow(values, windowStart, windowCount, 0.95, _percentileBuffer);
            double desiredMax = Math.Max(10, (visibleP95 * 1.15) + 0.75);
            if (_graphScaleMaxMs <= 0)
            {
                _graphScaleMaxMs = desiredMax;
            }
            else
            {
                // Quantize target slightly to stabilize breathing
                desiredMax = Math.Ceiling(desiredMax / 2.0) * 2.0;
                double lerp = desiredMax > _graphScaleMaxMs ? 0.35 : 0.12;
                _graphScaleMaxMs += (desiredMax - _graphScaleMaxMs) * lerp;
            }

            double maxValue = Math.Clamp(_graphScaleMaxMs, 10, 500);

            double targetMs = maxValue <= 18.0 ? 6.94 : (maxValue <= 34.0 ? 16.67 : 33.33);
            float targetY = graphRect.Bottom - (float)Math.Min(1.0, targetMs / maxValue) * graphRect.Height;
            g.DrawLine(_targetPen, graphRect.Left, targetY, graphRect.Right, targetY);

            for (int i = 0; i < pointCount; i++)
            {
                int sampleIndex = _graphIndexBuffer[i];
                double value = Math.Clamp(values[sampleIndex], 0, maxValue);
                float nx = (float)(_graphTimeBuffer[i] / totalWindowMs);
                float x = graphRect.Left + nx * graphRect.Width;
                float y = graphRect.Bottom - (float)(value / maxValue) * graphRect.Height;
                _pointBuffer[i] = new PointF(x, y);
            }

            if (pointCount > 1)
            {
                if (pointCount == _pointBuffer.Length)
                {
                    g.DrawLines(_linePen, _pointBuffer);
                }
                else
                {
                    var points = new PointF[pointCount];
                    Array.Copy(_pointBuffer, points, pointCount);
                    g.DrawLines(_linePen, points);
                }
            }
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

    internal static class FrameTimeGraphMath
    {
        internal static int FindTimeWindowStart(double[] values, int validCount, double targetWindowMs)
        {
            if (values == null || validCount <= 0 || targetWindowMs <= 0)
            {
                return 0;
            }

            int end = Math.Min(validCount, values.Length) - 1;
            if (end <= 0)
            {
                return 0;
            }

            double sum = 0;
            int i = end;
            while (i > 0 && sum < targetWindowMs)
            {
                sum += values[i];
                i--;
            }

            if (i == 0)
            {
                return 0;
            }

            return Math.Clamp(i + 1, 0, end);
        }

        internal static int BuildTimeWindowSamples(
            double[] values,
            int windowStart,
            int windowCount,
            int[] indexBuffer,
            double[] timeBuffer,
            out double totalWindowMs)
        {
            totalWindowMs = 0;
            if (values == null || indexBuffer == null || timeBuffer == null) return 0;
            if (windowCount <= 1 || indexBuffer.Length == 0 || timeBuffer.Length == 0) return 0;

            int maxPoints = Math.Min(indexBuffer.Length, timeBuffer.Length);
            int available = Math.Min(windowCount, values.Length - Math.Max(0, windowStart));
            if (available <= 1) return 0;

            int pointCount = Math.Min(maxPoints, available);
            int endIndex = windowStart + available - 1;

            int current = windowStart;
            double elapsed = 0;
            
            int previous = windowStart - 1;
            for (int i = 0; i < pointCount; i++)
            {
                int endIdx;
                if (i == pointCount - 1)
                {
                    endIdx = endIndex;
                }
                else
                {
                    double t = i / (double)(pointCount - 1);
                    endIdx = windowStart + (int)Math.Round(t * (available - 1));
                }

                if (endIdx <= previous) endIdx = previous;
                if (endIdx > endIndex) endIdx = endIndex;

                int startIdx = previous + 1;
                if (startIdx > endIdx) startIdx = endIdx;

                int maxIdx = startIdx;
                double maxVal = values[startIdx];
                for (int j = startIdx + 1; j <= endIdx; j++)
                {
                    if (values[j] >= maxVal)
                    {
                        maxVal = values[j];
                        maxIdx = j;
                    }
                }

                indexBuffer[i] = maxIdx;

                while (current <= endIdx)
                {
                    elapsed += values[current];
                    current++;
                }

                timeBuffer[i] = elapsed;
                previous = endIdx;
            }

            totalWindowMs = Math.Max(0.001, elapsed);
            return pointCount;
        }

        internal static double PercentileWindow(double[] data, int offset, int count, double percentile, double[] scratchBuffer)
        {
            if (data == null || scratchBuffer == null) return 0;
            if (count <= 0 || data.Length == 0 || scratchBuffer.Length == 0) return 0;

            int start = Math.Clamp(offset, 0, data.Length - 1);
            int available = data.Length - start;
            int copyCount = Math.Min(count, Math.Min(available, scratchBuffer.Length));
            if (copyCount <= 0) return 0;

            Array.Copy(data, start, scratchBuffer, 0, copyCount);

            double p = Math.Clamp(percentile, 0.0, 1.0);
            double index = (copyCount - 1) * p;
            int low = (int)Math.Floor(index);
            int high = (int)Math.Ceiling(index);

            double lowVal = QuickSelect(scratchBuffer, 0, copyCount - 1, low);
            if (low == high) return lowVal;

            double highVal = QuickSelect(scratchBuffer, 0, copyCount - 1, high);
            double fraction = index - low;
            return lowVal + (highVal - lowVal) * fraction;
        }

        private static double QuickSelect(double[] arr, int left, int right, int k)
        {
            while (left < right)
            {
                int pivotIndex = Partition(arr, left, right);
                if (pivotIndex == k)
                {
                    return arr[k];
                }
                else if (pivotIndex < k)
                {
                    left = pivotIndex + 1;
                }
                else
                {
                    right = pivotIndex - 1;
                }
            }
            return arr[k];
        }

        private static int Partition(double[] arr, int left, int right)
        {
            int mid = left + (right - left) / 2;
            if (arr[mid] < arr[left]) Swap(arr, left, mid);
            if (arr[right] < arr[left]) Swap(arr, left, right);
            if (arr[right] < arr[mid]) Swap(arr, mid, right);
            
            Swap(arr, mid, right);
            double pivot = arr[right];
            int i = left;
            for (int j = left; j < right; j++)
            {
                if (arr[j] <= pivot)
                {
                    Swap(arr, i, j);
                    i++;
                }
            }
            Swap(arr, i, right);
            return i;
        }

        private static void Swap(double[] arr, int a, int b)
        {
            double temp = arr[a];
            arr[a] = arr[b];
            arr[b] = temp;
        }
    }
}