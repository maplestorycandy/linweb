using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class StormBuffRules
{
	public static readonly IReadOnlySet<string> SkillIds = new HashSet<string>(StringComparer.Ordinal) { "sk_fire_prison", "sk_blizzard_storm" };

	public const int DefaultIntervalTicks = 40;

	public const int FreezeDurationTicks = 60;

	public const int FreezeBaseHitPercent = 20;

	private static readonly Dictionary<string, CombatSkill?> TickSkillCache = new Dictionary<string, CombatSkill>(StringComparer.Ordinal);

	public static bool IsStormBuff(string skillId)
	{
		return SkillIds.Contains(skillId);
	}

	internal static CombatSkill? TickSkill(IGameData? data, string skillId)
	{
		if (TickSkillCache.TryGetValue(skillId, out CombatSkill value))
		{
			return value;
		}
		CombatSkill combatSkill = null;
		JsonObject jsonObject = data?.Skill(skillId);
		if (jsonObject != null)
		{
			JsonObject jsonObject2 = new JsonObject();
			foreach (var (text2, jsonNode2) in jsonObject)
			{
				bool flag;
				switch (text2)
				{
				case "type":
				case "dmgType":
				case "target":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					jsonObject2[text2] = jsonNode2?.DeepClone();
				}
			}
			jsonObject2["type"] = "atk";
			jsonObject2["dmgType"] = "magic";
			jsonObject2["target"] = "all";
			if (CombatSkill.TryRead(skillId, jsonObject2, out CombatSkill skill))
			{
				combatSkill = skill;
			}
		}
		TickSkillCache[skillId] = combatSkill;
		return combatSkill;
	}

	public static bool ShouldTick(IGameData? data, string skillId, long currentTick)
	{
		int num = IntervalTicks(data, skillId);
		if (num > 0)
		{
			return currentTick % num == 0;
		}
		return false;
	}

	public static int IntervalTicks(IGameData? data, string skillId)
	{
		JsonObject jsonObject = data?.Skill(skillId);
		if (jsonObject == null)
		{
			return 40;
		}
		int num = (int)CombatSkill.ReadDouble(jsonObject, "stormInterval");
		if (num <= 0)
		{
			return 40;
		}
		return num;
	}

	public static double? FreezeHitOffset(IGameData? data, string skillId)
	{
		JsonObject jsonObject = data?.Skill(skillId);
		if (jsonObject == null)
		{
			return null;
		}
		if (!(jsonObject["freezeHitOff"] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return null;
		}
		return value;
	}

	public static double DamageMultiplier(IGameData? data, Combatant caster, string skillId)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		if (data == null || skillId != "sk_fire_prison")
		{
			return 1.0;
		}
		if (!caster.EquippedItems.TryGetValue("armor", out ItemStack value) || value == null)
		{
			return 1.0;
		}
		JsonObject jsonObject = data.Item(value.ItemKey);
		if (jsonObject == null || !(jsonObject["firePrisonMult"] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value2) || value2 <= 0.0)
		{
			return 1.0;
		}
		return value2;
	}
}
