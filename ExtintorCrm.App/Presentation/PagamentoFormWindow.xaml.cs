using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class PagamentoFormWindow : Window
    {
        private readonly CultureInfo _ptBr = new("pt-BR");
        private bool _isFormattingValor;

        public PagamentoFormWindow(PagamentoFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            SyncValorFromViewModel();
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PagamentoFormViewModel vm)
            {
                return;
            }

            if (vm.ClienteId == Guid.Empty || vm.Valor <= 0)
            {
                var isValorInvalid = vm.Valor <= 0;
                SetValorValidationState(isValorInvalid, "Informe um valor maior que zero.");
                if (!isValorInvalid)
                {
                    DialogService.Info(
                        "Validação",
                        "Preencha os campos obrigatórios (Cliente e Valor).",
                        this);
                }
                else
                {
                    ValorTextBox.Focus();
                }
                return;
            }

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

        private void ValorTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(ch => !char.IsDigit(ch) && ch != ',' && ch != '.');
        }

        private void ValorTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!TryParseMoneyFlexible(text, out _))
            {
                e.CancelCommand();
            }
        }

        private void ValorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingValor || sender is not TextBox textBox)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                UpdateValorOnViewModel(0m);
                SetValorValidationState(false, string.Empty);
                return;
            }

            if (!TryParseMoneyFlexible(textBox.Text, out var value))
            {
                SetValorValidationState(true, "Informe um valor numérico válido.");
                return;
            }

            UpdateValorOnViewModel(value);
            SetValorValidationState(value <= 0m, "Informe um valor maior que zero.");
        }

        private void ValorTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PagamentoFormViewModel vm || sender is not TextBox textBox)
            {
                return;
            }

            _isFormattingValor = true;
            try
            {
                textBox.Text = vm.Valor > 0
                    ? vm.Valor.ToString("N2", _ptBr)
                    : string.Empty;
                textBox.CaretIndex = textBox.Text.Length;
            }
            finally
            {
                _isFormattingValor = false;
            }
        }

        private void ValorTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PagamentoFormViewModel vm || sender is not TextBox textBox)
            {
                return;
            }

            textBox.Text = vm.Valor > 0 ? vm.Valor.ToString("C2", _ptBr) : string.Empty;
            textBox.CaretIndex = textBox.Text.Length;
            SetValorValidationState(vm.Valor <= 0m, "Informe um valor maior que zero.");
        }

        private void SyncValorFromViewModel()
        {
            if (DataContext is not PagamentoFormViewModel vm)
            {
                return;
            }

            ValorTextBox.Text = vm.Valor > 0 ? vm.Valor.ToString("C2", _ptBr) : string.Empty;
            ValorTextBox.CaretIndex = ValorTextBox.Text.Length;
            SetValorValidationState(false, string.Empty);
        }

        private void UpdateValorOnViewModel(decimal value)
        {
            if (DataContext is not PagamentoFormViewModel vm)
            {
                return;
            }

            vm.Valor = value;
        }

        private bool TryParseMoneyFlexible(string input, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var sanitized = input.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .Trim();

            var digitsOnly = new string(sanitized.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digitsOnly))
            {
                return false;
            }

            if (decimal.TryParse(
                    sanitized,
                    NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                    _ptBr,
                    out value))
            {
                return true;
            }

            var lastComma = sanitized.LastIndexOf(',');
            var lastDot = sanitized.LastIndexOf('.');
            var hasExplicitDecimal = lastComma >= 0 || lastDot >= 0;

            if (!hasExplicitDecimal)
            {
                if (!decimal.TryParse(digitsOnly, out var integerValue))
                {
                    return false;
                }

                value = integerValue;
                return true;
            }

            var normalized = sanitized;
            if (lastComma >= 0 && lastDot >= 0)
            {
                // Quando há os dois separadores, o último é decimal e o outro vira milhar.
                var decimalSep = lastComma > lastDot ? ',' : '.';
                var thousandSep = decimalSep == ',' ? '.' : ',';
                normalized = normalized.Replace(thousandSep.ToString(), string.Empty);
                normalized = normalized.Replace(decimalSep, '.');
            }
            else if (lastDot >= 0)
            {
                // Somente ponto: se padrão milhar (ex.: 1.000), remove; senão trata como decimal.
                var dotCount = normalized.Count(ch => ch == '.');
                if (dotCount > 1)
                {
                    normalized = normalized.Replace(".", string.Empty);
                }
                else
                {
                    var digitsAfter = normalized.Length - lastDot - 1;
                    if (digitsAfter == 3)
                    {
                        normalized = normalized.Replace(".", string.Empty);
                    }
                }
            }
            else if (lastComma >= 0)
            {
                // Somente vírgula: padrão pt-BR decimal.
                var commaCount = normalized.Count(ch => ch == ',');
                if (commaCount > 1)
                {
                    var firstComma = normalized.IndexOf(',');
                    normalized = normalized[..(firstComma + 1)]
                        + normalized[(firstComma + 1)..].Replace(",", string.Empty);
                }
                normalized = normalized.Replace(',', '.');
            }

            normalized = new string(normalized.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static int CountDigitsBeforeCaret(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var limit = Math.Min(Math.Max(caretIndex, 0), text.Length);
            var count = 0;
            for (var i = 0; i < limit; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static int FindCaretIndexByDigits(string formattedText, int digitsAtLeft)
        {
            if (digitsAtLeft <= 0)
            {
                return 0;
            }

            var seen = 0;
            for (var i = 0; i < formattedText.Length; i++)
            {
                if (!char.IsDigit(formattedText[i]))
                {
                    continue;
                }

                seen++;
                if (seen >= digitsAtLeft)
                {
                    return Math.Min(i + 1, formattedText.Length);
                }
            }

            return formattedText.Length;
        }

        private void SetValorValidationState(bool hasError, string message)
        {
            ValorValidationText.Text = string.IsNullOrWhiteSpace(message)
                ? "Informe um valor válido."
                : message;
            ValorValidationText.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
            ValorTextBox.BorderBrush = hasError
                ? System.Windows.Media.Brushes.IndianRed
                : (System.Windows.Media.Brush)FindResource("ControlBorder");
        }
    }
}

