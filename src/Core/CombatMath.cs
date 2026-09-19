using System;

namespace IdleLineage.Core;

public static class CombatMath
{
	private const double MinimumCooldown = 1E-06;

	private const double CooldownCarryFloor = -0.999999;

	public static double PlayerAttackIntervalTicks(double? attackIntervalSeconds, bool slowAttack, bool includeTemporarySlow = true)
	{
		double num = FiniteOr(attackIntervalSeconds, 0.1);
		if (num <= 0.0)
		{
			num = 0.1;
		}
		double num2 = Math.Max(1.0, num * 10.0);
		if (includeTemporarySlow && slowAttack)
		{
			num2 *= 2.0;
		}
		return num2;
	}

	public static double PlayerOffhandIntervalTicks(bool hasOffhand, double? attackIntervalSeconds, bool slowAttack, bool includeTemporarySlow = true)
	{
		if (!hasOffhand)
		{
			return 0.0;
		}
		double num = FiniteOr(attackIntervalSeconds, 0.0);
		if (num <= 0.0)
		{
			return 0.0;
		}
		double num2 = Math.Max(1.0, num * 10.0);
		if (includeTemporarySlow && slowAttack)
		{
			num2 *= 2.0;
		}
		return num2;
	}

	public static double AllyAttackIntervalTicks(double? attackIntervalSeconds, double fallbackIntervalSeconds, bool cleaveActive, bool cleaveMastery, bool crushFuryActive, bool fangFuryActive, bool slowAttack)
	{
		double num = FiniteOr(attackIntervalSeconds, 0.0);
		if (num <= 0.0)
		{
			num = FiniteOr(fallbackIntervalSeconds, 0.1);
		}
		double num2 = Math.Max(1.0, num * 10.0);
		if (cleaveActive && !cleaveMastery)
		{
			num2 = Math.Max(1.0, num2 / 1.2);
		}
		if (crushFuryActive)
		{
			num2 = Math.Max(1.0, num2 / 1.2);
		}
		if (fangFuryActive)
		{
			num2 = Math.Max(1.0, num2 / 1.3);
		}
		if (slowAttack)
		{
			num2 *= 2.0;
		}
		return num2;
	}

	public static double AllyOffhandIntervalTicks(bool hasOffhand, double? attackIntervalSeconds, bool cleaveActive, bool cleaveMastery, bool crushFuryActive, bool fangFuryActive, bool slowAttack)
	{
		if (!hasOffhand)
		{
			return 0.0;
		}
		double num = FiniteOr(attackIntervalSeconds, 0.0);
		if (num <= 0.0)
		{
			return 0.0;
		}
		double num2 = Math.Max(1.0, num * 10.0);
		if (cleaveActive && !cleaveMastery)
		{
			num2 = Math.Max(1.0, num2 / 1.2);
		}
		if (crushFuryActive)
		{
			num2 = Math.Max(1.0, num2 / 1.2);
		}
		if (fangFuryActive)
		{
			num2 = Math.Max(1.0, num2 / 1.3);
		}
		if (slowAttack)
		{
			num2 *= 2.0;
		}
		return num2;
	}

	public static double CastIntervalTicks(double? castLock, double? supportCastLock, double fallbackCastLock, bool support)
	{
		double num = ((support && supportCastLock.HasValue) ? supportCastLock.Value : (castLock ?? fallbackCastLock));
		if (!double.IsFinite(num) || num == 0.0)
		{
			num = 12.0;
		}
		return Math.Max(1.0, num);
	}

	public static double NextCastCooldown(double current, double? castLock, double? supportCastLock, double fallbackCastLock, bool support)
	{
		double num = ((double.IsFinite(current) && current < 0.0) ? Math.Max(-0.999999, current) : 0.0);
		double val = JsRound((CastIntervalTicks(castLock, supportCastLock, fallbackCastLock, support) + num) * 1000000.0) / 1000000.0;
		return Math.Max(1E-06, val);
	}

	public static double MagicResistanceMultiplier(double magicResistance)
	{
		if (magicResistance <= 100.0)
		{
			return (100.0 - magicResistance / 2.0) / 100.0;
		}
		if (magicResistance <= 200.0)
		{
			return 0.5 - (magicResistance - 100.0) / 1000.0;
		}
		if (magicResistance <= 400.0)
		{
			return 0.4 - (magicResistance - 200.0) * 0.00075;
		}
		if (magicResistance <= 600.0)
		{
			return 0.25 - (magicResistance - 400.0) * 0.0006;
		}
		if (magicResistance <= 800.0)
		{
			return 0.13 - (magicResistance - 600.0) * 0.0004;
		}
		if (magicResistance <= 1000.0)
		{
			return 0.05 - (magicResistance - 800.0) * 0.0002;
		}
		return 0.01;
	}

	public static double ElementCounterMultiplier(string? attackElement, string? defenseElement)
	{
		return 1.0;
	}

	public static bool IsElementCounter(string? attackElement, string? defenseElement)
	{
		string text = NormalizeElement(attackElement);
		string text2 = NormalizeElement(defenseElement);
		if ((!(text == "fire") || !(text2 == "earth")) && (!(text == "earth") || !(text2 == "wind")) && (!(text == "wind") || !(text2 == "water")))
		{
			if (text == "water")
			{
				return text2 == "fire";
			}
			return false;
		}
		return true;
	}

	public static double MagicDamageCoefficient(double? intelligenceSpellPower, double? itemSpellPower, double attributeDefense, double? spellTier)
	{
		double num = Math.Clamp(FiniteOr(intelligenceSpellPower, 0.0), 0.0, 33.0);
		double num2 = Math.Max(0.0, FiniteOr(itemSpellPower, 0.0));
		double num3 = Math.Max(1.0, num + num2);
		double num4 = Math.Clamp(FiniteOr(attributeDefense, 0.0), 0.0, 1.0);
		return Math.Max(0.0, 1.0 - num4 + 3.0 * num3 / 32.0) * (spellTier.HasValue ? CombatCurveMath.MagicTierMultiplier(spellTier.Value) : 1.0);
	}

	public static double MagicBaseDamage(double rolled, double flatBase, double magicDamage, bool includeStat = true)
	{
		double num = (includeStat ? Math.Max(0.0, FiniteOr(magicDamage, 0.0)) : 0.0);
		return Math.Max(0.0, FiniteOr(rolled, 0.0)) + Math.Max(0.0, FiniteOr(flatBase, 0.0)) + num;
	}

	public static int ClassicHealMagicBonus(double intelligence)
	{
		int num = Math.Max(0, (int)Math.Floor(FiniteOr(intelligence, 0.0)));
		if (num <= 9)
		{
			return -1;
		}
		if (num <= 11)
		{
			return 0;
		}
		if (num <= 14)
		{
			return 1;
		}
		if (num <= 17)
		{
			return 2;
		}
		if (num == 18)
		{
			return 3;
		}
		if (num <= 25)
		{
			return num - 15;
		}
		return Math.Min(21, 10 + (int)Math.Floor((double)(num - 25) / 5.0));
	}

	private static string NormalizeElement(string? value)
	{
		return value ?? string.Empty;
	}

	private static double FiniteOr(double? value, double fallback)
	{
		if (!value.HasValue || !double.IsFinite(value.Value))
		{
			return fallback;
		}
		return value.Value;
	}

	private static double JsRound(double value)
	{
		return Math.Floor(value + 0.5);
	}
}
