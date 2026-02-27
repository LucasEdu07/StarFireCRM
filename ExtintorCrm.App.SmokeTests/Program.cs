using System;
using System.Collections.Generic;
using System.IO;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Backup;
using ExtintorCrm.App.Infrastructure.Import;
using ExtintorCrm.App.UseCases.Alerts;
using ExtintorCrm.App.UseCases.Common;

namespace ExtintorCrm.App.SmokeTests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            SmokeTests.RunAll();
            Console.WriteLine("OK: 12/12 smoke tests passaram.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FALHA nos smoke tests:");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

internal static class AssertEx
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}. Esperado: {expected} | Atual: {actual}");
        }
    }
}

internal static class SmokeTests
{
    public static void RunAll()
    {
        AlertRules_SetAlertDays_SortsDistinctAndFiltersInvalid();
        AlertService_ApplyAlerts_Clientes_ComputaSituacao();
        AlertService_Counts_Clientes_ComputaVencidosEVencendo();
        AlertService_ApplyAlerts_Pagamentos_ComputaSituacao();
        AlertService_CountPagamentos_ConsideraApenasAbertos();
        ValidationResult_IsInvalid_WhenAnyErrorExists();
        OperationResult_NormalizesCodeAndDetails();
        ImportResult_ToOperationResult_WithErrorsFlagsFailure();
        ImportValidation_ValidateSourceFile_RejectsUnsupportedExtension();
        ImportValidation_EnsureRequiredHeaders_AddsErrorsForMissingHeaders();
        BackupService_TryCreateBackupAsync_ReturnsFailureWhenFolderMissing();
        BackupService_TryRestoreBackupAsync_ReturnsFailureWhenFileMissing();
    }

    private static void AlertRules_SetAlertDays_SortsDistinctAndFiltersInvalid()
    {
        var rules = new AlertRules();
        rules.SetAlertDays(30, 7, 0, -2, 15, 7, 30);

        AssertEx.Equal(3, rules.AlertDays.Length, "AlertDays deve conter apenas positivos distintos");
        AssertEx.Equal(7, rules.AlertDays[0], "AlertDays deve estar ordenado");
        AssertEx.Equal(15, rules.AlertDays[1], "AlertDays deve estar ordenado");
        AssertEx.Equal(30, rules.AlertDays[2], "AlertDays deve estar ordenado");
        AssertEx.Equal(30, rules.MaxAlertDays, "MaxAlertDays incorreto");
    }

    private static void AlertService_ApplyAlerts_Clientes_ComputaSituacao()
    {
        var today = new DateTime(2026, 2, 23);
        var rules = new AlertRules();
        rules.SetAlertDays(7, 15, 30);
        var service = new AlertService(rules);

        var cliente = new Cliente
        {
            NomeFantasia = "Cliente Teste",
            VencimentoExtintores = today.AddDays(-1),
            VencimentoAlvara = today.AddDays(5)
        };

        service.ApplyAlerts(new[] { cliente }, today);

        AssertEx.Equal("Vencido", cliente.ExtintorStatus, "Status do extintor incorreto");
        AssertEx.Equal(-1, cliente.ExtintorDaysToDue ?? 0, "Dias do extintor incorreto");
        AssertEx.Equal("Vencendo", cliente.AlvaraStatus, "Status do alvara incorreto");
        AssertEx.Equal(5, cliente.AlvaraDaysToDue ?? -1, "Dias do alvara incorreto");
        AssertEx.Equal("Vencido", cliente.SituacaoNivel, "Situacao geral incorreta");
        AssertEx.Equal("Vencido", cliente.SituacaoTexto, "Texto da situacao geral incorreto");
    }

    private static void AlertService_Counts_Clientes_ComputaVencidosEVencendo()
    {
        var rules = new AlertRules();
        var service = new AlertService(rules);

        var clientes = new[]
        {
            new Cliente { ExtintorStatus = "Vencido", AlvaraStatus = "OK" },
            new Cliente { ExtintorStatus = "Vencendo", AlvaraStatus = "Vencendo" },
            new Cliente { ExtintorStatus = "OK", AlvaraStatus = "Vencido" }
        };

        var ext = service.CountExtintores(clientes);
        var alv = service.CountAlvaras(clientes);

        AssertEx.Equal(1, ext.Vencidos, "Contagem de extintores vencidos incorreta");
        AssertEx.Equal(1, ext.Vencendo, "Contagem de extintores vencendo incorreta");
        AssertEx.Equal(1, alv.Vencidos, "Contagem de alvaras vencidos incorreta");
        AssertEx.Equal(1, alv.Vencendo, "Contagem de alvaras vencendo incorreta");
    }

    private static void AlertService_ApplyAlerts_Pagamentos_ComputaSituacao()
    {
        var today = new DateTime(2026, 2, 23);
        var rules = new AlertRules();
        rules.SetAlertDays(30);
        var service = new AlertService(rules);

        var pagamentos = new[]
        {
            new Pagamento { Pago = true, DataVencimento = today.AddDays(-40) },
            new Pagamento { Pago = false, DataVencimento = today.AddDays(-2) },
            new Pagamento { Pago = false, DataVencimento = today.AddDays(10) },
            new Pagamento { Pago = false, DataVencimento = today.AddDays(45) }
        };

        service.ApplyAlerts(pagamentos, today);

        AssertEx.Equal("OK", pagamentos[0].SituacaoNivel, "Pagamento pago deve ficar OK");
        AssertEx.Equal("Pago", pagamentos[0].SituacaoTexto, "Pagamento pago deve ficar com texto Pago");

        AssertEx.Equal("Vencido", pagamentos[1].SituacaoNivel, "Pagamento vencido incorreto");
        AssertEx.Equal(-2, pagamentos[1].DaysToDue ?? 0, "DaysToDue vencido incorreto");

        AssertEx.Equal("Vencendo", pagamentos[2].SituacaoNivel, "Pagamento vencendo incorreto");
        AssertEx.Equal("Vence em 10 dias", pagamentos[2].SituacaoTexto, "Texto do pagamento vencendo incorreto");

        AssertEx.Equal("OK", pagamentos[3].SituacaoNivel, "Pagamento fora da janela deve ser OK");
    }

