using System;
using System.Runtime.CompilerServices;

namespace IdleLineage.Combat;

public static class SatietyRules
{
	private sealed class ConsumptionTimer
	{
		public double ElapsedSeconds { get; set; }
	}

	public const double Maximum = 225.0;

	public const double Initial = 40.0;

	public const double ConsumptionIntervalSeconds = 60.0;

	public const double ConsumptionPerInterval = 1.0;

	public const double NaturalRegenerationMinimum = 3.0;

	private const double TimerEpsilon = 1E-09;

	private static readonly ConditionalWeakTable<Combatant, ConsumptionTimer> ConsumptionTimers = new ConditionalWeakTable<Combatant, ConsumptionTimer>();

	public static double Clamp(double value)
	{
		if (!double.IsFinite(value))
		{
			return 225.0;
		}
		return Math.Clamp(value, 0.0, 225.0);
	}

	public static double Restore(Combatant actor, double amount)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!double.IsFinite(amount) || amount < 0.0)
		{
			throw new ArgumentOutOfRangeException("amount", "Satiety restoration must be finite and non-negative.");
		}
		if (!UsesSatiety(actor))
		{
			return 0.0;
		}
		double num = Clamp(actor.Satiety);
		actor.Satiety = Clamp(num + amount);
		return actor.Satiety - num;
	}

	public static double Percent(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!UsesSatiety(actor))
		{
			return 100.0;
		}
		return Math.Clamp(actor.Satiety / 225.0 * 100.0, 0.0, 100.0);
	}

	public static bool NaturalRegenerationAllowed(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!UsesSatiety(actor))
		{
			return true;
		}
		return Clamp(actor.Satiety) >= 3.0;
	}

	public static bool UsesSatiety(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.Kind != CombatantKind.Player)
		{
			if (actor.Kind == CombatantKind.Ally)
			{
				return !MonsterCompanionRules.IsCompanion(actor);
			}
			return false;
		}
		return true;
	}

	public static double Tick(Combatant actor, double deltaSeconds)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0.0)
		{
			throw new ArgumentOutOfRangeException("deltaSeconds", "Satiety delta time must be finite and non-negative.");
		}
		if (deltaSeconds == 0.0 || actor.Dead || !UsesSatiety(actor))
		{
			return 0.0;
		}
		ConsumptionTimer orCreateValue = ConsumptionTimers.GetOrCreateValue(actor);
		orCreateValue.ElapsedSeconds += deltaSeconds;
		if (orCreateValue.ElapsedSeconds + 1E-09 < 60.0)
		{
			return 0.0;
		}
		double num = Math.Floor((orCreateValue.ElapsedSeconds + 1E-09) / 60.0);
		orCreateValue.ElapsedSeconds -= num * 60.0;
		if (orCreateValue.ElapsedSeconds < 0.0)
		{
			orCreateValue.ElapsedSeconds = 0.0;
		}
		double num2 = Clamp(actor.Satiety);
		actor.Satiety = Clamp(num2 - num * 1.0);
		return num2 - actor.Satiety;
	}

	public static void ResetOnDeath(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (UsesSatiety(actor))
		{
			actor.Satiety = 0.0;
			ConsumptionTimers.Remove(actor);
		}
	}
}
