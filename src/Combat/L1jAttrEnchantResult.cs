namespace IdleLineage.Combat;

public readonly record struct L1jAttrEnchantResult(bool Attempted, L1jAttrEnchantFailure Failure, string TargetItemKey, string TargetUid, int Roll, bool Succeeded, int Kind, int Level);
