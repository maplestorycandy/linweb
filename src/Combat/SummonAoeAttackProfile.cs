namespace IdleLineage.Combat;

public sealed record SummonAoeAttackProfile(double Chance, string Name, string Element, int DiceCount, int DiceSides, double FlatDamage, double DamageMultiplier, double MagicResistancePenetration, double AreaRadius);
