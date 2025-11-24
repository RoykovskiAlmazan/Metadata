using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Conversor.Analisis
{
    public class VideoMetadata
    {
        [JsonPropertyName("titulo")]
        public string Titulo { get; set; }

        [JsonPropertyName("duracion_total_segundos")]
        public double DuracionTotalSegundos { get; set; }

        [JsonPropertyName("duracion_hms")]
        public string DuracionHms { get; set; }

        [JsonPropertyName("instrucciones")]
        public List<Instruccion> Instrucciones { get; set; } = new();
    }

    public class Instruccion
    {
        [JsonPropertyName("categoria")]
        public string Categoria { get; set; }   // start | stop | velocidad | inclinacion

        [JsonPropertyName("valor")]
        public double Valor { get; set; }       // 1.0, 2.0, 4.0, 0.0, etc.

        [JsonPropertyName("t_seg")]
        public double TSeg { get; set; }        // segundo en el video

        [JsonPropertyName("t_hms")]
        public string THms { get; set; }        // "00:01:42.920"
    }
}
