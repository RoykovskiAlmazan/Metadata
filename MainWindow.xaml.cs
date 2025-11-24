/*
                                          :#@@@@@@*.                                                 
                                       +@@@@@@@@@@@@%:                                              
                                     .@@@@@@@@@%##%%@@#                                             
                                     @@@@*-:......-+**#*                                            
                                    *@%*+-:...   ..:-=+#                                            
                                    %@@#+=-...   ..::-=++                                           
                                    %@@%*--:.... .....-+#                                           
                                 =: -@@@#+--::=*=:....--..:                                         
                                  +..*@@@@#=-=##%-.....:::.                                         
                                  %=.+@@@@@+...::.  .......                                         
                                  @*::@@%@@%..............                                          
                                 #@#=.@@%@@*...:.......:                                            
                                 @#==.:@@@@@*-..........                                            
                                .%%@%-:=@@@@*=--:.....:.@@                                          
                                #@@@@@: *%@@@+=::....:..#@@@                                        
                             -@@@@@@@@@@@-.@@%+-...:-:..-@@@@=                                      
                           -@@@@@@@###**+%@@@#+*+==-:..:-@@@@@#                                     
                         :@@@@@@@@@@@@@#-::..:-==--:..:-=@@@@@@@.                                   
                       :@@@@@@@@@@@#++*##%######*-:--:-=+@@@@@@@@-                                  
                      *@@@@@@@@@@@@%%#*##=::...:.::+=--=*%@@@@@@@@@@%:                              
                     %@@@@@@@@@%%%##*===++**++**+==#+=--:*@@@@@@@@@@@@@@+                           
                     @@@@@@@@@@@@@@@@@@@@@@@@@@@@  ..    #@@@@@@@@@@@@@@@@@@                        
                    -@@@@@@@@@@@@@@@@@@@@@@@@@@@@       =@@@@@@@@%%@@@@@@@@@@%                      
                    %@@@@@@@@@@@@@@@@@@@@@@@@@@@@:     -@@@@@@@@@@@@@@**%@@%@@@                     
                   .@@@@@@@@@@@@@@@@@@@@@@@@@@@@@:.   :#@@@@@@@@@@@@%***%@%%@@@+                    
                   -@@@@@@@@@@@@@@@@@@@@@@@@%%@@@ :   :%@@@@@@@@@@@*=+*+#@*@@@@@                    
                   #@@@@@@@@@@@@@@@@@@@#@@@@@@@@% ..  .*##%@@@@@%*=+*++**%*@@@@@+                   
                   +@@@@@@@@@@@@@@@@@@@%#@@@@@@@+  . . :#@@@%%#*+=+##*+=+**@@@@@@                   
                    @@@@@@@@@@@@@@@@@@@@+#@@@@@@.  ..   *@%#++++++#%#**+++#@@%@@@+                  
                       :--:%@@@@@@@@@@@@#+*@@@@@  ...   -%%*++++++%%%###+*@@@@#@@@                  
                            @@@@@@@@@@@@@#=+%@@#  .     .#@#++++++#%#++**%@@#%@@@@:                 
                            .@@@@@@@@@@@@@*+*#@= .=-:   .##%++++++*%%*+**@@@#@@@@@.                 
                             @@@@@@@@@@@@@%*++@.   .    .+%@#++++++#@%**#@@@@@@@@@@                 
 
 */
