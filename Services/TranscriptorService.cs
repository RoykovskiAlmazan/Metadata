using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Conversor.Apis
{
    public static class TranscriptorService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<WhisperVerboseResult> ObtenerTranscripcionVerboseAsync(string audioFilePath)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
                throw new FileNotFoundException("No se encontró el archivo de audio para transcripción.", audioFilePath);

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("No se encontró la variable de entorno OPENAI_API_KEY.");

            var requestUri = "https://api.openai.com/v1/audio/transcriptions";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var form = new MultipartFormDataContent();

            var fileStream = File.OpenRead(audioFilePath);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(fileContent, "file", Path.GetFileName(audioFilePath));

            // Modelo y formato
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent("verbose_json"), "response_format");
            form.Add(new StringContent("es"), "language"); // español

            request.Content = form;

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Error en transcripción ({(int)response.StatusCode} - {response.StatusCode}): {responseBody}"
                );
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<WhisperVerboseResult>(responseBody, options);
            if (result == null)
                throw new InvalidOperationException("No se pudo deserializar la respuesta de Whisper.");

            return result;
        }
    }
}