    private static void AlertService_CountPagamentos_ConsideraApenasAbertos()
    {
        var service = new AlertService(new AlertRules());
        var pagamentos = new[]
        {
            new Pagamento { Pago = false, SituacaoNivel = "Vencido" },
            new Pagamento { Pago = false, SituacaoNivel = "Vencendo" },
            new Pagamento { Pago = true, SituacaoNivel = "Vencido" }
        };

        var result = service.CountPagamentos(pagamentos);

        AssertEx.Equal(1, result.Vencidos, "CountPagamentos nao deve incluir pagos em vencidos");
        AssertEx.Equal(1, result.Vencendo, "CountPagamentos vencendo incorreto");
    }

    private static void ValidationResult_IsInvalid_WhenAnyErrorExists()
    {
        var validation = new ValidationResult();
        validation.AddWarning("Arquivo", "extension", "Formato incomum.");
        AssertEx.True(validation.IsValid, "ValidationResult nao deveria falhar apenas com warning.");

        validation.AddError("Arquivo", "required", "Arquivo nao informado.");
        AssertEx.True(!validation.IsValid, "ValidationResult deveria falhar ao receber erro.");
        AssertEx.Equal(2, validation.Issues.Count, "Quantidade de issues de validacao incorreta.");
    }

    private static void OperationResult_NormalizesCodeAndDetails()
    {
        var result = OperationResult.Success(
            title: "Teste",
            message: "Operacao concluida",
            code: " import_ok ",
            details: new[] { "  detalhe  ", "", "outro" });

        AssertEx.True(result.IsSuccess, "OperationResult deveria ser de sucesso.");
        AssertEx.Equal("IMPORT_OK", result.Code, "Codigo da operacao deveria ser normalizado.");
        AssertEx.Equal(2, result.Details.Count, "Detalhes deveriam remover entradas vazias.");
    }

    private static void ImportResult_ToOperationResult_WithErrorsFlagsFailure()
    {
        var result = new ImportResult
        {
            Inserted = 1,
            Updated = 0,
            Skipped = 2
        };
        result.Errors.Add("Linha 7: CPF invalido");
        result.SkippedReasons.Add("CPF/CNPJ invalido");
        result.Validation.AddError("Cabecalho", "required", "Cabecalho obrigatorio ausente: cpfcnpj.");

        var operation = result.ToOperationResult("clientes");

        AssertEx.True(!operation.IsSuccess, "Importacao com erro deveria retornar falha.");
        AssertEx.Equal("IMPORT_WITH_ERRORS", operation.Code, "Codigo de importacao com erro incorreto.");
        AssertEx.True(operation.Details.Count > 0, "Operacao deveria trazer detalhes para troubleshooting.");
    }

    private static void ImportValidation_ValidateSourceFile_RejectsUnsupportedExtension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"smoke-import-{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempFile, "conteudo");

        try
        {
            var validation = ImportValidation.ValidateSourceFile(tempFile, ".xlsx", ".xlsm");
            AssertEx.True(!validation.IsValid, "Arquivo com extensao nao suportada deveria ser invalido.");
            AssertEx.True(validation.Issues.Count > 0, "Validacao deveria registrar issue para extensao invalida.");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void ImportValidation_EnsureRequiredHeaders_AddsErrorsForMissingHeaders()
    {
        var validation = new ValidationResult();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpfcnpj"] = 1
        };

        ImportValidation.EnsureRequiredHeaders(validation, headers, "cpfcnpj", "descricao");

        AssertEx.True(!validation.IsValid, "Validacao deveria falhar quando cabecalho obrigatorio estiver ausente.");
        AssertEx.Equal(1, validation.Issues.Count, "Deveria existir apenas um erro de cabecalho ausente.");
        AssertEx.Equal("required", validation.Issues[0].Rule, "Regra do erro de cabecalho deveria ser 'required'.");
    }

    private static void BackupService_TryCreateBackupAsync_ReturnsFailureWhenFolderMissing()
    {
        var service = new BackupService();
        var operation = service.TryCreateBackupAsync(string.Empty).GetAwaiter().GetResult();

        AssertEx.True(!operation.IsSuccess, "Backup sem pasta deveria retornar falha.");
        AssertEx.Equal("BACKUP_FOLDER_REQUIRED", operation.Code, "Codigo de erro de backup sem pasta incorreto.");
    }

    private static void BackupService_TryRestoreBackupAsync_ReturnsFailureWhenFileMissing()
    {
        var service = new BackupService();
        var operation = service.TryRestoreBackupAsync("arquivo-inexistente.zip").GetAwaiter().GetResult();

        AssertEx.True(!operation.IsSuccess, "Restore com arquivo inexistente deveria retornar falha.");
        AssertEx.Equal("BACKUP_FILE_NOT_FOUND", operation.Code, "Codigo de erro de restore inexistente incorreto.");
    }
}
