namespace IdleLineage.Combat;

public readonly record struct CollectionCategoryProgress(CollectionBookKind Book, string Key, string Name, string Group, int Collected, int Total, int Tier, string BonusStat, double BonusValue, string BonusLabel, bool BonusActive);
