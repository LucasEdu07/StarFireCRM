using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtintorCrm.App.UseCases.Common;

namespace ExtintorCrm.App.Infrastructure.Import;

public static class ImportValidation
{
    public static ValidationResult ValidateSourceFile(string? filePath, params string[] allowedExtensions)
    {
        var validation = new ValidationResult();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            validation.AddError("Arquivo", "required", "Arquivo nao informado.");
            return validation;
        }

        if (!File.Exists(filePath))
        {
            validation.AddError("Arquivo", "exists", "Arquivo nao encontrado.");
            return validation;
        }

        if (allowedExtensions == null || allowedExtensions.Length == 0)
        {
            return validation;
        }

        var extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
        var allowed = new HashSet<string>(
            allowedExtensions
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        if (!allowed.Contains(extension))
        {
            validation.AddError(
                "Arquivo",
                "extension",
                $"Formato nao suportado ({extension}). Formatos aceitos: {string.Join(", ", allowed.OrderBy(x => x))}.");
        }

        return validation;
    }

    public static void EnsureRequiredHeaders(
        ValidationResult validation,
        IReadOnlyDictionary<string, int> headers,
        params string[] requiredKeys)
    {
        foreach (var requiredKey in requiredKeys)
        {
            if (headers.ContainsKey(requiredKey))
            {
                continue;
            }

            validation.AddError(
                "Cabecalho",
                "required",
                $"Cabecalho obrigatorio ausente: {requiredKey}.");
        }
    }
}
