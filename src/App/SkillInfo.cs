using System;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class SkillInfo
{
	public static string Name(string skillId)
	{
		return Field(skillId, "n") ?? skillId;
	}

	public static string Type(string skillId)
	{
		return Field(skillId, "type") ?? "";
	}

	public static int Mp(string skillId)
	{
		if (!(GameDataProvider.Shared.Skill(skillId)?["mp"] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0;
		}
		return (int)value;
	}

	public static int HpCost(string skillId)
	{
		if (!(GameDataProvider.Shared.Skill(skillId)?["hpCost"] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0;
		}
		return (int)value;
	}

	public static int Tier(string skillId)
	{
		if (!(GameDataProvider.Shared.Skill(skillId)?["tier"] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0;
		}
		return (int)value;
	}

	public static string TypeLabel(string skillId)
	{
		if (IsProcOnly(skillId))
		{
			return "觸發";
		}
		if (UsesCharmCards(skillId))
		{
			return "捕捉";
		}
		if (IsCleanse(skillId))
		{
			return "淨化";
		}
		if (UsesHealingSlot(skillId))
		{
			return "治療";
		}
		return Type(skillId) switch
		{
			"atk" => "攻擊", 
			"buff" => "增益", 
			"passive" => "被動", 
			"manual" => (Field(skillId, "mEff") == "barrier") ? "輔助" : "瞬移", 
			"convert" => "轉化", 
			"summon" => "召喚", 
			_ => "技能", 
		};
	}

	public static string ResourceLabel(string skillId, int manaCost)
	{
		if (!UsesCharmCards(skillId))
		{
			return $"MP {manaCost}";
		}
		return "不消耗 HP／MP；消耗目標對應未封印卡×1";
	}

	public static string UsageDescription(string skillId)
	{
		if (!UsesCharmCards(skillId))
		{
			return "";
		}
		return Field(skillId, "desc") ?? "";
	}

	public static bool UsesCharmCards(string skillId)
	{
		if (string.Equals(Type(skillId), "manual", StringComparison.Ordinal))
		{
			return string.Equals(Field(skillId, "mEff"), "charm", StringComparison.Ordinal);
		}
		return false;
	}

	public static bool IsProcOnly(string skillId)
	{
		return SkillPresentationRules.IsProcOnly(GameDataProvider.Shared, skillId);
	}

	public static bool UsesHealingSlot(string skillId)
	{
		return SkillPresentationRules.UsesHealingSlot(GameDataProvider.Shared, skillId);
	}

	public static bool IsCleanse(string skillId)
	{
		return SkillPresentationRules.IsCleanse(GameDataProvider.Shared, skillId);
	}

	public static bool IsCastable(string skillId)
	{
		if (!IsProcOnly(skillId))
		{
			return SkillExecutionRules.IsExecutable(GameDataProvider.Shared, skillId);
		}
		return false;
	}

	private static string? Field(string skillId, string key)
	{
		if (!(GameDataProvider.Shared.Skill(skillId)?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}
}
