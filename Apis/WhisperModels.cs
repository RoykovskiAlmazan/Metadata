using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Conversor.Apis
{
    public class WhisperVerboseResult
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("segments")]
        public List<WhisperSegment> Segments { get; set; } = new();
    }

    public class WhisperSegment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
