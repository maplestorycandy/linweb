using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class AllyBehaviorRules
{
	public const AllyBehavior Default = AllyBehavior.Balanced;

	public const int LeashCells = 6;

	public const double LeashDistance = 288.0;

	public const double BalancedLowHpPercent = 60.0;

	public const double BalancedHealTeammatePercent = 60.0;

	public const double GuardianHealTeammatePercent = 70.0;

	public const double SupportHealTeammatePercent = 65.0;

	public const double GuardianAttackMinMpPercent = 60.0;

	public const double SupportAttackMinMpPercent = 40.0;

	public const double HealMinMpPercent = 25.0;

	public static double HealTeammatePercent(AllyBehavior behavior)
	{
		return behavior switch
		{
			AllyBehavior.Guardian => GuardianHealTeammatePercent,
			AllyBehavior.Support => SupportHealTeammatePercent,
			_ => BalancedHealTeammatePercent,
		};
	}

	public static double HealPotionHpPercent(AllyBehavior behavior)
	{
		return (behavior == AllyBehavior.Balanced) ? 60 : 70;
	}

	public static bool LeashedToLeader(AllyBehavior behavior, double hpPercent)
	{
		return behavior switch
		{
			AllyBehavior.Guardian => true, 
			AllyBehavior.Balanced => hpPercent < 60.0, 
			_ => false, 
		};
	}

	public static bool AttackSkillsAllowed(AllyBehavior behavior, double mpPercent)
	{
		return behavior switch
		{
			AllyBehavior.Guardian => mpPercent >= 60.0, 
			AllyBehavior.Support => mpPercent >= 40.0, 
			_ => true, 
		};
	}

	public static bool HealSkillsAllowed(AllyBehavior behavior, double mpPercent)
	{
		return behavior switch
		{
			AllyBehavior.Guardian => mpPercent >= 30.0,
			AllyBehavior.Support => mpPercent >= 20.0,
			_ => mpPercent >= 25.0,
		};
	}

	public static string ToKey(AllyBehavior behavior)
	{
		return behavior switch
		{
			AllyBehavior.Aggressive => "aggressive", 
			AllyBehavior.Guardian => "guardian", 
			AllyBehavior.Support => "support", 
			_ => "balanced", 
		};
	}

	public static AllyBehavior Parse(string? key)
	{
		return key?.Trim().ToLowerInvariant() switch
		{
			"aggressive" => AllyBehavior.Aggressive, 
			"guardian" => AllyBehavior.Guardian, 
			"support" => AllyBehavior.Support, 
			_ => AllyBehavior.Balanced, 
		};
	}

	public static string Label(AllyBehavior behavior)
	{
		return behavior switch
		{
			AllyBehavior.Aggressive => "進攻", 
			AllyBehavior.Guardian => "保護", 
			AllyBehavior.Support => "輔助", 
			_ => "平衡", 
		};
	}

	public static AllySkillClass Classify(IGameData data, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!string.IsNullOrWhiteSpace(skillId))
		{
			JsonObject jsonObject = data.Skill(skillId);
			if (jsonObject != null)
			{
				if (SummonRules.IsSummonSkill(skillId, jsonObject) || CharmRules.IsCharmSkill(jsonObject))
				{
					return AllySkillClass.None;
				}
				if (CombatSkill.TryRead(skillId, jsonObject, out CombatSkill skill) && skill != null)
				{
					if (skill.DarkCritical || string.Equals(skillId, "sk_dark_crit", StringComparison.OrdinalIgnoreCase))
					{
						return AllySkillClass.None;
					}
					if (skill.IsHeal)
					{
						return AllySkillClass.Heal;
					}
					if (skill.Status != null)
					{
						return AllySkillClass.Debuff;
					}
					return AllySkillClass.Attack;
				}
				if (!(CombatSkill.ReadString(jsonObject, "type") == "buff") || !SkillExecutionRules.IsExecutable(data, skillId))
				{
					return AllySkillClass.None;
				}
				return AllySkillClass.Buff;
			}
		}
		return AllySkillClass.None;
	}
}
