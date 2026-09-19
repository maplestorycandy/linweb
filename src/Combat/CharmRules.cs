using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class CharmRules
{
	public const string SkillId = "sk_charm";

	public const int MinimumSuccessPercent = 1;

	public const int MaximumSuccessPercent = 90;

	public const int MagicResistanceDivisor = 3;

	public const int LevelsPerGrowthPoint = 5;

	public const int MaximumMagicHitBonusDice = 3;

	public const string MissingMaterialFailure = "迷魅失敗：缺少目標對應的未封印卡。";

	public const string DuplicateCardFailure = "迷魅失敗：你已擁有這種怪物的卡片。";

	public const string InvalidTargetFailure = "迷魅失敗：這個目標無法捕捉。";

	public const string UnreachableTargetFailure = "迷魅失敗：目標超出射程或視線受阻。";

	public static bool IsCharmSkill(JsonObject source)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		if (string.Equals(CombatSkill.ReadString(source, "type"), "manual", StringComparison.Ordinal))
		{
			return string.Equals(CombatSkill.ReadString(source, "mEff"), "charm", StringComparison.Ordinal);
		}
		return false;
	}

	public static double HpMultiplier(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = ((target.MaxHp <= 0.0) ? 1.0 : (target.Hp / target.MaxHp));
		if (num < 0.25)
		{
			return 1.3;
		}
		if (num < 0.5)
		{
			return 1.2;
		}
		if (num < 0.75)
		{
			return 1.1;
		}
		return 1.0;
	}

	public static int ApplyHpMultiplier(int probability, Combatant target)
	{
		return Math.Min(90, (int)((double)probability * HpMultiplier(target)));
	}

	public static int MagicResistancePenalty(double effectiveMagicResistance)
	{
		return (int)Math.Ceiling(Math.Max(0.0, effectiveMagicResistance) / 3.0);
	}

	public static int RelativeLevelGrowthBonus(int casterLevel, int targetLevel)
	{
		return Math.Max(0, casterLevel - targetLevel) / 5;
	}

	public static int MagicHitBonusDice(double totalMagicHit)
	{
		return Math.Clamp((int)Math.Floor(Math.Max(0.0, totalMagicHit)), 0, 3);
	}

	public static int FinalSuccessPercent(int rolledProbability, int casterLevel, int targetLevel, Combatant target)
	{
		int num = ApplyHpMultiplier(rolledProbability + RelativeLevelGrowthBonus(casterLevel, targetLevel), target);
		if (num <= 0)
		{
			return 1;
		}
		return Math.Min(num, 90);
	}
}
