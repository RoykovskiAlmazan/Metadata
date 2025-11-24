using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Conversor.Apis;
using   Conversor; // ComandoItem

namespace Conversor.Analisis
{
    public static class MetadataBuilder
    {
        public static VideoMetadata ConstruirMetadata(string videoPath, WhisperVerboseResult whisper, IEnumerable<ComandoItem> comandos)
        {
            var meta = new VideoMetadata
            {
                Titulo = Path.GetFileNameWithoutExtension(videoPath),
                DuracionTotalSegundos = whisper?.Duration ?? 0.0,
                DuracionHms = FormatearHms(whisper?.Duration ?? 0.0)
            };

            if (comandos != null)
            {
                foreach (var c in comandos.OrderBy(c => c.TS))
                {
                    meta.Instrucciones.Add(new Instruccion
                    {
                        Categoria = c.Categoria,
                        Valor = c.Valor,
                        TSeg = Math.Round(c.TS, 2),
                        THms = FormatearHms(c.TS)
                    });
                }
            }

            return meta;
        }

        private static string FormatearHms(double segundos)
        {
            var ts = TimeSpan.FromSeconds(segundos);
            // 00:03:27.125
            return ts.ToString(@"hh\:mm\:ss\.fff");
        }
    }
}
