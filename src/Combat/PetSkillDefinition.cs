namespace IdleLineage.Combat;

public sealed record PetSkillDefinition(string Id, string Name, PetSkillKind Kind, double ManaCost, int DiceCount, int DiceSides, string Element, bool AreaOfEffect, double AreaRadius, double Weight, bool DrainHalf, bool ForceCritical, double BonusDamage, string Debuff, double Accuracy, string Dot, double DamagePerSecond, double DurationSeconds, double FreezeChance, double DamageReduction);
