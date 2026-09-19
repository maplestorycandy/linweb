namespace IdleLineage.Combat;

public readonly record struct MonsterCompanionPotionUseResult(bool Success, string ItemKey = "", string SourceItemKey = "", double CalculatedHealing = 0.0, double HpRestored = 0.0);
