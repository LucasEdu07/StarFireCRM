using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class ExportOptionsWindow : Window
    {
        private readonly ExportOptionsViewModel _viewModel;
        public ExportOptionsResult? Result { get; private set; }

        public ExportOptionsWindow(
            IReadOnlyList<ExportFieldDefinition> clienteFields,
            IReadOnlyList<ExportFieldDefinition> pagamentoFields,
            string preferredEntity,
            bool preferExcel,
            IReadOnlyCollection<string> preferredClienteKeys,
            IReadOnlyCollection<string> preferredPagamentoKeys,
            IReadOnlyList<Dictionary<string, string>> clientePreviewRows,
            IReadOnlyList<Dictionary<string, string>> pagamentoPreviewRows,
            Window? owner)
        {
            InitializeComponent();
            Owner = owner;
            _viewModel = new ExportOptionsViewModel(
                clienteFields,
                pagamentoFields,
                preferredEntity,
                preferExcel,
                preferredClienteKeys,
                preferredPagamentoKeys,
                clientePreviewRows,
                pagamentoPreviewRows);
            DataContext = _viewModel;
        }

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            var selectedFields = _viewModel.FieldOptions.Where(x => x.IsSelected).Select(x => x.Key).ToList();
            if (selectedFields.Count == 0)
            {
                DialogService.Info(
                    "Exportação",
                    "Selecione ao menos um campo para exportar.",
                    this);
                return;
            }

            Result = new ExportOptionsResult
            {
                Entity = _viewModel.SelectedEntity,
                IsExcel = _viewModel.IsExcel,
                SaveAsDefault = _viewModel.SaveAsDefault,
                SelectedFieldKeys = selectedFields
            };

            DialogResult = true;
            Close();
        }

        private void SelecionarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var field in _viewModel.FieldOptions)
            {
                field.IsSelected = true;
            }
        }

        private void LimparSelecao_Click(object sender, RoutedEventArgs e)
        {
            foreach (var field in _viewModel.FieldOptions)
            {
                field.IsSelected = false;
            }
        }

        private void PresetPadrao_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyPresetPadrao();
        }

        private void PresetFinanceiro_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyPresetFinanceiro();
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

    public sealed class ExportOptionsResult
    {
        public string Entity { get; set; } = "Clientes";
        public bool IsExcel { get; set; } = true;
        public bool SaveAsDefault { get; set; }
        public List<string> SelectedFieldKeys { get; set; } = new();
    }

    public sealed class ExportFieldDefinition
    {
        public ExportFieldDefinition(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }
        public string Label { get; }
    }

    public sealed class ExportFieldOption : ViewModelBase
    {
        private bool _isSelected = true;
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public sealed class ExportOptionsViewModel : ViewModelBase
    {
        private readonly IReadOnlyList<ExportFieldDefinition> _clienteFields;
        private readonly IReadOnlyList<ExportFieldDefinition> _pagamentoFields;
        private readonly IReadOnlyList<Dictionary<string, string>> _clientePreviewRows;
        private readonly IReadOnlyList<Dictionary<string, string>> _pagamentoPreviewRows;
        private readonly HashSet<string> _preferredCliente;
        private readonly HashSet<string> _preferredPagamento;
        private string _selectedEntity;
        private bool _isExcel;
        private bool _saveAsDefault;

        public ExportOptionsViewModel(
            IReadOnlyList<ExportFieldDefinition> clienteFields,
            IReadOnlyList<ExportFieldDefinition> pagamentoFields,
            string preferredEntity,
            bool preferExcel,
            IReadOnlyCollection<string> preferredClienteKeys,
            IReadOnlyCollection<string> preferredPagamentoKeys,
            IReadOnlyList<Dictionary<string, string>> clientePreviewRows,
            IReadOnlyList<Dictionary<string, string>> pagamentoPreviewRows)
        {
            _clienteFields = clienteFields;
            _pagamentoFields = pagamentoFields;
            _clientePreviewRows = clientePreviewRows;
            _pagamentoPreviewRows = pagamentoPreviewRows;
            _preferredCliente = new HashSet<string>(preferredClienteKeys ?? new List<string>());
            _preferredPagamento = new HashSet<string>(preferredPagamentoKeys ?? new List<string>());
            EntityOptions = new ObservableCollection<string> { "Clientes", "Pagamentos" };
            _selectedEntity = preferredEntity == "Pagamentos" ? "Pagamentos" : "Clientes";
            _isExcel = preferExcel;
            FieldOptions = new ObservableCollection<ExportFieldOption>();
            PreviewTable = new DataTable("Preview");
            RebuildFields();
        }

        public ObservableCollection<string> EntityOptions { get; }
        public ObservableCollection<ExportFieldOption> FieldOptions { get; }
        public DataTable PreviewTable { get; }

        public string SelectedEntity
        {
            get => _selectedEntity;
            set
            {
                if (_selectedEntity == value)
                {
                    return;
                }

                _selectedEntity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFinanceiroVisible));
                RebuildFields();
            }
        }

        public bool IsExcel
        {
            get => _isExcel;
            set
            {
                _isExcel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCsv));
            }
        }

        public bool IsCsv
        {
            get => !_isExcel;
            set
            {
                if (!value)
                {
                    return;
                }

                IsExcel = false;
                OnPropertyChanged();
            }
        }

        public bool SaveAsDefault
        {
            get => _saveAsDefault;
            set
            {
                _saveAsDefault = value;
                OnPropertyChanged();
            }
        }

        public bool IsFinanceiroVisible => SelectedEntity == "Pagamentos";

        public string SelectedFieldSummary => $"Campos selecionados: {FieldOptions.Count(f => f.IsSelected)}";

        public void ApplyPresetPadrao()
        {
            if (SelectedEntity == "Pagamentos")
            {
                SetSelection("cliente", "descricao", "valor", "vencimento", "pago", "situacao");
                return;
            }

            SetSelection("cliente", "cpf_cnpj", "telefone", "aviso", "venc_1", "alvara", "venc_2", "status_alvara");
        }

        public void ApplyPresetFinanceiro()
        {
            if (SelectedEntity != "Pagamentos")
            {
                return;
            }

            SetSelection("cliente", "descricao", "valor", "vencimento", "pago", "data_pagamento", "situacao");
        }

        private void RebuildFields()
        {
            foreach (var field in FieldOptions)
            {
                field.PropertyChanged -= FieldOptionChanged;
            }

            FieldOptions.Clear();
            var source = SelectedEntity == "Pagamentos" ? _pagamentoFields : _clienteFields;
            var preferred = SelectedEntity == "Pagamentos" ? _preferredPagamento : _preferredCliente;

            foreach (var field in source)
            {
                var option = new ExportFieldOption
                {
                    Key = field.Key,
                    Label = field.Label,
                    IsSelected = preferred.Count == 0 || preferred.Contains(field.Key)
                };
                option.PropertyChanged += FieldOptionChanged;
                FieldOptions.Add(option);
            }

            if (preferred.Count == 0)
            {
                ApplyPresetPadrao();
            }
            else if (!FieldOptions.Any(f => f.IsSelected))
            {
                ApplyPresetPadrao();
            }

            RefreshPreview();
            OnPropertyChanged(nameof(SelectedFieldSummary));
        }

        private void FieldOptionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ExportFieldOption.IsSelected))
            {
                return;
            }

            RefreshPreview();
            OnPropertyChanged(nameof(SelectedFieldSummary));
        }

        private void SetSelection(params string[] keys)
        {
            var set = new HashSet<string>(keys);
            foreach (var field in FieldOptions)
            {
                field.IsSelected = set.Contains(field.Key);
            }

            RefreshPreview();
            OnPropertyChanged(nameof(SelectedFieldSummary));
        }

        private void RefreshPreview()
        {
            var selected = FieldOptions.Where(f => f.IsSelected).ToList();
            PreviewTable.Clear();
            PreviewTable.Columns.Clear();

            if (selected.Count == 0)
            {
                PreviewTable.Columns.Add("Preview");
                PreviewTable.Rows.Add("Selecione ao menos um campo para visualizar o preview.");
                return;
            }

            foreach (var col in selected)
            {
                PreviewTable.Columns.Add(col.Label);
            }

            var rows = SelectedEntity == "Pagamentos" ? _pagamentoPreviewRows : _clientePreviewRows;
            if (rows.Count == 0)
            {
                var empty = PreviewTable.NewRow();
                if (PreviewTable.Columns.Count > 0)
                {
                    empty[0] = "Sem dados disponíveis para preview.";
                }
                PreviewTable.Rows.Add(empty);
                return;
            }

            foreach (var row in rows.Take(3))
            {
                var dataRow = PreviewTable.NewRow();
                for (var i = 0; i < selected.Count; i++)
                {
                    var s = selected[i];
                    dataRow[i] = row.TryGetValue(s.Key, out var value)
                        ? (string.IsNullOrWhiteSpace(value) ? "—" : value)
                        : "—";
                }

                PreviewTable.Rows.Add(dataRow);
            }
        }
    }
}


