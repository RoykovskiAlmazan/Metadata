using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;



namespace Conversor.Conversores
{
    public static class Audio_Extract
    {
        public static async Task<String> ExtractwavAsync(string videoPath, string outputDir, string ffmpegPath) {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                throw new FileNotFoundException("Video no encontrado", videoPath);
            }
            if(string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                throw new FileNotFoundException("FFMPEG no encontrado en: ", ffmpegPath);
            }

            Directory.CreateDirectory(outputDir);

            var baseName = Path.GetFileName(videoPath);
            var outPath = Path.Combine(outputDir, $"{baseName}.wav");

            var args = $"-y -i \"{videoPath}\" -vn -ac 1 -ar 16000 -sample_fmt s16 \"{outPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true

            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            string stdOut = await proc.StandardOutput.ReadToEndAsync();
            string stdErr = await proc.StandardError.ReadToEndAsync();

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0 || !File.Exists(outPath))
            {
                var msg = $"FFmpeg falló (ExitCode {proc.ExitCode}). STDERR: {stdErr}";
                throw new InvalidOperationException(msg);
            }

            return outPath;

        }
    }
}
