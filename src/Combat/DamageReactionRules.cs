namespace IdleLineage.Combat;

public static class DamageReactionRules
{
	public static bool PlaysHurtAnimation(DamageType damageType)
	{
		return damageType != DamageType.Dot;
	}
}
