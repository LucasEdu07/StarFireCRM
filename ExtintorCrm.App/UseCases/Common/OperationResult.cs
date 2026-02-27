using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtintorCrm.App.UseCases.Common;

public sealed class OperationResult
{
    private static readonly IReadOnlyList<string> EmptyDetails = Array.Empty<string>();

    private OperationResult(
        bool isSuccess,
        string title,
        string message,
        string code,
        string nextStep,
        IReadOnlyList<string> details)
    {
        IsSuccess = isSuccess;
        Title = title;
        Message = message;
        Code = code;
        NextStep = nextStep;
        Details = details;
    }

    public bool IsSuccess { get; }
    public string Title { get; }
    public string Message { get; }
    public string Code { get; }
    public string NextStep { get; }
    public IReadOnlyList<string> Details { get; }

    public static OperationResult Success(
        string title,
        string message,
        string code,
        string? nextStep = null,
        IEnumerable<string>? details = null)
    {
        return new OperationResult(
            isSuccess: true,
            title: title?.Trim() ?? string.Empty,
            message: message?.Trim() ?? string.Empty,
            code: NormalizeCode(code),
            nextStep: nextStep?.Trim() ?? string.Empty,
            details: NormalizeDetails(details));
    }

    public static OperationResult Failure(
        string title,
        string message,
        string code,
        string? nextStep = null,
        IEnumerable<string>? details = null)
    {
        return new OperationResult(
            isSuccess: false,
            title: title?.Trim() ?? string.Empty,
            message: message?.Trim() ?? string.Empty,
            code: NormalizeCode(code),
            nextStep: nextStep?.Trim() ?? string.Empty,
            details: NormalizeDetails(details));
    }

    private static string NormalizeCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? "UNKNOWN"
            : code.Trim().ToUpperInvariant();
    }

    private static IReadOnlyList<string> NormalizeDetails(IEnumerable<string>? details)
    {
        if (details == null)
        {
            return EmptyDetails;
        }

        var normalized = details
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        return normalized.Count == 0 ? EmptyDetails : normalized;
    }
}
