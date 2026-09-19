using System;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class HitImpactRules
{
	public const double KnockbackDistance = 7.5;

	private const double LegacyTickSeconds = 0.1;

	public const int DefaultHitstunTicks = 5;

	public const int MaximumHitstunTicks = 12;

	public static int LegacyTicksToSteps(int ticks)
	{
		return LegacyTicksToSteps((double)ticks);
	}

	public static int LegacyTicksToSteps(double ticks)
	{
		return (int)Math.Round(ticks * 6.0, MidpointRounding.AwayFromZero);
	}

	public static bool IsStaggeringDamage(DamageType damageType)
	{
		if ((uint)damageType <= 2u)
		{
			return true;
		}
		return false;
	}

	public static bool CausesHitstun(DamageType damageType, Combatant target, bool heavy)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!IsStaggeringDamage(damageType))
		{
			return false;
		}
		if (target.Kind == CombatantKind.Player && !heavy)
		{
			return false;
		}
		if (MobFlinchCatalog.NeverFlinches(target.Avatar))
		{
			return false;
		}
		return true;
	}

	public static int HitstunUntil(Combatant target, long currentStep)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = Math.Clamp((target.D.HitstunTicks > 0) ? target.D.HitstunTicks : 5, 1, 12);
		if (num <= 0.0)
		{
			return target.HitstunUntil;
		}
		int val = (int)Math.Min(2147483647L, currentStep + LegacyTicksToSteps(num));
		return Math.Max(target.HitstunUntil, val);
	}

	public static bool IsStaggered(Combatant combatant, long currentStep)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		return combatant.HitstunUntil > currentStep;
	}

	public static bool CanBeKnockedBack(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (target.Kind == CombatantKind.Player)
		{
			return false;
		}
		return target.IsAlive;
	}

	public static WorldPoint? KnockbackTarget(Combatant attacker, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = target.Pos.X - attacker.Pos.X;
		double num2 = target.Pos.Y - attacker.Pos.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		if (num3 <= 1E-06)
		{
			return null;
		}
		return new WorldPoint(target.Pos.X + num / num3 * 7.5, target.Pos.Y + num2 / num3 * 7.5);
	}
}