using Conversor.Analisis;         // CommandExtractor, MetadataBuilder
using Conversor.Apis;             // WhisperVerboseResult, TranscriptorService
using Conversor.Conversores;      // Audio_Extract
using Conversor.Events;           // CargarVideo
using Conversor.Produccion;       // Reproductor

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Conversor
{
    public partial class MainWindow : Window
    {
        private Reproductor _reproductor;
        private bool _videoCargado = false;
        private readonly ObservableCollection<ComandoItem> _comandos = new();

        private DispatcherTimer _timelineTimer;
        private bool _isDraggingTimeline = false;

        private WhisperVerboseResult? _lastWhisper;
        private string? _rutaVideoActual;

        public MainWindow()
        {
            InitializeComponent();

            _reproductor = new Reproductor(MediaPlayer);
            LstComandos.ItemsSource = _comandos;
            ActualizarEstadoControles(false);

            _timelineTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _timelineTimer.Tick += TimelineTimer_Tick;
        }

        // ====================
        // CARGA / PROCESO VIDEO
        // ====================

        private async void CargarVideo_Click(object sender, RoutedEventArgs e)
        {
            string ruta = CargarVideo.SeleccionarVideo();
            if (string.IsNullOrEmpty(ruta)) return;

            _reproductor.CargarVideo(ruta);
            _videoCargado = true;
            ActualizarEstadoControles(true);
            _reproductor.Reproducir();

            SetTranscript(string.Empty);
            _comandos.Clear();

            try
            {
                string ffmpegPath = @"C:\ffmpeg\ffmpeg-8.0-essentials_build\bin\ffmpeg.exe";
                string outDir = Path.Combine(Path.GetTempPath(), "ConversorAudio");

                string wavPath = await Audio_Extract.ExtractwavAsync(ruta, outDir, ffmpegPath);

                WhisperVerboseResult whisper = await TranscriptorService.ObtenerTranscripcionVerboseAsync(wavPath);

                string transcriptPlano = whisper.Text ??
                    string.Join(" ", whisper.Segments?.Select(s => s.Text) ?? Array.Empty<string>());
                SetTranscript(transcriptPlano);

                var comandosDetectados = CommandExtractor.ExtraerComandos(whisper);
                LoadComandosDetectados(comandosDetectados);

                _rutaVideoActual = ruta;
                _lastWhisper = whisper;

                ActualizarJsonPreview();

                RightTabs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error durante la transcripción / análisis: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        public void SetTranscript(string texto)
        {
            TxtTranscript.Text = texto ?? string.Empty;
        }

        private void ActualizarJsonPreview()
        {
            if (TxtJsonMetadata == null)
                return;

            if (_lastWhisper == null || string.IsNullOrEmpty(_rutaVideoActual))
            {
                TxtJsonMetadata.Text = "// No hay metadata disponible todavía.";
                return;
            }

            try
            {
               
                var metadata = MetadataBuilder.ConstruirMetadata(_rutaVideoActual, _lastWhisper, _comandos);

                string json = JsonSerializer.Serialize(
                    metadata,
                    new JsonSerializerOptions { WriteIndented = true }
                );

                TxtJsonMetadata.Text = json;
            }
            catch (Exception ex)
            {
                TxtJsonMetadata.Text = $"// Error al construir metadata: {ex.Message}";
            }
        }

        public void LoadComandosDetectados(System.Collections.Generic.IEnumerable<ComandoItem> items)
        {
            _comandos.Clear();
            if (items == null) return;
            foreach (var it in items) _comandos.Add(it);
        }

        // =========
        // CONTROLES
        // =========

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_videoCargado)
            {
                _reproductor.Reproducir();
                _timelineTimer.Start();
            }
        }

        private void Pausa_Click(object sender, RoutedEventArgs e)
        {
            if (_videoCargado)
            {
                _reproductor.Pausar();
                _timelineTimer.Stop();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (_videoCargado)
            {
                _reproductor.Detener();
                MediaPlayer.Position = TimeSpan.Zero;
                TimelineSlider.Value = 0;
                LblTime.Text = "0.00 s";
                _timelineTimer.Stop();
            }
        }


        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            ActualizarEstadoControles(true);

            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var total = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                TimelineSlider.Minimum = 0;
                TimelineSlider.Maximum = total;
                TimelineSlider.Value = 0;
            }

            LblTime.Text = "0.00 s";

            _timelineTimer.Start();
        }


        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Position = TimeSpan.Zero;
            TimelineSlider.Value = 0;
            LblTime.Text = "0.00 s";
            _timelineTimer.Stop();
            ActualizarEstadoControles(true);
        }


        private void ActualizarEstadoControles(bool habilitar)
        {
            BtnPlay.IsEnabled = habilitar;
            BtnPausa.IsEnabled = habilitar;
            BtnStop.IsEnabled = habilitar;
        }

        // ================
        // TIMELINE / SLIDER
        // ================

        private void TimelineTimer_Tick(object? sender, EventArgs e)
        {
            if (!_videoCargado) return;
            if (_isDraggingTimeline) return;
            if (!MediaPlayer.NaturalDuration.HasTimeSpan) return;

            try
            {
                var pos = MediaPlayer.Position.TotalSeconds;
                if (pos < 0) pos = 0;

                if (pos <= TimelineSlider.Maximum)
                {
                    TimelineSlider.Value = pos;
                }

                // actualizar tiempo en segundos
                LblTime.Text = $"{pos:0.00} s";
            }
            catch
            {
                // evitar reventar si el MediaElement está en transición
            }
        }


        private void TimelineSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_videoCargado) return;
            _isDraggingTimeline = true;
        }

        private void TimelineSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_videoCargado) return;

            _isDraggingTimeline = false;

            try
            {
                var segundos = TimelineSlider.Value;
                MediaPlayer.Position = TimeSpan.FromSeconds(segundos);

                // actualizar tiempo en segundos al soltar
                LblTime.Text = $"{segundos:0.00} s";
            }
            catch
            {
                // ignore
            }
        }


        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingTimeline && _videoCargado)
            {
                var segundos = TimelineSlider.Value;
                LblTime.Text = $"{segundos:0.00} s";
            }
        }


        // ==================
        // LISTA DE COMANDOS
        // ==================


        private void LstComandos_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_videoCargado) return;

            if (LstComandos.SelectedItem is ComandoItem cmd)
            {
                var ts = cmd.TS;
                if (ts < 0) ts = 0;

                if (MediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    var total = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    if (ts > total) ts = total;
                }

                try
                {
                    MediaPlayer.Position = TimeSpan.FromSeconds(ts);

                    if (ts >= TimelineSlider.Minimum && ts <= TimelineSlider.Maximum)
                    {
                        TimelineSlider.Value = ts;
                    }

                    // actualizar tiempo en segundos
                    LblTime.Text = $"{ts:0.00} s";
                }
                catch
                {
                    // ignorar errores transitorios del MediaElement
                }
            }
        }


        private void EditarComando_Click(object sender, RoutedEventArgs e)
        {
            if (LstComandos.SelectedItem is not ComandoItem seleccionado)
                return;

            var ventana = new EditarComandoWindow(seleccionado)
            {
                Owner = this
            };

            if (ventana.ShowDialog() == true)
            {
                seleccionado.Categoria = ventana.Comando.Categoria;
                seleccionado.Valor = ventana.Comando.Valor;
                seleccionado.TS = ventana.Comando.TS;

                ActualizarJsonPreview();
            }
        }

        private void EliminarComando_Click(object sender, RoutedEventArgs e)
        {
            if (LstComandos.SelectedItem is not ComandoItem seleccionado)
                return;

            var result = MessageBox.Show(
                "¿Deseas eliminar este comando?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _comandos.Remove(seleccionado);
                ActualizarJsonPreview();
            }
        }

        private void AgregarComandoDebajo_Click(object sender, RoutedEventArgs e)
        {
            int index = LstComandos.SelectedIndex;
            if (index < 0)
            {
                index = _comandos.Count - 1;
            }

            var ventana = new EditarComandoWindow
            {
                Owner = this
            };

            if (ventana.ShowDialog() == true)
            {
                int insertIndex = index + 1;
                if (insertIndex < 0) insertIndex = 0;
                if (insertIndex > _comandos.Count) insertIndex = _comandos.Count;

                _comandos.Insert(insertIndex, ventana.Comando);
                ActualizarJsonPreview();
            }
        }

        // =====================
        // CONFIRMACIÓN + FFMPEG
        // =====================

        private void BtnConfirmarCambios_Click(object sender, RoutedEventArgs e)
        {
            if (_lastWhisper == null || string.IsNullOrEmpty(_rutaVideoActual))
            {
                MessageBox.Show(
                    "No hay video ni transcripción cargados. Carga un video antes de confirmar.",
                    "Sin contexto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_comandos.Count == 0)
            {
                var resVacio = MessageBox.Show(
                    "No hay comandos en la lista.\n\n¿Quieres continuar y guardar metadata sin comandos?",
                    "Sin comandos",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resVacio != MessageBoxResult.Yes)
                    return;
            }

            var confirm = MessageBox.Show(
                "Se escribirá el metadata en un nuevo archivo MP4.\n\n¿Confirmar cambios?",
                "Confirmar cambios",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                // Construimos metadata a partir del estado actual de _comandos
                var metadata = MetadataBuilder.ConstruirMetadata(
                    _rutaVideoActual,
                    _lastWhisper,
                    _comandos // importante: responde a ediciones
                );

                // JSON compacto para incrustar (sin identación ni saltos de línea)
                string jsonCompact = JsonSerializer.Serialize(
                    metadata,
                    new JsonSerializerOptions { WriteIndented = false });

                InsertarMetadataEnVideo(_rutaVideoActual!, jsonCompact);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al confirmar cambios e incrustar metadata:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void InsertarMetadataEnVideo(string inputPath, string json)
        {
            // Ruta de ffmpeg: la misma que usas para el audio
            string ffmpegPath = @"C:\ffmpeg\ffmpeg-8.0-essentials_build\bin\ffmpeg.exe";

            string? dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;

            string fileNameNoExt = Path.GetFileNameWithoutExtension(inputPath);
            string outputPath = Path.Combine(dir, fileNameNoExt + "_meta.mp4");

            // JSON en una sola línea (sin \r\n)
            string jsonOneLine = json.Replace("\r", "").Replace("\n", "");

            // Opción simple: escapamos comillas dobles para no romper el argumento
            string metaValue = jsonOneLine.Replace("\"", "\\\"");

            // Ojo: clave de metadata + movflags para permitir claves arbitrarias
            string args =
                $"-y -i \"{inputPath}\" " +
                "-c copy " +
                "-movflags +use_metadata_tags " +
                $"-metadata com.mercadazo=\"{metaValue}\" " +
                $"\"{outputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = new Process { StartInfo = psi })
            {
                proc.Start();

                string stdErr = proc.StandardError.ReadToEnd();
                string stdOut = proc.StandardOutput.ReadToEnd();

                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"ffmpeg terminó con código {proc.ExitCode}.\n\nSTDERR:\n{stdErr}");
                }
            }

            MessageBox.Show(
                $"Metadata incrustada correctamente.\n\nArchivo generado:\n{outputPath}",
                "Éxito",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

    }

    // Modelo para el Tab "Comandos"
    public class ComandoItem : INotifyPropertyChanged
    {
        private string _categoria;
        private double _valor;
        private double _ts;

        public string Categoria
        {
            get => _categoria;
            set
            {
                if (_categoria != value)
                {
                    _categoria = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Valor
        {
            get => _valor;
            set
            {
                if (Math.Abs(_valor - value) > 0.00001)
                {
                    _valor = value;
                    OnPropertyChanged();
                }
            }
        }

        public double TS
        {
            get => _ts;
            set
            {
                if (Math.Abs(_ts - value) > 0.00001)
                {
                    _ts = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
