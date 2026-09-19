namespace IdleLineage.Combat;

public sealed record MagicDollDefinition(string ItemKey, string Name, int L1jItemId, int NpcId, int Type, int Gfx)
{
	public MagicDollAbility Ability => AbilityOf(Type);

	internal static MagicDollAbility AbilityOf(int type)
	{
		switch (type)
		{
		case 0:
			return MagicDollAbility.WeightRelief;
		case 1:
		case 3:
			return MagicDollAbility.ManaRegen;
		case 2:
		case 4:
			return MagicDollAbility.AttackDamage;
		case 5:
			return MagicDollAbility.DamageShield;
		case 6:
		case 8:
			return MagicDollAbility.HealthRegen;
		case 9:
			return MagicDollAbility.BowMastery;
		case 13:
			return MagicDollAbility.ArmorBonus;
		default:
			return MagicDollAbility.None;
		}
	}
}
