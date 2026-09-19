namespace IdleLineage.Combat;

public readonly record struct L1jConsumableSpec(int ItemId, ConsumableKind Kind, int HealingBase = 0, string Effect = "", double DurationSeconds = 0.0, double MaximumDurationSeconds = 0.0, bool AddsDuration = false, string RequiredClass = "");
