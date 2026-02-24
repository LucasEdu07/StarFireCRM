using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExtintorCrm.App.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure.Import
{
    public class PagamentoExcelImporter
    {
        public async Task<ImportResult> ImportAsync(string filePath)
        {
            var result = new ImportResult();

            if (!File.Exists(filePath))
            {
                result.Errors.Add("Arquivo não encontrado.");
                return result;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xlsm" or ".xltx" or ".xltm"))
            {
                result.Errors.Add("Formato não suportado para importação de pagamentos. Use .xlsx.");
                return result;
            }

            await using var db = new AppDbContext();
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                result.Errors.Add("Planilha sem abas.");
                return result;
            }

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                AddSkip(result, "Planilha sem dados");
                return result;
            }

            var headers = BuildHeaderMap(usedRange.FirstRowUsed());
            if (!headers.ContainsKey("cpfcnpj") || !headers.ContainsKey("descricao"))
            {
                result.Errors.Add("Cabeçalhos esperados não encontrados (CPF/CNPJ e Descrição).");
                return result;
            }

            var clientes = await db.Clientes.ToListAsync();
            var clientesByCpf = clientes
                .Where(c => !string.IsNullOrWhiteSpace(c.CPF))
                .Select(c => new { Key = NormalizeDigits(c.CPF), Cliente = c })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key!)
                .ToDictionary(g => g.Key, g => g.First().Cliente);

            var pagamentos = await db.Pagamentos.ToListAsync();
            var pagamentosByKey = pagamentos
                .ToDictionary(BuildKeyForPagamento, p => p);

            foreach (var row in usedRange.RowsUsed().Skip(1))
            {
                result.TotalRowsRead++;

                try
                {
                    var cpf = NormalizeDigits(GetValue(row, headers, "cpfcnpj"));
                    var descricao = NullIfWhite(GetValue(row, headers, "descricao"));
                    var tipo = NullIfWhite(GetValue(row, headers, "tipo")) ?? "Receita";
                    var status = NullIfWhite(GetValue(row, headers, "status")) ?? "Agendado";
                    var categoria = NullIfWhite(GetValue(row, headers, "categoria"));
                    var subcategoria = NullIfWhite(GetValue(row, headers, "subcategoria"));
                    var conta = NullIfWhite(GetValue(row, headers, "conta"));
                    var contaTransferencia = NullIfWhite(GetValue(row, headers, "contatransferencia"));
                    var centro = NullIfWhite(GetValue(row, headers, "centro"));
                    var contato = NullIfWhite(GetValue(row, headers, "contato"));
                    var razaoSocial = NullIfWhite(GetValue(row, headers, "razaosocial"));
                    var forma = NullIfWhite(GetValue(row, headers, "forma"));
                    var projeto = NullIfWhite(GetValue(row, headers, "projeto"));
                    var numeroDocumento = NullIfWhite(GetValue(row, headers, "ndocumento"));
                    var observacoes = NullIfWhite(GetValue(row, headers, "observacoes"));

                    var dataPrevista = ParseDate(GetValue(row, headers, "dataprevista"));
                    var dataEfetiva = ParseDate(GetValue(row, headers, "dataefetiva"));
                    var vencFatura = ParseDate(GetValue(row, headers, "vencfatura"));
                    var valorPrevisto = ParseDecimal(GetValue(row, headers, "valorprevisto"));
                    var valorEfetivo = ParseDecimal(GetValue(row, headers, "valorefetivo"));

                    if (string.IsNullOrWhiteSpace(cpf))
                    {
                        AddSkip(result, "CPF/CNPJ vazio");
                        continue;
                    }

                    if (!clientesByCpf.TryGetValue(cpf!, out var cliente))
                    {
                        AddSkip(result, "Cliente não encontrado para CPF/CNPJ");
                        continue;
                    }

                    var key = BuildImportKey(cpf!, descricao, dataPrevista ?? vencFatura, valorPrevisto ?? valorEfetivo);
                    if (pagamentosByKey.TryGetValue(key, out var existing))
                    {
                        ApplyPagamento(existing);
                        result.Updated++;
                    }
                    else
                    {
                        var novo = new Pagamento
                        {
                            Id = Guid.NewGuid(),
                            CriadoEm = DateTime.UtcNow
                        };
                        ApplyPagamento(novo);
                        db.Pagamentos.Add(novo);
                        pagamentosByKey[key] = novo;
                        result.Inserted++;
                    }

                    void ApplyPagamento(Pagamento pagamento)
                    {
                        var valorBase = valorPrevisto ?? valorEfetivo ?? 0m;
                        var vencimentoBase = dataPrevista ?? vencFatura ?? DateTime.Today;
                        var isPago = status.Contains("efetiv", StringComparison.OrdinalIgnoreCase)
                                     || status.Contains("pago", StringComparison.OrdinalIgnoreCase)
                                     || dataEfetiva.HasValue;

                        pagamento.ClienteId = cliente.Id;
                        pagamento.CpfCnpjCliente = cliente.CPF ?? cliente.Documento;
                        pagamento.ClienteNome = cliente.NomeFantasia;
                        pagamento.Tipo = tipo;
                        pagamento.Status = status;
                        pagamento.DataPrevista = dataPrevista;
                        pagamento.DataEfetiva = dataEfetiva;
                        pagamento.VencimentoFatura = vencFatura;
                        pagamento.ValorPrevisto = valorPrevisto;
                        pagamento.ValorEfetivo = valorEfetivo;
                        pagamento.Categoria = categoria;
                        pagamento.Subcategoria = subcategoria;
                        pagamento.Conta = conta;
                        pagamento.ContaTransferencia = contaTransferencia;
                        pagamento.Centro = centro;
                        pagamento.Contato = contato;
                        pagamento.RazaoSocial = razaoSocial;
                        pagamento.Forma = forma;
                        pagamento.Projeto = projeto;
                        pagamento.NumeroDocumento = numeroDocumento;
                        pagamento.Observacoes = observacoes;
                        pagamento.Descricao = string.IsNullOrWhiteSpace(descricao)
                            ? (categoria ?? "Pagamento")
                            : descricao!;
                        pagamento.Valor = valorBase;
                        pagamento.DataVencimento = vencimentoBase;
                        pagamento.Pago = isPago;
                        pagamento.DataPagamento = isPago ? dataEfetiva : null;
                        pagamento.AtualizadoEm = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Linha {row.RowNumber()}: {ex.Message}");
                }
            }

            await db.SaveChangesAsync();
            return result;
        }

        private static Dictionary<string, int> BuildHeaderMap(IXLRangeRow headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                var key = NormalizeHeader(cell.GetString());
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!map.ContainsKey(key))
                {
                    map[key] = cell.Address.ColumnNumber;
                }
            }

            return map;
        }

        private static string GetValue(IXLRangeRow row, IReadOnlyDictionary<string, int> headers, string key)
        {
            if (!headers.TryGetValue(key, out var column))
            {
                return string.Empty;
            }

            return row.Cell(column).GetString().Trim();
        }

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return string.Empty;
            }

            var normalized = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var filtered = normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark);
            var text = new string(filtered.ToArray()).Normalize(NormalizationForm.FormC);
            text = text.Replace(".", string.Empty)
                .Replace("/", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("º", string.Empty)
                .Replace("ª", string.Empty);
            return text;
        }

        private static string? NullIfWhite(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }

        private static DateTime? ParseDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParse(raw, new CultureInfo("pt-BR"), DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }

            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
            {
                try
                {
                    return DateTime.FromOADate(oa).Date;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static decimal? ParseDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, new CultureInfo("pt-BR"), out var pt))
            {
                return pt;
            }

            var normalized = raw.Replace(".", string.Empty).Replace(",", ".");
            if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var inv))
            {
                return inv;
            }

            return null;
        }

        private static string BuildImportKey(string cpf, string? descricao, DateTime? data, decimal? valor)
        {
            var desc = (descricao ?? string.Empty).Trim().ToUpperInvariant();
            var date = data?.ToString("yyyyMMdd") ?? "00000000";
            var amount = (valor ?? 0m).ToString("0.00", CultureInfo.InvariantCulture);
            return $"{cpf}|{desc}|{date}|{amount}";
        }

        private static string BuildKeyForPagamento(Pagamento pagamento)
        {
            var cpf = NormalizeDigits(pagamento.CpfCnpjCliente) ?? "SEMCPF";
            var data = pagamento.DataPrevista ?? pagamento.DataVencimento;
            var valor = pagamento.ValorPrevisto ?? pagamento.Valor;
            return BuildImportKey(cpf, pagamento.Descricao, data, valor);
        }

        private static void AddSkip(ImportResult result, string reason)
        {
            result.Skipped++;
            result.SkippedReasons.Add(reason);
        }
    }
}
