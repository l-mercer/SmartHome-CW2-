namespace SmartHome.Core.Validation;

public record ValidationResult(
    bool IsValid,
    List<string> Errors
)
{
    public static ValidationResult Success() => new(true, new List<string>());
    public static ValidationResult Failure(params string[] errors) => new(false, errors.ToList());
    public static ValidationResult Failure(List<string> errors) => new(false, errors);
}


