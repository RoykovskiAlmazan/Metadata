/*
 Este archivo se encarga de la carga del video seleccionado para la introducción del metadato UWU
Author: EL roy
 */
using System.Windows.Controls;

namespace Conversor.Produccion
{
    public class Reproductor
    {
        private MediaElement _mediaElement;
        public Reproductor(MediaElement mediaElement)
        {
            _mediaElement = mediaElement;
        }
        public void CargarVideo(string ruta)
        {
            if (!string.IsNullOrEmpty(ruta))
            {
                _mediaElement.Source = new Uri(ruta);
                _mediaElement.LoadedBehavior = MediaState.Manual;
                _mediaElement.UnloadedBehavior = MediaState.Manual;
                _mediaElement.Play();
            }
        }
        public void Reproducir() => _mediaElement.Play(); // no se si implementar automaticamente la reproducción
        public void Pausar() => _mediaElement.Pause();
        public void Detener() => _mediaElement.Stop();
    }
}
