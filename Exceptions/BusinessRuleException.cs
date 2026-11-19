namespace CovStudent.Exceptions;

/// <summary>
/// Levée quand une règle métier (RM-xx) n'est pas respectée.
/// RuleId permet de tracer la règle dans les tests et la matrice de traçabilité.
/// </summary>
public class BusinessRuleException : Exception
{
    public string RuleId { get; }

    public BusinessRuleException(string ruleId, string message) : base(message)
    {
        RuleId = ruleId;
    }
}