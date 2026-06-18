namespace Hexalith.Timesheets.Server.References;

public sealed record ReferenceValidationResult(ReferenceValidationState State, string Reason)
{
    public bool IsValid => State == ReferenceValidationState.Valid;

    public static ReferenceValidationResult Invalid(string reason)
    {
        return Denied(ReferenceValidationState.InvalidReference, reason);
    }

    public static ReferenceValidationResult Denied(
        ReferenceValidationState state,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (state == ReferenceValidationState.Valid)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Denied reference validation requires a denial state.");
        }

        return new(state, reason);
    }

    public static ReferenceValidationResult Valid()
    {
        return new(ReferenceValidationState.Valid, "valid");
    }
}
