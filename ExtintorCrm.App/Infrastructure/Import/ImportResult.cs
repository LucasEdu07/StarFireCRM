using System;
using System.Collections.Generic;
using System.Linq;
using ExtintorCrm.App.UseCases.Common;

namespace ExtintorCrm.App.Infrastructure.Import;

public class ImportResult
{
    public int TotalRowsRead { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int BlankRowsIgnored { get; set; }
    public List<string> SkippedReasons { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> FallbackLogs { get; } = new();
    public ValidationResult Validation { get; } = new();

    public IReadOnlyDictionary<string, int> SkippedReasonCounts =>
        SkippedReasons
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    public bool HasBlockingErrors =>
        Errors.Count > 0 ||
        Validation.Issues.Any(x => x.Severity == ValidationSeverity.Error);

    public OperationResult ToOperationResult(string entityName)
    {
        var entity = string.IsNullOrWhiteSpace(entityName) ? "registros" : entityName.Trim().ToLowerInvariant();
        var message = $"Importacao de {entity} concluida: {Inserted} inseridos, {Updated} atualizados, {Skipped} ignorados.";
        if (BlankRowsIgnored > 0)
        {
            message = $"{message} Linhas vazias ignoradas: {BlankRowsIgnored}.";
        }

        var details = new List<string>();
        foreach (var issue in Validation.Issues.Where(x => x.Severity == ValidationSeverity.Error).Take(5))
        {
            details.Add($"Validacao [{issue.Field}/{issue.Rule}]: {issue.Message}");
        }

        foreach (var reason in SkippedReasonCounts.Take(5))
        {
            details.Add($"Motivo ignorado: {reason.Key} ({reason.Value})");
        }

        foreach (var error in Errors.Take(5))
        {
            details.Add($"Erro: {error}");
        }

        if (HasBlockingErrors)
        {
            return OperationResult.Failure(
                title: $"Importacao de {entity} com pendencias",
                message: message,
                code: "IMPORT_WITH_ERRORS",
                nextStep: "Revise os erros e os motivos ignorados antes de repetir a importacao.",
                details: details);
        }

        if (Skipped > 0 || Validation.Issues.Any(x => x.Severity == ValidationSeverity.Warning))
        {
            return OperationResult.Success(
                title: $"Importacao de {entity} concluida com avisos",
                message: message,
                code: "IMPORT_WITH_WARNINGS",
                nextStep: "Confira os itens ignorados para ajustar os dados de origem.",
                details: details);
        }

        return OperationResult.Success(
            title: $"Importacao de {entity} concluida",
            message: message,
            code: "IMPORT_OK",
            nextStep: "Operacao finalizada. Siga para a conferencia na lista principal.",
            details: details);
    }
}
