using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class DocumentoAnexosWindow : Window
    {
        public DocumentoAnexosWindow(DocumentoAnexosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DocumentoAnexosViewModel vm)
            {
                await vm.LoadAsync();
            }
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
