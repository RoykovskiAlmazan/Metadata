using Conversor;
using System.Windows;

namespace Conversor
{
    public partial class EditarComandoWindow : Window
    {
        public ComandoItem Comando { get; }

        // Constructor para CREAR un comando nuevo
        public EditarComandoWindow()
        {
            InitializeComponent();

            Comando = new ComandoItem
            {
                Categoria = "velocidad",
                Valor = 1.0,
                TS = 0.0
            };

            DataContext = Comando;
        }

        // Constructor para EDITAR un comando existente
        public EditarComandoWindow(ComandoItem original)
        {
            InitializeComponent();

            Comando = new ComandoItem
            {
                Categoria = original.Categoria,
                Valor = original.Valor,
                TS = original.TS
            };

            DataContext = Comando;
        }

        private void Aceptar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
