namespace IdleLineage.Combat;

public sealed record SummonProcProfile(SummonProcKind Kind, double Chance, string Name, string Element, int DiceCount, int DiceSides, double FlatDamage, double DamageMultiplier, bool Slow, bool Stun, double AreaRadius);
