using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Export;
using ExtintorCrm.App.Infrastructure.Import;
using ExtintorCrm.App.Infrastructure.Settings;
using Microsoft.Win32;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private async Task ImportAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Planilhas suportadas (*.xlsx;*.xlsm;*.xltx;*.xltm;*.xls;*.xlsb;*.csv)|*.xlsx;*.xlsm;*.xltx;*.xltm;*.xls;*.xlsb;*.csv|Excel OpenXML (*.xlsx;*.xlsm;*.xltx;*.xltm)|*.xlsx;*.xlsm;*.xltx;*.xltm|Excel legado/binário (*.xls;*.xlsb)|*.xls;*.xlsb|CSV (*.csv)|*.csv",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                IsImporting = true;
                var importer = new ClienteExcelImporter();
                var extension = Path.GetExtension(dialog.FileName)?.ToLowerInvariant();
                string? tempConvertedFile = null;
                try
                {
                    var sourcePath = dialog.FileName;
                    if (extension == ".xlsb")
                    {
                        tempConvertedFile = ClienteExcelImporter.ConvertLegacyExcelToXlsxTemp(dialog.FileName);
                        sourcePath = tempConvertedFile;
                    }

                    var result = await importer.ImportAsync(sourcePath);

                    var message = $"Importação concluída: {result.Inserted} clientes inseridos, {result.Updated} clientes atualizados, {result.Skipped} ignorados.";
                    if (result.BlankRowsIgnored > 0)
                    {
                        message = $"{message} Linhas vazias desconsideradas: {result.BlankRowsIgnored}.";
                    }
                    if (result.SkippedReasons.Count > 0)
                    {
                        var principais = result.SkippedReasons
                            .GroupBy(x => x)
                            .OrderByDescending(g => g.Count())
                            .Take(3)
                            .Select(g => $"{g.Key}: {g.Count()}")
                            .ToList();

                        message = $"{message} Motivos: {string.Join(" | ", principais)}";
                    }
                    await ShowToastAsync(message, "Success");
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(tempConvertedFile))
                    {
                        try
                        {
                            File.Delete(tempConvertedFile);
                        }
                        catch
                        {
                            // ignora falha de limpeza temporária
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Erro ao importar arquivo de clientes.", "Erro ao importar arquivo", ex);
            }
            finally
            {
                IsImporting = false;
                await ReloadListAsync();
            }
        }

        private async Task ExportAsync()
        {
            var clienteFields = _clienteExportColumns
                .Select(c => new ExportFieldDefinition(c.Key, c.Header))
                .ToList();
            var pagamentoFields = _pagamentoExportColumns
                .Select(c => new ExportFieldDefinition(c.Key, c.Header))
                .ToList();
            var clientePreviewRows = _allClientes
                .Take(3)
                .Select(c => _clienteExportColumns.ToDictionary(col => col.Key, col => col.ValueSelector(c)))
                .ToList();
            var pagamentoPreviewRows = _allPagamentos
                .Take(3)
                .Select(p => _pagamentoExportColumns.ToDictionary(col => col.Key, col => col.ValueSelector(p)))
                .ToList();
            var optionsWindow = new ExportOptionsWindow(
                clienteFields,
                pagamentoFields,
                _exportPreferredEntity,
                _exportPreferExcel,
                _preferredClienteExportFields,
                _preferredPagamentoExportFields,
                clientePreviewRows,
                pagamentoPreviewRows,
                Application.Current?.MainWindow);

            if (optionsWindow.ShowDialog() != true || optionsWindow.Result == null)
            {
                return;
            }

            var options = optionsWindow.Result;
            var isClientes = options.Entity == "Clientes";
            var filePrefix = isClientes ? "clientes" : "pagamentos";
            var defaultExt = options.IsExcel ? ".xlsx" : ".csv";

            if (isClientes && !_allClientes.Any())
            {
                await ShowToastAsync("Não há clientes para exportar.", "Info");
                return;
            }

            if (!isClientes && !_allPagamentos.Any())
            {
                await ShowToastAsync("Não há pagamentos para exportar.", "Info");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
                AddExtension = true,
                DefaultExt = defaultExt,
                FileName = $"{filePrefix}-{DateTime.Now:yyyyMMdd-HHmm}{defaultExt}"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var exportService = new TabularExportService();
                var selectedKeys = new HashSet<string>(options.SelectedFieldKeys);

                if (options.SaveAsDefault)
                {
                    _exportPreferredEntity = options.Entity;
                    _exportPreferExcel = options.IsExcel;
                    if (isClientes)
                    {
                        _preferredClienteExportFields.Clear();
                        foreach (var key in selectedKeys)
                        {
                            _preferredClienteExportFields.Add(key);
                        }
                    }
                    else
                    {
                        _preferredPagamentoExportFields.Clear();
                        foreach (var key in selectedKeys)
                        {
                            _preferredPagamentoExportFields.Add(key);
                        }
                    }

                    var themeForSave = IsDarkMode ? AppThemeManager.DarkTheme : AppThemeManager.LightTheme;
                    SaveAppSettings(themeForSave);
                }

                if (isClientes)
                {
                    var columns = _clienteExportColumns.Where(c => selectedKeys.Contains(c.Key)).ToList();
                    var path = await exportService.ExportAsync(_allClientes, columns, dialog.FileName);
                    await ShowToastAsync($"Clientes exportados com sucesso: {path}", "Success");
                }
                else
                {
                    var columns = _pagamentoExportColumns.Where(c => selectedKeys.Contains(c.Key)).ToList();
                    var path = await exportService.ExportAsync(_allPagamentos, columns, dialog.FileName);
                    await ShowToastAsync($"Pagamentos exportados com sucesso: {path}", "Success");
                }
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao exportar dados.", "Falha ao exportar dados", ex);
            }
        }

        private static IReadOnlyList<ExportColumnDefinition<Cliente>> BuildClienteExportColumns()
        {
            return new List<ExportColumnDefinition<Cliente>>
            {
                new("cliente", "Cliente", c => c.NomeFantasia ?? string.Empty),
                new("cpf_cnpj", "CPF/CNPJ", c => c.CPF ?? string.Empty),
                new("contato", "Contato", c => c.Contato ?? string.Empty),
                new("telefone", "Telefone", c => c.Telefone1 ?? c.Telefone ?? string.Empty),
                new("aviso", "Aviso", c => c.IsAtivo ? "Ativo" : "Inativo"),
                new("venc_1", "Venc.", c => FormatDate(c.VencimentoExtintores)),
                new("recargas", "Recargas", c => c.StatusRecarga ?? string.Empty),
                new("tipo", "Tipo", c => c.TipoServico ?? string.Empty),
                new("alvara", "Alvará", c => c.NumeroAlvara ?? string.Empty),
                new("venc_2", "Venc. (Alvará)", c => FormatDate(c.VencimentoAlvara)),
                new("status_alvara", "Status", c => c.Status ?? string.Empty),
                new("razao_social", "Razão Social", c => c.RazaoSocial ?? string.Empty),
                new("endereco", "Endereço", c => c.Endereco ?? string.Empty)
            };
        }

        private static IReadOnlyList<ExportColumnDefinition<Pagamento>> BuildPagamentoExportColumns()
        {
            return new List<ExportColumnDefinition<Pagamento>>
            {
                new("cliente", "Cliente", p => p.ClienteNome ?? string.Empty),
                new("descricao", "Descrição", p => p.Descricao),
                new("valor", "Valor", p => p.Valor.ToString("C2", new CultureInfo("pt-BR"))),
                new("vencimento", "Vencimento", p => p.DataVencimento.ToString("dd/MM/yyyy")),
                new("pago", "Pago", p => p.Pago ? "Sim" : "Não"),
                new("data_pagamento", "Data Pagamento", p => FormatDate(p.DataPagamento)),
                new("situacao", "Situação", p => p.SituacaoTexto ?? string.Empty)
            };
        }

        private static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("dd/MM/yyyy") : string.Empty;
        }
    }
}
