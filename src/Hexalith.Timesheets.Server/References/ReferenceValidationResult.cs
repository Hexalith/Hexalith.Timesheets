namespace Hexalith.Timesheets.Server.References;

public sealed record ReferenceValidationResult(bool IsValid, string Reason)
{
    public static ReferenceValidationResult Invalid(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(false, reason);
    }

    public static ReferenceValidationResult Valid()
    {
        return new(true, "valid");
    }
}
