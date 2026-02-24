using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class ConfirmDialogWindow : Window
    {
        public string Titulo { get; }
        public string Mensagem { get; }

        public ConfirmDialogWindow(string titulo, string mensagem, Window? owner)
        {
            Titulo = titulo;
            Mensagem = mensagem;
            InitializeComponent();
            DataContext = this;
            Owner = owner;
        }

        public static bool Show(string titulo, string mensagem, Window? owner)
        {
            var dialog = new ConfirmDialogWindow(titulo, mensagem, owner);
            return dialog.ShowDialog() == true;
        }

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
