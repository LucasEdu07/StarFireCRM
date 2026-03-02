using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClienteFormWindow : Window
    {
        private readonly Brush _defaultTextBoxBorderBrush;

        public ClienteFormWindow(ClienteFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _defaultTextBoxBorderBrush = NomeTextBox.BorderBrush;
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteFormViewModel viewModel)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(viewModel.NomeFantasia))
            {
                SetNomeValidationState(true, "Nome é obrigatório.");
                NomeTextBox.Focus();
                return;
            }
            SetNomeValidationState(false, string.Empty);

            if (string.IsNullOrWhiteSpace(viewModel.CpfCnpj))
            {
                SetCpfValidationState(true, "CPF/CNPJ é obrigatório.");
                CpfTextBox.Focus();
                return;
            }
            SetCpfValidationState(false, string.Empty);

            DialogResult = true;
            Close();
        }

        private void NomeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetNomeValidationState(string.IsNullOrWhiteSpace(NomeTextBox.Text), "Nome é obrigatório.");
        }

        private void CpfTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetCpfValidationState(string.IsNullOrWhiteSpace(CpfTextBox.Text), "CPF/CNPJ é obrigatório.");
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

        private void SetNomeValidationState(bool isInvalid, string message)
        {
            NomeTextBox.BorderBrush = isInvalid ? Brushes.IndianRed : _defaultTextBoxBorderBrush;
            NomeValidationText.Text = message;
            NomeValidationText.Visibility = isInvalid ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetCpfValidationState(bool isInvalid, string message)
        {
            CpfTextBox.BorderBrush = isInvalid ? Brushes.IndianRed : _defaultTextBoxBorderBrush;
            CpfValidationText.Text = message;
            CpfValidationText.Visibility = isInvalid ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
