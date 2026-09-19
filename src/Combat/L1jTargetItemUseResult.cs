namespace IdleLineage.Combat;

public readonly record struct L1jTargetItemUseResult(bool Attempted, bool Succeeded, L1jTargetItemUseFailure Failure, int SourceItemId, int TargetItemId, int OutputItemId, string OutputItemKey);
