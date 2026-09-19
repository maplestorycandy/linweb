namespace IdleLineage.Combat;

public readonly record struct L1jIdentifyResult(bool Attempted, L1jIdentifyFailure Failure, string TargetItemKey, string TargetUid, bool NewlyIdentified);
