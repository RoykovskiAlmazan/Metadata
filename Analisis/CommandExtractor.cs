using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Conversor.Apis;   // WhisperVerboseResult, WhisperSegment
using Conversor;        // ComandoItem

namespace Conversor.Analisis
{
    public static class CommandExtractor
    {
        // Cuenta regresiva: detecta "...3, 2, 1" o "tres, dos, uno"
        private static readonly Regex CountdownRegex = new(
            @"(3\s*,?\s*2\s*,?\s*1)|(tres\s*,?\s*dos\s*,?\s*uno)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    
        private static readonly Regex StartRegex = new(
            @"(comando\s+de\s+start|\biniciar\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex StopRegex = new(
            @"\b(stop|paramos|paro|detenemos|detener|detenerse|se\s+detiene)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex VelocidadRegex = new(
            @"\bvelocidad\b[^\d]*(?<num>\d+([.,]\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex VelocidadKmHRegex = new(
            @"(?<num>\d+([.,]\d+)?)\s+kil[oó]metros\s+por\s+hora",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex InclinacionRegex = new(
            @"\binclinaci[oó]n\b[^\d]*(?<num>\d+([.,]\d+)?)\s*(%|por\s+ciento)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex InclinacionAltRegex = new(
            @"\ba\s+(?<num>\d+([.,]\d+)?)\s+por\s+ciento\s+inclinaci[oó]n",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private const double MaxLookbackSeconds = 25.0; // ventana para buscar comando antes del conteo

        public static List<ComandoItem> ExtraerComandos(WhisperVerboseResult result)
        {
            var comandosFinales = new List<ComandoItem>();
            if (result?.Segments == null || result.Segments.Count == 0)
                return comandosFinales;

            var segments = result.Segments
                                 .Where(s => !string.IsNullOrWhiteSpace(s.Text))
                                 .OrderBy(s => s.Start)
                                 .ToList();

            var countdowns = DetectCountdowns(segments);

            var raw = new List<ComandoItem>();

            foreach (var cd in countdowns)
            {
                var cmd = FindCommandForCountdown(segments, cd.SegmentIndex, cd.Time);
                if (cmd != null)
                    raw.Add(cmd);
            }

            if (raw.Count == 0)
                return comandosFinales;

            // Ordenamos por tiempo
            raw = raw.OrderBy(c => c.TS).ToList();

            bool tieneStartExplicito = raw.Any(c => c.Categoria == "start");

            List<ComandoItem> finalList;

            if (tieneStartExplicito)
            {
                finalList = raw;
            }
            else
            {
                finalList = new List<ComandoItem>();

                int firstVelIndex = raw.FindIndex(c => c.Categoria == "velocidad" && c.Valor > 0.0);

                if (firstVelIndex >= 0)
                {
                    var firstVel = raw[firstVelIndex];

                    finalList.Add(new ComandoItem
                    {
                        Categoria = "start",
                        Valor = 1.0,         
                        TS = firstVel.TS
                    });

                    for (int i = 0; i < raw.Count; i++)
                    {
                        if (i == firstVelIndex) continue;
                        finalList.Add(raw[i]);
                    }
                }
                else
                {
                    finalList.AddRange(raw);
                }
            }

            comandosFinales = CleanDuplicates(finalList);

            return comandosFinales.OrderBy(c => c.TS).ToList();
        }

        // --------- Búsqueda por cada cuenta regresiva ---------

        private static ComandoItem? FindCommandForCountdown(
            IReadOnlyList<WhisperSegment> segments,
            int countdownIndex,
            double countdownTime)
        {
            double cdStart = segments[countdownIndex].Start;

            for (int j = countdownIndex; j >= 0; j--)
            {
                var seg = segments[j];
                if (cdStart - seg.End > MaxLookbackSeconds)
                    break; 

                var text = seg.Text?.ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (StartRegex.IsMatch(text))
                {
                    return new ComandoItem
                    {
                        Categoria = "start",
                        Valor = 1.0,
                        TS = Math.Round(countdownTime, 2)
                    };
                }

                if (StopRegex.IsMatch(text))
                {
                    return new ComandoItem
                    {
                        Categoria = "velocidad", // modelamos stop como velocidad 0.0
                        Valor = 0.0,
                        TS = Math.Round(countdownTime, 2)
                    };
                }

                if (TryParseVelocidad(text, out var velVal))
                {
                    return new ComandoItem
                    {
                        Categoria = "velocidad",
                        Valor = velVal,
                        TS = Math.Round(countdownTime, 2)
                    };
                }

                if (TryParseInclinacion(text, out var incVal))
                {
                    return new ComandoItem
                    {
                        Categoria = "inclinacion",
                        Valor = incVal,
                        TS = Math.Round(countdownTime, 2)
                    };
                }

            }

            return null;
        }

        // --------- Detectar countdowns ---------

        private class CountdownDef
        {
            public int SegmentIndex { get; set; }
            public double Time { get; set; }
        }

        private static List<CountdownDef> DetectCountdowns(IReadOnlyList<WhisperSegment> segments)
        {
            var cds = new List<CountdownDef>();

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var text = seg.Text?.ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (CountdownRegex.IsMatch(text))
                {
                    cds.Add(new CountdownDef
                    {
                        SegmentIndex = i,
                        Time = seg.End
                    });
                }
            }

            return cds;
        }

        // --------- Parseo de velocidad / inclinación / números ---------

        private static bool TryParseVelocidad(string text, out double valor)
        {
            valor = 0;

            var m = VelocidadRegex.Match(text);
            if (!m.Success)
            {
                m = VelocidadKmHRegex.Match(text);
            }

            if (!m.Success)
                return false;

            return TryParseNumero(m.Groups["num"].Value, out valor);
        }

        private static bool TryParseInclinacion(string text, out double valor)
        {
            valor = 0;

            var m = InclinacionRegex.Match(text);
            if (!m.Success)
            {
                m = InclinacionAltRegex.Match(text);
            }

            if (!m.Success)
                return false;

            return TryParseNumero(m.Groups["num"].Value, out valor);
        }

        private static bool TryParseNumero(string raw, out double value)
        {
            raw = raw?.Trim().Replace(',', '.');

            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return false;

            if (value >= 10 && value < 100)
                value /= 10.0;

            return true;
        }

        private static List<ComandoItem> CleanDuplicates(List<ComandoItem> input)
        {
            const double ventanaSegundos = 5.0;

            var result = new List<ComandoItem>();
            ComandoItem? last = null;

            foreach (var c in input.OrderBy(c => c.TS))
            {
                if (last != null &&
                    c.Categoria == last.Categoria &&
                    Math.Abs(c.Valor - last.Valor) < 0.01 &&
                    Math.Abs(c.TS - last.TS) < ventanaSegundos)
                {
                    last = c;
                    result[result.Count - 1] = c;
                }
                else
                {
                    result.Add(c);
                    last = c;
                }
            }

            return result;
        }
    }
}
