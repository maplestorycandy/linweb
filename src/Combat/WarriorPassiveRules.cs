using System;

namespace IdleLineage.Combat;

public static class WarriorPassiveRules
{
	public const string CrushSkill = "sk_warrior_crush";

	public const string BerserkSkill = "sk_warrior_berserk";

	public const string TitanRockSkill = "sk_warrior_titan_rock";

	public const string TitanMagicSkill = "sk_warrior_titan_magic";

	public const string TitanBulletSkill = "sk_warrior_titan_bullet";

	public const double BerserkChance = 0.05;

	public const double BerserkDamageMultiplier = 2.0;

	public const double TitanBulletEvasionBonus = 50.0;

	public static bool Has(Combatant actor, string skillId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!actor.LearnedSkills.Contains(skillId))
		{
			return actor.GrantedSkills.Contains(skillId);
		}
		return true;
	}

	public static double CrushMeleeDamageBonus(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return Has(actor, "sk_warrior_crush") ? 2 : 0;
	}

	public static double BerserkDamageMultiplierFor(Combatant attacker, bool ranged, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (ranged || !Has(attacker, "sk_warrior_berserk"))
		{
			return 1.0;
		}
		if (!(random.NextDouble() < 0.05))
		{
			return 1.0;
		}
		return 2.0;
	}

	public static bool TitanActive(Combatant actor, string skillId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (Has(actor, skillId) && actor.MaxHp > 0.0)
		{
			return actor.Hp < actor.MaxHp * CombatModifierRules.TitanThreshold(actor);
		}
		return false;
	}

	public static bool ReflectsDamage(Combatant defender, DamageType damageType)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		switch (damageType)
		{
		case DamageType.Melee:
		case DamageType.Ranged:
			return TitanActive(defender, "sk_warrior_titan_rock");
		case DamageType.Magic:
			return TitanActive(defender, "sk_warrior_titan_magic");
		default:
			return false;
		}
	}

	public static double TitanBulletEvasionRating(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!TitanActive(actor, "sk_warrior_titan_bullet"))
		{
			return 0.0;
		}
		return 50.0;
	}
}
