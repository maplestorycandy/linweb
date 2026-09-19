namespace IdleLineage.Combat;

public readonly record struct RelicWeaponSpellProc(string EffectId, double ChancePercent, int FixDamage, int RandomDamage, string Element, int AreaCells, int DiceCount = 0, int DiceSides = 0, string StatusKind = "", double StatusChancePercent = 0.0, int StatusDurationTicks = 0);
