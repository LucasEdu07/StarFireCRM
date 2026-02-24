using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace ExtintorCrm.App.Infrastructure.Export
{
    public sealed class TabularExportService
    {
        public Task<string> ExportAsync<T>(
            IEnumerable<T> rows,
            IReadOnlyList<ExportColumnDefinition<T>> columns,
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("Arquivo de exportação não informado.");
            }

            if (columns == null || columns.Count == 0)
            {
                throw new InvalidOperationException("Selecione ao menos um campo para exportação.");
            }

            var list = (rows ?? Enumerable.Empty<T>()).ToList();
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            if (extension == ".xlsx")
            {
                ExportXlsx(list, columns, filePath);
                return Task.FromResult(filePath);
            }

            ExportCsv(list, columns, filePath);
            return Task.FromResult(filePath);
        }

        private static void ExportCsv<T>(IReadOnlyCollection<T> rows, IReadOnlyList<ExportColumnDefinition<T>> columns, string filePath)
        {
            EnsureOutputDirectory(filePath);

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            writer.WriteLine(string.Join(",", columns.Select(c => Escape(c.Header))));

            foreach (var row in rows)
            {
                var values = columns.Select(c => Escape(c.ValueSelector(row)));
                writer.WriteLine(string.Join(",", values));
            }
        }

        private static void ExportXlsx<T>(IReadOnlyCollection<T> rows, IReadOnlyList<ExportColumnDefinition<T>> columns, string filePath)
        {
            EnsureOutputDirectory(filePath);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Exportacao");

            for (var i = 0; i < columns.Count; i++)
            {
                ws.Cell(1, i + 1).Value = columns[i].Header;
            }

            var rowIndex = 2;
            foreach (var row in rows)
            {
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    ws.Cell(rowIndex, colIndex + 1).Value = columns[colIndex].ValueSelector(row) ?? string.Empty;
                }

                rowIndex++;
            }

            ws.Range(1, 1, 1, columns.Count).Style.Font.Bold = true;
            ws.Columns(1, columns.Count).AdjustToContents();
            workbook.SaveAs(filePath);
        }

        private static void EnsureOutputDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static string Escape(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('"'))
            {
                text = text.Replace("\"", "\"\"");
            }

            if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            {
                return $"\"{text}\"";
            }

            return text;
        }
    }

    public sealed class ExportColumnDefinition<T>
    {
        public ExportColumnDefinition(string key, string header, Func<T, string> valueSelector)
        {
            Key = key;
            Header = header;
            ValueSelector = valueSelector;
        }

        public string Key { get; }
        public string Header { get; }
        public Func<T, string> ValueSelector { get; }
    }
}
