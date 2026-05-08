using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LightCrosshair.Diagnostics.PresentMon
{
    internal enum PresentMonFrameGenerationClassification
    {
        Unavailable,
        HeuristicOnly,
        VerifiedSignalPresent,
        Inconclusive,
        UnsupportedCapture
    }

    internal sealed record PresentMonSample(
        string ApplicationName,
        int? ProcessId,
        double? Fps,
        double? FrameTimeMs,
        double? PresentIntervalMs,
        double? DisplayIntervalMs,
        string PresentMode,
        bool? AllowsTearing,
        bool? IsDropped,
        bool? IsLate,
        string FrameType,
        bool? IsExplicitGeneratedFrame,
        IReadOnlyDictionary<string, string> RawColumns);

    internal sealed record PresentMonColumnMap(
        int? ApplicationNameIndex,
        int? ProcessIdIndex,
        int? FpsIndex,
        int? FrameTimeMsIndex,
        int? PresentIntervalMsIndex,
        int? DisplayIntervalMsIndex,
        int? PresentModeIndex,
        int? AllowsTearingIndex,
        int? DroppedIndex,
        int? LateIndex,
        int? FrameTypeIndex,
        IReadOnlyList<string> Columns)
    {
        public bool HasFrameType => FrameTypeIndex.HasValue;
        public bool HasFrameTiming => FrameTimeMsIndex.HasValue || PresentIntervalMsIndex.HasValue || DisplayIntervalMsIndex.HasValue;
    }

    internal sealed record PresentMonCaptureSummary(
        int SampleCount,
        string ApplicationName,
        double? AverageFps,
        double? AverageFrameTimeMs,
        double? P95FrameTimeMs,
        double? P99FrameTimeMs,
        IReadOnlyDictionary<string, int> PresentModeDistribution,
        int AllowsTearingSampleCount,
        int DroppedSampleCount,
        int LateSampleCount,
        int ExplicitGeneratedFrameSampleCount,
        PresentMonFrameGenerationClassification FrameGenerationClassification,
        string FrameGenerationEvidence);

    internal sealed record PresentMonValidationResult(
        bool IsSupportedCapture,
        PresentMonColumnMap ColumnMap,
        IReadOnlyList<PresentMonSample> Samples,
        PresentMonCaptureSummary Summary,
        IReadOnlyList<string> Warnings);

    internal static class PresentMonCsvParser
    {
        public static PresentMonValidationResult ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("CSV path is required.", nameof(path));
            }

            return Parse(File.ReadAllText(path));
        }

        public static PresentMonValidationResult Parse(string csvText)
        {
            var warnings = new List<string>();
            string normalized = csvText ?? string.Empty;
            IReadOnlyList<IReadOnlyList<string>> rows = ReadRows(normalized);

            if (rows.Count == 0)
            {
                var emptyMap = CreateColumnMap(Array.Empty<string>());
                return new PresentMonValidationResult(
                    false,
                    emptyMap,
                    Array.Empty<PresentMonSample>(),
                    CreateSummary(Array.Empty<PresentMonSample>(), emptyMap, "No CSV rows were found."),
                    new[] { "No CSV rows were found." });
            }

            string[] headers = rows[0].Select(header => header.Trim()).ToArray();
            PresentMonColumnMap columnMap = CreateColumnMap(headers);

            if (!columnMap.HasFrameTiming && !columnMap.FpsIndex.HasValue)
            {
                warnings.Add("Capture does not contain recognized frame timing or FPS columns.");
            }

            var samples = new List<PresentMonSample>();
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                IReadOnlyList<string> row = rows[rowIndex];
                if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                samples.Add(CreateSample(headers, columnMap, row, rowIndex, warnings));
            }

            bool isSupported = samples.Count > 0 && (columnMap.HasFrameTiming || columnMap.FpsIndex.HasValue || columnMap.HasFrameType);
            return new PresentMonValidationResult(
                isSupported,
                columnMap,
                samples,
                CreateSummary(samples, columnMap, string.Empty),
                warnings);
        }

        private static PresentMonSample CreateSample(
            IReadOnlyList<string> headers,
            PresentMonColumnMap columnMap,
            IReadOnlyList<string> row,
            int rowIndex,
            ICollection<string> warnings)
        {
            string applicationName = GetString(row, columnMap.ApplicationNameIndex);
            int? processId = GetInt(row, columnMap.ProcessIdIndex, rowIndex, warnings);
            double? fps = GetDouble(row, columnMap.FpsIndex, rowIndex, warnings);
            double? frameTimeMs = GetDouble(row, columnMap.FrameTimeMsIndex, rowIndex, warnings);
            double? presentIntervalMs = GetDouble(row, columnMap.PresentIntervalMsIndex, rowIndex, warnings);
            double? displayIntervalMs = GetDouble(row, columnMap.DisplayIntervalMsIndex, rowIndex, warnings);
            string presentMode = GetString(row, columnMap.PresentModeIndex);
            bool? allowsTearing = GetBool(row, columnMap.AllowsTearingIndex, rowIndex, warnings);
            bool? isDropped = GetBool(row, columnMap.DroppedIndex, rowIndex, warnings);
            bool? isLate = GetBool(row, columnMap.LateIndex, rowIndex, warnings);
            string frameType = GetString(row, columnMap.FrameTypeIndex);

            return new PresentMonSample(
                applicationName,
                processId,
                fps,
                frameTimeMs,
                presentIntervalMs,
                displayIntervalMs,
                presentMode,
                allowsTearing,
                isDropped,
                isLate,
                frameType,
                ClassifyExplicitFrameType(frameType),
                CreateRawColumns(headers, row));
        }

        private static PresentMonCaptureSummary CreateSummary(
            IReadOnlyList<PresentMonSample> samples,
            PresentMonColumnMap columnMap,
            string emptyEvidence)
        {
            double[] frameTimes = samples
                .Select(GetPreferredFrameTime)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Where(value => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value))
                .OrderBy(value => value)
                .ToArray();

            double[] fpsValues = samples
                .Select(sample => sample.Fps)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Where(value => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value))
                .ToArray();

            IReadOnlyDictionary<string, int> presentModes = samples
                .Select(sample => sample.PresentMode)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            int generatedCount = samples.Count(sample => sample.IsExplicitGeneratedFrame == true);
            PresentMonFrameGenerationClassification classification;
            string evidence;

            if (samples.Count == 0)
            {
                classification = PresentMonFrameGenerationClassification.UnsupportedCapture;
                evidence = string.IsNullOrWhiteSpace(emptyEvidence)
                    ? "No PresentMon samples were parsed."
                    : emptyEvidence;
            }
            else if (columnMap.HasFrameType && generatedCount > 0)
            {
                classification = PresentMonFrameGenerationClassification.VerifiedSignalPresent;
                evidence = $"FrameType explicitly reported {generatedCount.ToString(CultureInfo.InvariantCulture)} generated or interpolated frame sample(s).";
            }
            else if (columnMap.HasFrameType)
            {
                classification = PresentMonFrameGenerationClassification.Inconclusive;
                evidence = "FrameType column is present, but no generated or interpolated frame values were observed.";
            }
            else if (columnMap.FpsIndex.HasValue && columnMap.HasFrameTiming)
            {
                classification = PresentMonFrameGenerationClassification.HeuristicOnly;
                evidence = "Capture has FPS and timing data, but no dedicated generated-frame column. Timing or FPS ratios are heuristic only.";
            }
            else
            {
                classification = PresentMonFrameGenerationClassification.Unavailable;
                evidence = "No dedicated PresentMon generated-frame evidence column was found.";
            }

            return new PresentMonCaptureSummary(
                samples.Count,
                MostCommon(samples.Select(sample => sample.ApplicationName)),
                AverageOrNull(fpsValues),
                AverageOrNull(frameTimes),
                Percentile(frameTimes, 0.95),
                Percentile(frameTimes, 0.99),
                presentModes,
                samples.Count(sample => sample.AllowsTearing == true),
                samples.Count(sample => sample.IsDropped == true),
                samples.Count(sample => sample.IsLate == true),
                generatedCount,
                classification,
                evidence);
        }

        private static PresentMonColumnMap CreateColumnMap(IReadOnlyList<string> headers)
        {
            var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                string key = NormalizeColumnName(headers[i]);
                if (!normalized.ContainsKey(key))
                {
                    normalized.Add(key, i);
                }
            }

            return new PresentMonColumnMap(
                Find(normalized, "application", "app", "process", "processname", "executablename"),
                Find(normalized, "processid", "pid"),
                Find(normalized, "fps", "averagefps", "avgfps", "displayedfps", "presentedfps", "fpsdisplay", "fpspresents", "fpsapp"),
                Find(normalized, "frametime", "msbetweenpresents", "msbetweenappstart", "msbetweendisplaychange", "msbetweendisplaychangeactual"),
                Find(normalized, "msbetweenpresents", "presentinterval", "presentintervalms"),
                Find(normalized, "msbetweendisplaychange", "msbetweendisplaychangeactual", "displayinterval", "displayintervalms", "displayedtime"),
                Find(normalized, "presentmode"),
                Find(normalized, "allowstearing"),
                Find(normalized, "dropped", "droppedframe", "droppedframes", "wasdropped"),
                Find(normalized, "late", "lateframe", "islate"),
                Find(normalized, "frametype", "frameclassification", "displayedframetype"),
                headers.ToArray());
        }

        private static int? Find(IReadOnlyDictionary<string, int> normalizedHeaders, params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                if (normalizedHeaders.TryGetValue(NormalizeColumnName(alias), out int index))
                {
                    return index;
                }
            }

            return null;
        }

        private static IReadOnlyDictionary<string, string> CreateRawColumns(
            IReadOnlyList<string> headers,
            IReadOnlyList<string> row)
        {
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                raw[headers[i]] = GetString(row, i);
            }

            return raw;
        }

        private static string NormalizeColumnName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            return builder.ToString();
        }

        private static double? GetPreferredFrameTime(PresentMonSample sample) =>
            sample.FrameTimeMs ?? sample.DisplayIntervalMs ?? sample.PresentIntervalMs;

        private static string GetString(IReadOnlyList<string> row, int? index)
        {
            if (!index.HasValue || index.Value < 0 || index.Value >= row.Count)
            {
                return string.Empty;
            }

            return row[index.Value].Trim();
        }

        private static int? GetInt(
            IReadOnlyList<string> row,
            int? index,
            int rowIndex,
            ICollection<string> warnings)
        {
            string value = GetString(row, index);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }

            warnings.Add($"Row {rowIndex.ToString(CultureInfo.InvariantCulture)} has an invalid integer value '{value}'.");
            return null;
        }

        private static double? GetDouble(
            IReadOnlyList<string> row,
            int? index,
            int rowIndex,
            ICollection<string> warnings)
        {
            string value = GetString(row, index);
            if (string.IsNullOrWhiteSpace(value) || value.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            warnings.Add($"Row {rowIndex.ToString(CultureInfo.InvariantCulture)} has an invalid numeric value '{value}'.");
            return null;
        }

        private static bool? GetBool(
            IReadOnlyList<string> row,
            int? index,
            int rowIndex,
            ICollection<string> warnings)
        {
            string value = GetString(row, index);
            if (string.IsNullOrWhiteSpace(value) || value.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            warnings.Add($"Row {rowIndex.ToString(CultureInfo.InvariantCulture)} has an invalid boolean value '{value}'.");
            return null;
        }

        private static bool? ClassifyExplicitFrameType(string frameType)
        {
            if (string.IsNullOrWhiteSpace(frameType) || frameType.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string normalized = NormalizeColumnName(frameType);
            if (normalized.Contains("generated", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("interpolated", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("synthetic", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Contains("application", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("rendered", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("normal", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return null;
        }

        private static double? AverageOrNull(IReadOnlyCollection<double> values) =>
            values.Count == 0 ? null : values.Average();

        private static double? Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return null;
            }

            if (sortedValues.Count == 1)
            {
                return sortedValues[0];
            }

            double position = (sortedValues.Count - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper)
            {
                return sortedValues[lower];
            }

            double fraction = position - lower;
            return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
        }

        private static string MostCommon(IEnumerable<string> values) =>
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Key)
                .FirstOrDefault() ?? string.Empty;

        private static IReadOnlyList<IReadOnlyList<string>> ReadRows(string text)
        {
            var rows = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
                    {
                        rows.Add(row.ToArray());
                    }

                    row.Clear();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }

            row.Add(field.ToString());
            if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
            {
                rows.Add(row.ToArray());
            }

            return rows;
        }
    }
}
