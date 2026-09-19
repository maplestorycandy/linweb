using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class RegenerationRules
{
	public const double BaseHealthIntervalSeconds = 8.0;

	public const double MinimumHealthIntervalSeconds = 3.0;

	public const double BaseManaIntervalSeconds = 8.0;

	public const double MinimumManaIntervalSeconds = 1.0;

	public static double HealthIntervalSeconds(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		double num = Math.Max(3.0, 8.0 - Math.Max(0.0, actor.D.HealthRegenIntervalReductionSeconds));
		if (actor.Buffs.GetValueOrDefault("sk_heal_energy_storm") > 0.0)
		{
			num = Math.Min(num, 3.0);
		}
		return num;
	}

	public static double MapHealthDrainPerCycle(int mapId)
	{
		return (mapId == 410) ? 10 : 0;
	}

	public static double ManaIntervalSeconds(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return 8.0;
	}

	public static bool CanRestoreHealth(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.IsAlive && WeightRules.NaturalRegenerationAllowed(actor) && actor.Hp < actor.MaxHp && actor.Buffs.GetValueOrDefault("sk_abs_barrier") <= 0.0)
		{
			return actor.Buffs.GetValueOrDefault("sk_berserk") <= 0.0;
		}
		return false;
	}

	public static bool CanRestoreMana(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.IsAlive && WeightRules.NaturalRegenerationAllowed(actor) && actor.Mp < actor.MaxMp)
		{
			return actor.Buffs.GetValueOrDefault("sk_abs_barrier") <= 0.0;
		}
		return false;
	}

	public static double RollHealthAmount(Combatant actor, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(random, "random");
		int num = Math.Max(0, (int)Math.Floor(actor.D.HealthRegenMaximum));
		double val = (double)(((num > 0) ? random.Roll(1, num) : 0) + actor.D.OriginalHealthRegen) + actor.D.HealthRegenFlat + CombatModifierRules.ActiveHealthRegenBonus(actor);
		if (actor.Kind == CombatantKind.Ally)
		{
			val = Math.Max(1.0, val);
		}
		return Math.Max(0.0, val);
	}

	public static double ManaAmount(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		double num = actor.D.ManaRegen + (double)actor.D.OriginalManaRegen + CombatModifierRules.ActiveManaRegenBonus(actor);
		if (actor.MaxMp > 0.0 && actor.Mp < actor.MaxMp * 0.15)
		{
			num += actor.D.LowManaRegenBonus;
		}
		return Math.Max(0.0, num);
	}
}
