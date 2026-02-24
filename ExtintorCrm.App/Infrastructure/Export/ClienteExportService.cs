using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.Infrastructure.Export
{
    public sealed class ClienteExportService
    {
        public Task<string> ExportAsync(IEnumerable<Cliente> clientes, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("Arquivo de exportação não informado.");
            }

            var list = (clientes ?? Enumerable.Empty<Cliente>()).ToList();
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            if (extension == ".xlsx")
            {
                ExportXlsx(list, filePath);
                return Task.FromResult(filePath);
            }

            ExportCsv(list, filePath);
            return Task.FromResult(filePath);
        }

        private static void ExportCsv(IReadOnlyCollection<Cliente> clientes, string filePath)
        {
            EnsureOutputDirectory(filePath);

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            writer.WriteLine("Cliente,CPF/CNPJ,Contato,Telefone,Aviso,Venc.,Recargas,Tipo,Alvará,Venc. (Alvará),Status,Razão Social,Endereço");

            foreach (var cliente in clientes)
            {
                writer.WriteLine(string.Join(",",
                    Escape(cliente.NomeFantasia),
                    Escape(cliente.CPF),
                    Escape(cliente.Contato),
                    Escape(cliente.Telefone1 ?? cliente.Telefone),
                    Escape(cliente.IsAtivo ? "Ativo" : "Inativo"),
                    Escape(FormatDate(cliente.VencimentoExtintores)),
                    Escape(cliente.StatusRecarga),
                    Escape(cliente.TipoServico),
                    Escape(cliente.NumeroAlvara),
                    Escape(FormatDate(cliente.VencimentoAlvara)),
                    Escape(cliente.Status),
                    Escape(cliente.RazaoSocial),
                    Escape(cliente.Endereco)));
            }
        }

        private static void ExportXlsx(IReadOnlyCollection<Cliente> clientes, string filePath)
        {
            EnsureOutputDirectory(filePath);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Clientes");

            ws.Cell(1, 1).Value = "Cliente";
            ws.Cell(1, 2).Value = "CPF/CNPJ";
            ws.Cell(1, 3).Value = "Contato";
            ws.Cell(1, 4).Value = "Telefone";
            ws.Cell(1, 5).Value = "Aviso";
            ws.Cell(1, 6).Value = "Venc.";
            ws.Cell(1, 7).Value = "Recargas";
            ws.Cell(1, 8).Value = "Tipo";
            ws.Cell(1, 9).Value = "Alvará";
            ws.Cell(1, 10).Value = "Venc. (Alvará)";
            ws.Cell(1, 11).Value = "Status";
            ws.Cell(1, 12).Value = "Razão Social";
            ws.Cell(1, 13).Value = "Endereço";

            var row = 2;
            foreach (var cliente in clientes)
            {
                ws.Cell(row, 1).Value = cliente.NomeFantasia ?? string.Empty;
                ws.Cell(row, 2).Value = cliente.CPF ?? string.Empty;
                ws.Cell(row, 3).Value = cliente.Contato ?? string.Empty;
                ws.Cell(row, 4).Value = cliente.Telefone1 ?? cliente.Telefone ?? string.Empty;
                ws.Cell(row, 5).Value = cliente.IsAtivo ? "Ativo" : "Inativo";
                ws.Cell(row, 6).Value = FormatDate(cliente.VencimentoExtintores);
                ws.Cell(row, 7).Value = cliente.StatusRecarga ?? string.Empty;
                ws.Cell(row, 8).Value = cliente.TipoServico ?? string.Empty;
                ws.Cell(row, 9).Value = cliente.NumeroAlvara ?? string.Empty;
                ws.Cell(row, 10).Value = FormatDate(cliente.VencimentoAlvara);
                ws.Cell(row, 11).Value = cliente.Status ?? string.Empty;
                ws.Cell(row, 12).Value = cliente.RazaoSocial ?? string.Empty;
                ws.Cell(row, 13).Value = cliente.Endereco ?? string.Empty;
                row++;
            }

            ws.Range(1, 1, 1, 13).Style.Font.Bold = true;
            ws.Columns(1, 13).AdjustToContents();
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

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : string.Empty;
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
}
