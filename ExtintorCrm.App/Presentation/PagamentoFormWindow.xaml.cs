using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Documents;
using Microsoft.Win32;

namespace ExtintorCrm.App.Presentation
{
    public partial class PagamentoFormWindow : Window
    {
        private readonly CultureInfo _ptBr = new("pt-BR");
        private readonly DocumentoAnexoRepository _documentoAnexoRepository = new();
        private readonly DocumentoStorageService _documentoStorageService = new();
        private readonly ObservableCollection<DocumentoAnexoItem> _anexos = new();
        private bool _isFormattingValor;

        public PagamentoFormWindow(PagamentoFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            PagamentoAnexosDataGrid.ItemsSource = _anexos;
            SyncValorFromViewModel();
            if (viewModel.IsEditMode)
            {
                _ = LoadAnexosAsync();
            }
            UpdateAnexosUi();
        }

        private async Task LoadAnexosAsync()
        {
            if (DataContext is not PagamentoFormViewModel vm || vm.PagamentoId == Guid.Empty)
            {
                return;
            }

            var anexos = await _documentoAnexoRepository.ListByPagamentoAsync(vm.PagamentoId);
            _anexos.Clear();
            foreach (var anexo in anexos)
            {
                _anexos.Add(new DocumentoAnexoItem(anexo));
            }

            PagamentoAnexosDataGrid.SelectedIndex = _anexos.Count > 0 ? 0 : -1;
            UpdateAnexosUi();
        }

        private void UpdateAnexosUi()
        {
            if (PagamentoAnexosCountText != null)
            {
                PagamentoAnexosCountText.Text = _anexos.Count == 0
                    ? "Nenhum documento anexado"
                    : $"{_anexos.Count} documento(s) anexado(s)";
            }

            var hasSelection = PagamentoAnexosDataGrid.SelectedItem is DocumentoAnexoItem;
            AbrirPagamentoButton.IsEnabled = hasSelection;
            ExcluirPagamentoButton.IsEnabled = hasSelection;
        }

        private void PagamentoAnexosDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAnexosUi();
        }

        private async void AnexarPagamentoDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PagamentoFormViewModel vm || vm.PagamentoId == Guid.Empty)
            {
                DialogService.Info("Anexos", "Salve o pagamento antes de anexar documentos.", this);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Selecionar documentos do pagamento",
                Filter = "Documentos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            foreach (var fileName in dialog.FileNames)
            {
                StoredDocumentoArquivo? storedFile = null;
                try
                {
                    storedFile = _documentoStorageService.StoreForPagamento(vm.PagamentoId, fileName);
                    await _documentoAnexoRepository.AddAsync(new DocumentoAnexo
                    {
                        Id = storedFile.DocumentoId,
                        PagamentoId = vm.PagamentoId,
                        Contexto = "Pagamento",
                        TipoDocumento = ResolvePagamentoDocumentType(Path.GetFileName(fileName)),
                        NomeOriginal = storedFile.NomeOriginal,
                        CaminhoRelativo = storedFile.CaminhoRelativo,
                        TamanhoBytes = storedFile.TamanhoBytes,
                        CriadoEm = DateTime.UtcNow,
                        AtualizadoEm = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    if (storedFile != null)
                    {
                        _documentoStorageService.DeleteByRelativePath(storedFile.CaminhoRelativo);
                    }

                    DialogService.Error(
                        "Anexos",
                        $"Falha ao anexar '{Path.GetFileName(fileName)}': {ex.Message}",
                        this);
                }
            }

            await LoadAnexosAsync();
        }

        private void AbrirPagamentoDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (PagamentoAnexosDataGrid.SelectedItem is not DocumentoAnexoItem selected)
            {
                return;
            }

            try
            {
                var absolutePath = _documentoStorageService.ResolveAbsolutePath(selected.CaminhoRelativo);
                if (!File.Exists(absolutePath))
                {
                    DialogService.Info("Anexos", "Arquivo não encontrado no armazenamento local.", this);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = absolutePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DialogService.Error("Anexos", $"Falha ao abrir arquivo: {ex.Message}", this);
            }
        }

        private async void ExcluirPagamentoDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (PagamentoAnexosDataGrid.SelectedItem is not DocumentoAnexoItem selected)
            {
                return;
            }

            var confirmed = DialogService.Confirm(
                "Excluir anexo",
                $"Deseja realmente excluir o anexo '{selected.NomeOriginal}'?",
                this);
            if (!confirmed)
            {
                return;
            }

            try
            {
                await _documentoAnexoRepository.DeleteAsync(selected.Id);
                _documentoStorageService.DeleteByRelativePath(selected.CaminhoRelativo);
                await LoadAnexosAsync();
            }
            catch (Exception ex)
            {
                DialogService.Error("Anexos", $"Falha ao excluir anexo: {ex.Message}", this);
            }
        }

        private static string ResolvePagamentoDocumentType(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "Outro";
            }

            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("nf") || lower.Contains("nota"))
            {
                return "Nota fiscal";
            }

            if (lower.Contains("boleto"))
            {
                return "Boleto";
            }

            if (lower.Contains("comprovante") || lower.Contains("recibo"))
            {
                return "Comprovante";
            }

            return "Outro";
        }

                private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PagamentoFormViewModel vm)
            {
                return;
            }

            var isClienteInvalid = vm.ClienteId == Guid.Empty;
            var isValorInvalid = vm.Valor <= 0;
            SetClienteValidationState(isClienteInvalid, "Selecione um cliente.");
            SetValorValidationState(isValorInvalid, "Informe um valor maior que zero.");

            if (isClienteInvalid || isValorInvalid)
            {
                if (isClienteInvalid)
                {
                    ClienteComboBox?.Focus();
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

        private void ClienteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not PagamentoFormViewModel vm)
            {
                return;
            }

            SetClienteValidationState(vm.ClienteId == Guid.Empty, "Selecione um cliente.");
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

        private void SetValorValidationState(bool isInvalid, string message)
        {
            if (ValorValidationText == null)
            {
                return;
            }

            ValorValidationText.Text = message;
            ValorValidationText.Visibility = isInvalid ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetClienteValidationState(bool isInvalid, string message)
        {
            if (ClienteValidationText == null)
            {
                return;
            }

            ClienteValidationText.Text = message;
            ClienteValidationText.Visibility = isInvalid ? Visibility.Visible : Visibility.Collapsed;
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

