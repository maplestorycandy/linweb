namespace IdleLineage.Combat;

public readonly record struct IvoryQuiverUseResult(bool Success, IvoryQuiverFailure Failure, long RewardQuantity = 0L, long RemainingCooldownSeconds = 0L);
