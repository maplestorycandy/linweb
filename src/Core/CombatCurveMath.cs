using System;

namespace IdleLineage.Core;

public static class CombatCurveMath
{
	public const double AlignmentMinimum = -32767.0;

	public const double AlignmentMaximum = 32767.0;

	public const double AlignmentJustice = 1000.0;

	public static double MagicTierMultiplier(double tier)
	{
		return 1.0 + Math.Max(0.0, FiniteOr(tier, 0.0)) / 10.0;
	}

	public static int WeaponMagicTier(double gachaWeight, bool legend, bool relic)
	{
		double num = FiniteOr(gachaWeight, 0.0);
		if (legend || relic || num == 1.0)
		{
			return 5;
		}
		if (num >= 2.0 && num <= 20.0)
		{
			return 4;
		}
		if (num >= 21.0 && num <= 40.0)
		{
			return 3;
		}
		if (num >= 41.0 && num <= 60.0)
		{
			return 2;
		}
		if (num >= 61.0 && num <= 80.0)
		{
			return 1;
		}
		return 0;
	}

	public static double EffectiveResistancePercent(double value)
	{
		double num = FiniteOr(value, 0.0);
		if (num <= 50.0)
		{
			return Math.Max(0.0, num);
		}
		return 50.0 + Math.Floor((num - 50.0) / 5.0);
	}

	public static double PhysicalHitSoftFloor(double hitBonus, bool difficultTarget)
	{
		if (!difficultTarget)
		{
			return 1.0;
		}
		return Math.Clamp(1.0 + Math.Floor(Math.Max(0.0, FiniteOr(hitBonus, 0.0)) / 20.0), 1.0, 5.0);
	}

	public static double FuryRageRatio(bool furySetActive, double currentHealth, double maximumHealth)
	{
		if (!furySetActive)
		{
			return 0.0;
		}
		double num = Math.Max(1.0, FiniteOr(maximumHealth, 1.0));
		double num2 = 1.0 - FiniteOr(currentHealth, 0.0) / num;
		return Math.Min(0.2, Math.Max(0.0, Math.Floor(num2 * 10.0 + 1E-09) * 0.04));
	}

	public static double FinalDamageMultiplier(bool redLionSetActive, bool furySetActive, double currentHealth, double maximumHealth)
	{
		return (redLionSetActive ? 1.2 : 1.0) * (1.0 + FuryRageRatio(furySetActive, currentHealth, maximumHealth));
	}

	public static double AllyBuffDamageReductionMultiplier(bool holyBarrierActive, bool dragonScionActive, bool furySetActive, double currentHealth, double maximumHealth)
	{
		double num = 1.0;
		if (holyBarrierActive)
		{
			num *= 0.7;
		}
		if (dragonScionActive)
		{
			num *= 0.85;
		}
		if (furySetActive)
		{
			num *= 1.0 - FuryRageRatio(furySetActive: true, currentHealth, maximumHealth);
		}
		return num;
	}

	public static double JusticeHealMultiplier(double alignment)
	{
		double num = ClampAlignment(alignment);
		if (num < 1000.0)
		{
			return 1.0;
		}
		return 1.0 + 0.2 * (num - 1000.0) / 31767.0;
	}

	public static double EvilAlignmentBonus(double maximumBonus, double alignment)
	{
		return Math.Floor(FiniteOr(maximumBonus, 0.0) * Math.Max(0.0, 0.0 - ClampAlignment(alignment)) / Math.Abs(-32767.0));
	}

	public static AlignmentTier GetAlignmentTier(double alignment)
	{
		double num = ClampAlignment(alignment);
		if (num >= 1000.0)
		{
			return AlignmentTier.Justice;
		}
		if (num <= -1000.0)
		{
			return AlignmentTier.Evil;
		}
		return AlignmentTier.Neutral;
	}

	public static double ChangeAlignment(double current, double delta)
	{
		return ClampAlignment(FiniteOr(current, 0.0) + FiniteOr(delta, 0.0));
	}

	public static double ClampAlignment(double value)
	{
		return Math.Clamp(Math.Floor(FiniteOr(value, 0.0) + 0.5), -32767.0, 32767.0);
	}

	private static double NonZeroOr(double value, double fallback)
	{
		double num = FiniteOr(value, fallback);
		if (num != 0.0)
		{
			return num;
		}
		return fallback;
	}

	private static double FiniteOr(double value, double fallback)
	{
		if (!double.IsFinite(value))
		{
			return fallback;
		}
		return value;
	}
}
