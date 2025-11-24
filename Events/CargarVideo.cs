using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conversor.Events
{
    public class CargarVideo
    {
        public static string SeleccionarVideo()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos de video (*.mp4)|*.mp4",
                Title = "Seleccionar archivo de video"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
