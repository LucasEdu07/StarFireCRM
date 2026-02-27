using System.Collections.Generic;
using System.Linq;

namespace ExtintorCrm.App.UseCases.Common;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ValidationIssue(
    string Field,
    string Rule,
    ValidationSeverity Severity,
    string Message);

public sealed class ValidationResult
{
    private readonly List<ValidationIssue> _issues = new();

    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public bool IsValid => _issues.All(x => x.Severity != ValidationSeverity.Error);

    public void AddIssue(string field, string rule, ValidationSeverity severity, string message)
    {
        _issues.Add(new ValidationIssue(
            field?.Trim() ?? string.Empty,
            rule?.Trim() ?? string.Empty,
            severity,
            message?.Trim() ?? string.Empty));
    }

    public void AddError(string field, string rule, string message)
    {
        AddIssue(field, rule, ValidationSeverity.Error, message);
    }

    public void AddWarning(string field, string rule, string message)
    {
        AddIssue(field, rule, ValidationSeverity.Warning, message);
    }

    public void AddInfo(string field, string rule, string message)
    {
        AddIssue(field, rule, ValidationSeverity.Info, message);
    }

    public void Merge(ValidationResult? other)
    {
        if (other == null || other._issues.Count == 0)
        {
            return;
        }

        _issues.AddRange(other._issues);
    }
}
