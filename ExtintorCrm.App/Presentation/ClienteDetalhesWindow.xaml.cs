using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClienteDetalhesWindow : Window
    {
        private readonly CultureInfo _ptBr = new("pt-BR");
        private bool _isFormattingValor;
        public bool SaveRequested { get; private set; }

        public ClienteDetalhesWindow(ClienteDetalhesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Editar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ClienteDetalhesViewModel vm)
            {
                vm.IsEditMode = true;
            }
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteDetalhesViewModel vm)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.NomeFantasia))
            {
                DialogService.Info("Validação", "Nome é obrigatório.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.CPF))
            {
                DialogService.Info("Validação", "CPF/CNPJ é obrigatório.", this);
                return;
            }

            SaveRequested = true;
            DialogResult = true;
            Close();
        }

        private void CancelarEdicao_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ClienteDetalhesViewModel vm)
            {
                vm.CancelEdit();
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

        private void ValorEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(ch => !char.IsDigit(ch) && ch != ',' && ch != '.');
        }

        private void ValorEditor_Pasting(object sender, DataObjectPastingEventArgs e)
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

        private void ValorEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingValor || sender is not TextBox textBox || textBox.DataContext is not PagamentoPerfilItem item)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                item.Valor = 0m;
                return;
            }

            if (!TryParseMoneyFlexible(textBox.Text, out var value))
            {
                return;
            }

            item.Valor = value;
        }

        private void ValorEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PagamentoPerfilItem item)
            {
                return;
            }

            _isFormattingValor = true;
            try
            {
                textBox.Text = item.Valor > 0 ? item.Valor.ToString("N2", _ptBr) : string.Empty;
                textBox.CaretIndex = textBox.Text.Length;
            }
            finally
            {
                _isFormattingValor = false;
            }
        }

        private void ValorEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PagamentoPerfilItem item)
            {
                return;
            }

            textBox.Text = item.Valor > 0 ? item.Valor.ToString("C2", _ptBr) : string.Empty;
            textBox.CaretIndex = textBox.Text.Length;
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
                var decimalSep = lastComma > lastDot ? ',' : '.';
                var thousandSep = decimalSep == ',' ? '.' : ',';
                normalized = normalized.Replace(thousandSep.ToString(), string.Empty);
                normalized = normalized.Replace(decimalSep, '.');
            }
            else if (lastDot >= 0)
            {
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
    }
}


