namespace IdleLineage.Combat;

public readonly record struct SetTierState(string Code, string DisplayName, int RequiredPieces, int EquippedPieces, string Description, bool Active);
