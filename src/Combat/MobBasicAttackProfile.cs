namespace IdleLineage.Combat;

public sealed record MobBasicAttackProfile(MobBasicAttackKind Kind, double Range, string ProjectileKind, int MagicDiceCount, int MagicDiceSides, double MagicFlatDamage)
{
	public bool UsesMagicDamage => Kind == MobBasicAttackKind.Magic;

	public bool UsesRangedPhysicalDamage => Kind == MobBasicAttackKind.RangedPhysical;
}
