namespace IdleLineage.Combat;

public sealed class DerivedStats
{
	public double Str;

	public double Dex;

	public double Con;

	public double Int;

	public double Wis;

	public double Cha;

	public double AttackInterval = 0.1;

	public double Hit;

	public double MeleeDamage;

	public double RangedDamage;

	public double MagicDamage;

	public double MagicHit;

	public double MagicCritical;

	public double MagicCriticalDamage = 50.0;

	public double IntelligenceSpellPower;

	public double ItemSpellPower;

	public double ResistFire;

	public double ResistWater;

	public double ResistWind;

	public double ResistEarth;

	public double ArmorClass;

	public double MagicResist;

	public double HealthRegenMaximum;

	public double HealthRegenFlat;

	public double ManaRegen;

	public double HealthRegenIntervalReductionSeconds;

	public double LowManaRegenBonus;

	public double MeleeHit;

	public double RangedHit;

	public double ExtraHit;

	public double ExtraDamage;

	public double MeleeCritical;

	public double RangedCritical;

	public double MeleeCriticalDamage = 50.0;

	public double RangedCriticalDamage = 50.0;

	public double ItemDropRatePercent;

	public double GoldDropAmountPercent;

	public double DamageReduction;

	public double EvasionRating;

	public double MeleeEvasion;

	public int AttackDiceSmall = 1;

	public int AttackDiceLarge = 1;

	public int OriginalMagicHit;

	public int OriginalMagicCritical;

	public int OriginalMagicDamage;

	public int OriginalManaCostReduction;

	public int OriginalHealthRegen;

	public int OriginalManaRegen;

	public int OriginalWeightReduction;

	public bool UsesRangedAttack;

	public int HitstunTicks = 5;

	public int CastLockTicks = 12;
}
