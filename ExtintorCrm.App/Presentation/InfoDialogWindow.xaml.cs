using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class InfoDialogWindow : Window
    {
        public InfoDialogWindow(string titulo, string mensagem, Window? owner)
        {
            InitializeComponent();
            Owner = owner;
            DataContext = new
            {
                Titulo = string.IsNullOrWhiteSpace(titulo) ? "Informação" : titulo.Trim(),
                Mensagem = mensagem ?? string.Empty
            };
        }

        public static bool Show(string titulo, string mensagem, Window? owner = null)
        {
            var dialog = new InfoDialogWindow(titulo, mensagem, owner);
            return dialog.ShowDialog() == true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
