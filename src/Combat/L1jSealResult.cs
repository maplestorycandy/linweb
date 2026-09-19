namespace IdleLineage.Combat;

public readonly record struct L1jSealResult(bool Attempted, L1jSealFailure Failure, string TargetItemKey, string TargetUid, bool Sealed);
