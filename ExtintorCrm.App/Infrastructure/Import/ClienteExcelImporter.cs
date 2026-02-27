using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExtintorCrm.App.Domain;
using Microsoft.VisualBasic.FileIO;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure.Import
{
    public class ClienteExcelImporter
    {
        public static string ConvertLegacyExcelToXlsxTemp(string filePath)
        {
            return ConvertWithExcelComToXlsx(filePath);
        }

        public async Task<ImportResult> ImportAsync(string filePath)
        {
            var result = new ImportResult();
            var fileValidation = ImportValidation.ValidateSourceFile(
                filePath,
                ".xlsx",
                ".xlsm",
                ".xltx",
                ".xltm",
                ".xls",
                ".xlsb",
                ".csv");
            result.Validation.Merge(fileValidation);
            if (!fileValidation.IsValid)
            {
                result.Errors.AddRange(fileValidation.Issues.Select(x => x.Message));
                return result;
            }

            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            await Task.Run(async () =>
            {
                using var workbook = LoadWorkbookForImport(filePath, extension);
                if (workbook == null)
                {
                    AddSkip(result, "Formato nao suportado");
                    result.Errors.Add("Nao foi possivel abrir o arquivo para importacao.");
                    result.Validation.AddError("Arquivo", "open", "Nao foi possivel abrir o arquivo para importacao.");
                    return;
                }

                var worksheet = FindWorksheetWithNomeHeader(workbook, out var headers);
                if (worksheet == null || headers == null)
                {
                    AddSkip(result, "Aba sem dados");
                    result.Errors.Add("Nenhuma aba com cabecalho 'Nome' foi encontrada.");
                    result.Validation.AddError("Cabecalho", "nome_required", "Nenhuma aba com cabecalho 'Nome' foi encontrada.");
                    return;
                }

                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    AddSkip(result, "Aba sem dados");
                    result.Validation.AddWarning("Planilha", "no_data", "Aba sem dados para importar.");
                    return;
                }

                await using var db = new AppDbContext();
                var existingClientes = await db.Clientes
                    .Where(c => !string.IsNullOrWhiteSpace(c.CPF))
                    .ToListAsync();
                var byCpf = existingClientes
                    .Where(c => !string.IsNullOrWhiteSpace(c.CPF))
                    .Select(c => new { Key = NormalizeDigits(c.CPF!), Cliente = c })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .GroupBy(x => x.Key)
                    .ToDictionary(g => g.Key, g => g.First().Cliente);

                var byNomeTelefone = existingClientes
                    .Select(c => new { Key = BuildLookupKeyForExisting(c), Cliente = c })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .GroupBy(x => x.Key)
                    .ToDictionary(g => g.Key, g => g.First().Cliente);
                var processedKeys = new HashSet<string>();

                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    result.TotalRowsRead++;

                    try
                    {
                        var nome = GetValueAny(row, headers, "nome", "nomefantasia", "cliente");
                        var documento = NormalizeDigitsOrNull(GetValueAny(
                            row,
                            headers,
                            "cpf",
                            "cnpj",
                            "cpfcnpj",
                            "cnpjcpf",
                            "documento",
                            "doc"));
                        var telefone1 = NormalizeDigitsOrNull(GetValueAny(row, headers, "telefone1", "telefone"));
                        var email = NullIfWhite(GetValueAny(row, headers, "email"));
                        var cidade = NullIfWhite(GetValueAny(row, headers, "cidade"));

                        if (string.IsNullOrWhiteSpace(nome) &&
                            string.IsNullOrWhiteSpace(documento) &&
                            string.IsNullOrWhiteSpace(telefone1) &&
                            string.IsNullOrWhiteSpace(email) &&
                            string.IsNullOrWhiteSpace(cidade))
                        {
                            result.BlankRowsIgnored++;
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(nome) && string.IsNullOrWhiteSpace(documento) && string.IsNullOrWhiteSpace(telefone1) &&
                            string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(cidade))
                        {
                            AddSkip(result, "Chave insuficiente");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(nome))
                        {
                            AddSkip(result, "Nome vazio");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(documento))
                        {
                            AddSkip(result, "CPF/CNPJ vazio");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(documento) && documento.Length is not (11 or 14))
                        {
                            AddSkip(result, "CPF/CNPJ invalido");
                            continue;
                        }

                        var dedupKey = BuildDedupKey(nome, documento, telefone1, email, cidade, result);
                        if (string.IsNullOrWhiteSpace(dedupKey))
                        {
                            AddSkip(result, "Chave insuficiente");
                            continue;
                        }

                        if (!processedKeys.Add(dedupKey))
                        {
                            AddSkip(result, "Chave duplicada");
                            continue;
                        }

                        Cliente? cliente = null;
                        var isNewCliente = false;

                        if (dedupKey.StartsWith("CPF:", StringComparison.Ordinal) && byCpf.TryGetValue(documento!, out var existingByCpf))
                        {
                            cliente = existingByCpf;
                        }
                        else if (byNomeTelefone.TryGetValue(dedupKey, out var existingByNomeTelefone))
                        {
                            cliente = existingByNomeTelefone;
                        }

                        if (cliente == null)
                        {
                            cliente = new Cliente
                            {
                                Id = Guid.NewGuid(),
                                CriadoEm = DateTime.UtcNow
                            };
                            isNewCliente = true;
                        }

                        var mapped = new Cliente();
                        MapCliente(mapped, row, headers);
                        ApplyMappedFields(cliente, mapped);
                        // Regra de negócio: se passou na validação, CPF/CNPJ não pode voltar a null.
                        cliente.CPF = documento;
                        cliente.Documento = documento;
                        cliente.AtualizadoEm = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(cliente.CPF))
                        {
                            byCpf[cliente.CPF] = cliente;
                        }

                        var updatedKey = BuildDedupKey(cliente.NomeFantasia, cliente.CPF, cliente.Telefone1, cliente.Email, cliente.Cidade, result);
                        if (!string.IsNullOrWhiteSpace(updatedKey) && !updatedKey.StartsWith("CPF:", StringComparison.Ordinal))
                        {
                            byNomeTelefone[updatedKey] = cliente;
                        }

                        if (isNewCliente)
                        {
                            db.Clientes.Add(cliente);
                            result.Inserted++;
                        }
                        else
                        {
                            result.Updated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Linha {row.RowNumber()}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync();
            });

            return result;
        }

        private static XLWorkbook? LoadWorkbookForImport(string filePath, string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            if (extension is ".xlsx" or ".xlsm" or ".xltx" or ".xltm")
            {
                return new XLWorkbook(filePath);
            }

            if (extension == ".csv")
            {
                return BuildWorkbookFromCsv(filePath);
            }

            if (extension == ".xlsb")
            {
                try
                {
                    return BuildWorkbookByExcelComConversion(filePath);
                }
                catch (Exception conversionEx)
                {
                    throw new InvalidOperationException(
                        "Nao foi possivel ler este arquivo .xlsb com seguranca. " +
                        "A leitura deste formato depende do Excel instalado para conversao. " +
                        "Abra o arquivo no Excel e salve como .xlsx, depois importe novamente.",
                        conversionEx);
                }
            }

            if (extension == ".xls")
            {
                try
                {
                    return BuildWorkbookByExcelComConversion(filePath);
                }
                catch (Exception conversionEx)
                {
                    try
                    {
                        return BuildWorkbookFromOleDb(filePath, extension);
                    }
                    catch (Exception oleDbEx)
                    {
                        throw new InvalidOperationException(
                            "Nao foi possivel ler este arquivo .xls. " +
                            "Tentamos conversao via Excel e leitura OLE DB, mas ambas falharam. " +
                            "Converta para .xlsx e tente novamente.",
                            new AggregateException(conversionEx, oleDbEx));
                    }
                }
            }

            return null;
        }

        private static XLWorkbook BuildWorkbookFromCsv(string filePath)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Importacao");

            var delimiter = DetectCsvDelimiter(filePath);
            using var parser = new TextFieldParser(filePath, Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(delimiter.ToString());

            var rowNumber = 1;
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields() ?? [];
                for (var i = 0; i < fields.Length; i++)
                {
                    worksheet.Cell(rowNumber, i + 1).Value = fields[i] ?? string.Empty;
                }

                rowNumber++;
            }

            return workbook;
        }

        private static char DetectCsvDelimiter(string filePath)
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, true);
            var firstLine = reader.ReadLine() ?? string.Empty;
            var semicolonCount = firstLine.Count(c => c == ';');
            var commaCount = firstLine.Count(c => c == ',');
            return semicolonCount >= commaCount ? ';' : ',';
        }

        private static XLWorkbook BuildWorkbookFromOleDb(string filePath, string extension)
        {
            var dataTable = ReadSheetWithOleDb(filePath, extension);
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Importacao");

            for (var col = 0; col < dataTable.Columns.Count; col++)
            {
                worksheet.Cell(1, col + 1).Value = dataTable.Columns[col].ColumnName;
            }

            for (var row = 0; row < dataTable.Rows.Count; row++)
            {
                for (var col = 0; col < dataTable.Columns.Count; col++)
                {
                    worksheet.Cell(row + 2, col + 1).Value = dataTable.Rows[row][col]?.ToString() ?? string.Empty;
                }
            }

            return workbook;
        }

        private static DataTable ReadSheetWithOleDb(string filePath, string extension)
        {
            var extProps = extension switch
            {
                ".xls" => "Excel 8.0;HDR=YES;IMEX=1",
                ".xlsb" => "Excel 12.0;HDR=YES;IMEX=1",
                _ => "Excel 12.0 Xml;HDR=YES;IMEX=1"
            };

            var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties=\"{extProps}\";";
            var oleDbConnectionType = Type.GetType("System.Data.OleDb.OleDbConnection, System.Data.OleDb", throwOnError: false);
            if (oleDbConnectionType == null)
            {
                throw new InvalidOperationException("Assembly System.Data.OleDb indisponivel neste runtime.");
            }

            using var connection = (IDbConnection?)Activator.CreateInstance(oleDbConnectionType);
            if (connection == null)
            {
                throw new InvalidOperationException("Nao foi possivel criar conexao OLE DB.");
            }

            connection.ConnectionString = connectionString;
            connection.Open();

            var getSchemaMethod = oleDbConnectionType.GetMethod("GetSchema", new[] { typeof(string) });
            var schema = (DataTable?)getSchemaMethod?.Invoke(connection, new object[] { "Tables" });
            if (schema == null || schema.Rows.Count == 0)
            {
                throw new InvalidOperationException("Nenhuma aba encontrada no arquivo.");
            }

            string? tableName = null;
            foreach (DataRow row in schema.Rows)
            {
                var name = row["TABLE_NAME"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.EndsWith("$") || name.EndsWith("$'") || name.EndsWith("$\""))
                {
                    tableName = name;
                    break;
                }
            }

            tableName ??= schema.Rows[0]["TABLE_NAME"]?.ToString();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new InvalidOperationException("Nenhuma aba valida encontrada.");
            }

            var table = new DataTable();
            using var command = connection.CreateCommand();
            if (command == null)
            {
                throw new InvalidOperationException("Nao foi possivel criar comando de leitura.");
            }

            command.CommandText = $"SELECT * FROM [{tableName}]";
            using var reader = command.ExecuteReader();
            if (reader == null)
            {
                throw new InvalidOperationException("Nao foi possivel ler dados da planilha.");
            }

            // Carrega manualmente para evitar constraints/chaves inferidas pelo provider OLE DB
            // (ex.: coluna CPF definida como chave) que quebram quando houver NULL na origem.
            var fieldCount = reader.FieldCount;
            for (var i = 0; i < fieldCount; i++)
            {
                var columnName = reader.GetName(i);
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    columnName = $"Coluna{i + 1}";
                }

                var uniqueName = columnName;
                var suffix = 2;
                while (table.Columns.Contains(uniqueName))
                {
                    uniqueName = $"{columnName}_{suffix}";
                    suffix++;
                }

                table.Columns.Add(uniqueName, typeof(string));
            }

            while (reader.Read())
            {
                var values = new object[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    values[i] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i)) ?? string.Empty;
                }

                table.Rows.Add(values);
            }

            return table;
        }

        private static XLWorkbook BuildWorkbookByExcelComConversion(string filePath)
        {
            var tempXlsx = ConvertWithExcelComToXlsx(filePath);
            try
            {
                return new XLWorkbook(tempXlsx);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempXlsx))
                    {
                        File.Delete(tempXlsx);
                    }
                }
                catch
                {
                    // Ignora falha de limpeza de arquivo temporario.
                }
            }
        }

        private static string ConvertWithExcelComToXlsx(string sourcePath)
        {
            string? outputPath = null;
            Exception? failure = null;

            var thread = new System.Threading.Thread(() =>
            {
                object? excelApp = null;
                object? workbooks = null;
                object? workbook = null;

                try
                {
                    var excelType = Type.GetTypeFromProgID("Excel.Application");
                    if (excelType == null)
                    {
                        throw new InvalidOperationException("Microsoft Excel nao esta instalado neste computador.");
                    }

                    excelApp = Activator.CreateInstance(excelType);
                    if (excelApp == null)
                    {
                        throw new InvalidOperationException("Nao foi possivel iniciar o Microsoft Excel.");
                    }

                    excelType.InvokeMember("Visible", BindingFlags.SetProperty, null, excelApp, new object[] { false });
                    excelType.InvokeMember("DisplayAlerts", BindingFlags.SetProperty, null, excelApp, new object[] { false });

                    workbooks = excelType.InvokeMember("Workbooks", BindingFlags.GetProperty, null, excelApp, null);
                    if (workbooks == null)
                    {
                        throw new InvalidOperationException("Nao foi possivel acessar o motor de planilhas do Excel.");
                    }

                    var workbooksType = workbooks.GetType();
                    workbook = workbooksType.InvokeMember(
                        "Open",
                        BindingFlags.InvokeMethod,
                        null,
                        workbooks,
                        new object[] { sourcePath, Type.Missing, true });

                    if (workbook == null)
                    {
                        throw new InvalidOperationException("Nao foi possivel abrir o arquivo no Excel.");
                    }

                    outputPath = Path.Combine(Path.GetTempPath(), $"starfire-import-{Guid.NewGuid():N}.xlsx");
                    var workbookType = workbook.GetType();
                    workbookType.InvokeMember(
                        "SaveAs",
                        BindingFlags.InvokeMethod,
                        null,
                        workbook,
                        new object[] { outputPath, 51 }); // 51 = xlOpenXMLWorkbook (.xlsx)
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    try
                    {
                        if (workbook != null)
                        {
                            workbook.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, workbook, new object[] { false });
                        }
                    }
                    catch { }

                    try
                    {
                        if (excelApp != null)
                        {
                            excelApp.GetType().InvokeMember("Quit", BindingFlags.InvokeMethod, null, excelApp, null);
                        }
                    }
                    catch { }

                    ReleaseComObjectSafely(workbook);
                    ReleaseComObjectSafely(workbooks);
                    ReleaseComObjectSafely(excelApp);
                }
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException("Falha ao converter arquivo via Excel.", failure);
            }

            if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
            {
                throw new InvalidOperationException("Falha ao converter arquivo para .xlsx.");
            }

            return outputPath;
        }

        private static void ReleaseComObjectSafely(object? comObject)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                Marshal.FinalReleaseComObject(comObject);
            }
            catch
            {
                // Ignora falha de limpeza COM.
            }
        }

        private static void MapCliente(Cliente cliente, IXLRangeRow row, Dictionary<string, int> headers)
        {
            var nome = GetValueAny(row, headers, "nome", "nomefantasia", "cliente");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                cliente.NomeFantasia = nome;
            }

            cliente.RG = NullIfWhite(GetValueAny(row, headers, "rg"));
            cliente.CPF = NormalizeDigitsOrNull(GetValueAny(
                row,
                headers,
                "cpf",
                "cnpj",
                "cpfcnpj",
                "cnpjcpf",
                "documento",
                "doc"));
            cliente.Nascimento = GetDateValueAny(row, headers, "nascimento");
            cliente.Sexo = NullIfWhite(GetValueAny(row, headers, "sexo"));
            cliente.Categoria = NullIfWhite(GetValueAny(row, headers, "categoria"));
            cliente.TipoContato = NullIfWhite(GetValueAny(row, headers, "tipocontato"));
            cliente.Contato = NullIfWhite(GetValueAny(row, headers, "contato"));
            cliente.Telefone1 = NormalizeDigitsOrNull(GetValueAny(row, headers, "telefone1", "telefone", "fone"));
            cliente.Telefone2 = NormalizeDigitsOrNull(GetValueAny(row, headers, "telefone2"));
            cliente.Telefone3 = NormalizeDigitsOrNull(GetValueAny(row, headers, "telefone3"));
            cliente.Email = NullIfWhite(GetValueAny(row, headers, "email", "email"));
            cliente.Endereco = NullIfWhite(GetValueAny(row, headers, "endereco"));
            cliente.Numero = NullIfWhite(GetValueAny(row, headers, "numero"));
            cliente.Complemento = NullIfWhite(GetValueAny(row, headers, "complemento"));
            cliente.Bairro = NullIfWhite(GetValueAny(row, headers, "bairro"));
            cliente.Cidade = NullIfWhite(GetValueAny(row, headers, "cidade"));
            cliente.UF = NullIfWhite(GetValueAny(row, headers, "uf"));
            cliente.CEP = NormalizeDigitsOrNull(GetValueAny(row, headers, "cep"));
            cliente.Observacoes = NullIfWhite(GetValueAny(row, headers, "observacoes"));
            cliente.VencimentoExtintores = GetDateValueAny(row, headers, "vencimentoextintores", "venc", "venc1");
            cliente.VencimentoAlvara = GetDateValueAny(row, headers, "vencimentoalvara", "venc2");
            cliente.Representante = NullIfWhite(GetValueAny(row, headers, "representante", "contato"));
            cliente.TipoServico = NullIfWhite(GetValueAny(row, headers, "tiposervico", "tipo"));
            cliente.StatusRecarga = NullIfWhite(GetValueAny(row, headers, "statusrecarga", "recargas"));
            cliente.Status = NormalizeAlvaraStatus(GetValueAny(row, headers, "status"));
            cliente.RazaoSocial = NullIfWhite(GetValueAny(row, headers, "razaosocial"));
            cliente.NumeroAlvara = NullIfWhite(GetValueAny(row, headers, "numeroalvara", "alvara"));
            cliente.IsAtivo = ParseActiveFlag(GetValueAny(row, headers, "ativo", "isativo"), cliente.IsAtivo);

            cliente.Documento = cliente.CPF;
            cliente.Telefone = cliente.Telefone1;
            cliente.VencimentoServico = cliente.VencimentoExtintores;
        }

        private static void ApplyMappedFields(Cliente target, Cliente source)
        {
            target.NomeFantasia = source.NomeFantasia;
            target.RazaoSocial = source.RazaoSocial;
            target.Documento = source.Documento;
            target.RG = source.RG;
            target.CPF = source.CPF;
            target.Nascimento = source.Nascimento;
            target.Sexo = source.Sexo;
            target.Categoria = source.Categoria;
            target.Contato = source.Contato;
            target.TipoContato = source.TipoContato;
            target.Telefone = source.Telefone;
            target.Telefone1 = source.Telefone1;
            target.Telefone2 = source.Telefone2;
            target.Telefone3 = source.Telefone3;
            target.Email = source.Email;
            target.Endereco = source.Endereco;
            target.Numero = source.Numero;
            target.Complemento = source.Complemento;
            target.Bairro = source.Bairro;
            target.Cidade = source.Cidade;
            target.UF = source.UF;
            target.CEP = source.CEP;
            target.Observacoes = source.Observacoes;
            target.TipoServico = source.TipoServico;
            target.StatusRecarga = source.StatusRecarga;
            target.VencimentoServico = source.VencimentoServico;
            target.VencimentoExtintores = source.VencimentoExtintores;
            target.NumeroAlvara = source.NumeroAlvara;
            target.VencimentoAlvara = source.VencimentoAlvara;
            target.Representante = source.Representante;
            target.IsAtivo = source.IsAtivo;
            target.Status = source.Status;
            target.AvisoAtivo = source.AvisoAtivo;
        }

        private static Dictionary<string, int> BuildHeaderMap(IXLRangeRow headerRow)
        {
            var map = new Dictionary<string, int>();
            var counterByHeader = new Dictionary<string, int>();

            foreach (var cell in headerRow.CellsUsed())
            {
                var baseName = NormalizeHeader(cell.GetString());
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    continue;
                }

                var index = cell.Address.ColumnNumber;

                if (!map.ContainsKey(baseName))
                {
                    map[baseName] = index;
                }

                counterByHeader.TryGetValue(baseName, out var current);
                current++;
                counterByHeader[baseName] = current;
                if (current > 1)
                {
                    map[$"{baseName}{current}"] = index;
                }
            }

            return map;
        }

        private static string? GetValueAny(IXLRangeRow row, Dictionary<string, int> headers, params string[] headerKeys)
        {
            foreach (var headerKey in headerKeys)
            {
                if (headers.TryGetValue(headerKey, out var col))
                {
                    var value = row.Cell(col).GetString();
                    return NullIfWhite(value);
                }
            }

            return null;
        }

        private static DateTime? GetDateValueAny(IXLRangeRow row, Dictionary<string, int> headers, params string[] headerKeys)
        {
            var col = 0;
            var found = false;
            foreach (var headerKey in headerKeys)
            {
                if (headers.TryGetValue(headerKey, out col))
                {
                    found = true;
                    break;
                }
            }

            if (!found) return null;

            var cell = row.Cell(col);
            if (cell.IsEmpty())
            {
                return null;
            }

            if (cell.TryGetValue<DateTime>(out var date))
            {
                return date.Date;
            }

            if (cell.TryGetValue<double>(out var serial))
            {
                try
                {
                    return DateTime.FromOADate(serial).Date;
                }
                catch
                {
                    return null;
                }
            }

            var raw = NullIfWhite(cell.GetString());
            if (raw == null)
            {
                return null;
            }

            if (DateTime.TryParseExact(raw, "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var parsedPtBr))
            {
                return parsedPtBr.Date;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedInvariant))
            {
                return parsedInvariant.Date;
            }

            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var serialAsText))
            {
                try
                {
                    return DateTime.FromOADate(serialAsText).Date;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static string NormalizeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value, "[^0-9]", string.Empty);
        }

        private static string? NormalizeDigitsOrNull(string? value)
        {
            var digits = NormalizeDigits(value);
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }

        private static string BuildDedupKey(string? nome, string? cpf, string? telefone1, string? email, string? cidade, ImportResult result)
        {
            var nomeNorm = NullIfWhite(nome)?.Trim().ToUpperInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nomeNorm))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(cpf))
            {
                return $"CPF:{cpf}";
            }

            var telNorm = NormalizeDigits(telefone1);
            if (!string.IsNullOrWhiteSpace(telNorm))
            {
                return $"NT:{nomeNorm}|{telNorm}";
            }

            var emailNorm = NullIfWhite(email)?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(emailNorm))
            {
                result.FallbackLogs.Add($"Fallback Nome+Email: {nomeNorm}");
                return $"NE:{nomeNorm}|{emailNorm}";
            }

            var cidadeNorm = NullIfWhite(cidade)?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(cidadeNorm))
            {
                result.FallbackLogs.Add($"Fallback Nome+Cidade: {nomeNorm}");
                return $"NC:{nomeNorm}|{cidadeNorm}";
            }

            return string.Empty;
        }

        private static string BuildLookupKeyForExisting(Cliente cliente)
        {
            var nomeNorm = NullIfWhite(cliente.NomeFantasia)?.Trim().ToUpperInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nomeNorm))
            {
                return string.Empty;
            }

            var telNorm = NormalizeDigits(cliente.Telefone1);
            if (!string.IsNullOrWhiteSpace(telNorm))
            {
                return $"NT:{nomeNorm}|{telNorm}";
            }

            var emailNorm = NullIfWhite(cliente.Email)?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(emailNorm))
            {
                return $"NE:{nomeNorm}|{emailNorm}";
            }

            var cidadeNorm = NullIfWhite(cliente.Cidade)?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(cidadeNorm))
            {
                return $"NC:{nomeNorm}|{cidadeNorm}";
            }

            return string.Empty;
        }

        private static IXLWorksheet? FindWorksheetWithNomeHeader(XLWorkbook workbook, out Dictionary<string, int>? headers)
        {
            headers = null;

            foreach (var worksheet in workbook.Worksheets)
            {
                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    continue;
                }

                var map = BuildHeaderMap(usedRange.FirstRow());
                if (map.ContainsKey("nome") || map.ContainsKey("nomefantasia") || map.ContainsKey("cliente"))
                {
                    headers = map;
                    return worksheet;
                }
            }

            return null;
        }

        private static void AddSkip(ImportResult result, string reason)
        {
            result.Skipped++;
            result.SkippedReasons.Add(reason);
        }

        private static string NormalizeHeader(string header)
        {
            var input = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in input)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        private static bool ParseActiveFlag(string? rawValue, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue;
            }

            var normalized = NormalizeToken(rawValue);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return defaultValue;
            }

            var falseTokens = new[]
            {
                "0", "false", "nao", "n", "inativo", "desativado", "desligado", "off", "cancelado", "encerrado"
            };

            var trueTokens = new[]
            {
                "1", "true", "sim", "s", "ativo", "ativado", "ligado", "on", "ok", "regular", "emdia"
            };

            if (falseTokens.Contains(normalized))
            {
                return false;
            }

            if (trueTokens.Contains(normalized))
            {
                return true;
            }

            if (normalized.Contains("inativ") ||
                normalized.Contains("desativ") ||
                normalized.Contains("deslig") ||
                normalized.Contains("cancel") ||
                normalized.Contains("encerr"))
            {
                return false;
            }

            if (normalized.Contains("ativ") ||
                normalized.Contains("ligad") ||
                normalized.Contains("regular") ||
                normalized.Contains("emdia"))
            {
                return true;
            }

            // Em planilhas antigas, marcadores visuais (ex.: "-", ".", "--") indicam "sem marcação".
            // Mantém o valor anterior para evitar classificação indevida.
            if (normalized.Trim('-', '.', '_').Length == 0)
            {
                return defaultValue;
            }

            return defaultValue;
        }

        private static string? NormalizeAlvaraStatus(string? rawValue)
        {
            var value = NullIfWhite(rawValue);
            if (value == null)
            {
                return null;
            }

            var normalized = NormalizeToken(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var emptyTokens = new[]
            {
                "-", "--", "---", "na", "nainformado", "naoinformado", "semstatus", "seminformacao", "seminfo", "vazio"
            };

            if (emptyTokens.Contains(normalized))
            {
                return null;
            }

            return value;
        }

        private static string NormalizeToken(string rawValue)
        {
            var trimmed = rawValue.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(trimmed.Length);
            foreach (var c in trimmed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string? NullIfWhite(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

