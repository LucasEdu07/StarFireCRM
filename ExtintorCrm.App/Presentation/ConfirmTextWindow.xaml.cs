using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class ConfirmTextWindow : Window
    {
        private readonly string _requiredText;
        private readonly ConfirmTextWindowModel _model;

        private ConfirmTextWindow(
            string title,
            string message,
            string requiredText,
            Window? owner)
        {
            InitializeComponent();
            Owner = owner;
            _requiredText = requiredText;
            _model = new ConfirmTextWindowModel
            {
                DialogTitle = title,
                Message = message,
                Instruction = $"Digite \"{requiredText}\" para confirmar:",
                ErrorMessage = string.Empty
            };
            DataContext = _model;
        }

        public static bool Show(
            string title,
            string message,
            string requiredText,
            Window? owner)
        {
            var dialog = new ConfirmTextWindow(title, message, requiredText, owner);
            return dialog.ShowDialog() == true;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var typed = ValueTextBox.Text?.Trim() ?? string.Empty;
            if (!string.Equals(typed, _requiredText, System.StringComparison.OrdinalIgnoreCase))
            {
                _model.ErrorMessage = "Texto de confirmação inválido.";
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }

    public class ConfirmTextWindowModel : ViewModelBase
    {
        private string _dialogTitle = string.Empty;
        private string _message = string.Empty;
        private string _instruction = string.Empty;
        private string _errorMessage = string.Empty;

        public string DialogTitle
        {
            get => _dialogTitle;
            set { _dialogTitle = value; OnPropertyChanged(); }
        }

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public string Instruction
        {
            get => _instruction;
            set { _instruction = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }
    }
}
