using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClienteFormWindow : Window
    {
        public ClienteFormWindow(ClienteFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteFormViewModel viewModel)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(viewModel.NomeFantasia))
            {
                NomeTextBox.BorderBrush = System.Windows.Media.Brushes.IndianRed;
                NomeTextBox.Focus();
                DialogService.Info(
                    "Validação",
                    "Nome é obrigatório.",
                    this);
                return;
            }

            if (string.IsNullOrWhiteSpace(viewModel.CpfCnpj))
            {
                CpfTextBox.BorderBrush = System.Windows.Media.Brushes.IndianRed;
                CpfTextBox.Focus();
                DialogService.Info(
                    "Validação",
                    "CPF/CNPJ é obrigatório.",
                    this);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CpfTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteFormViewModel vm || string.IsNullOrWhiteSpace(vm.CpfCnpj))
            {
                return;
            }

            var digits = DigitsOnly(vm.CpfCnpj);
            vm.CpfCnpj = digits.Length == 11
                ? Convert.ToUInt64(digits).ToString(@"000\.000\.000\-00")
                : digits;
        }

        private void TelefoneTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteFormViewModel vm || string.IsNullOrWhiteSpace(vm.Telefone1))
            {
                return;
            }

            var digits = DigitsOnly(vm.Telefone1);
            if (digits.Length == 11)
            {
                vm.Telefone1 = $"({digits[..2]}) {digits[2..7]}-{digits[7..]}";
            }
            else if (digits.Length == 10)
            {
                vm.Telefone1 = $"({digits[..2]}) {digits[2..6]}-{digits[6..]}";
            }
            else
            {
                vm.Telefone1 = digits;
            }
        }

        private static string DigitsOnly(string input)
        {
            return new string(input.Where(char.IsDigit).ToArray());
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

