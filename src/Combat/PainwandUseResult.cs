namespace IdleLineage.Combat;

public sealed record PainwandUseResult(bool Success, PainwandFailure Failure, string MobKey, int RemainingCharges);
