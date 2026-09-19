using System;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

internal static class L1jSummonRules
{
	public static bool IsLive(IGameData data, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Roster(data, skillId) != null;
	}

	public static JsonObject? Roster(IGameData data, string skillId)
	{
		return data.Skill(skillId)?["l1jSummon"] as JsonObject;
	}

	public static int SummonCount(JsonObject roster, Combatant owner, int existingSummons)
	{
		return SummonCountByPetCost(roster, owner, Math.Max(0, existingSummons) * PetCostPerUnit(roster, owner));
	}

	public static int SummonCountByPetCost(JsonObject roster, Combatant owner, int existingPetCost)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (!(roster["cost"] is JsonObject jsonObject))
		{
			return 0;
		}
		int num = (int)Math.Floor(Math.Max(0.0, owner.D.Cha));
		if (CombatSkill.ReadBool(jsonObject, "requiresNoPets"))
		{
			return (existingPetCost <= 0) ? 1 : 0;
		}
		if (jsonObject["byClass"] is JsonObject jsonObject2)
		{
			string propertyName = ClassKitRegistry.NormalizeClassId(owner.ClassId);
			if (!((jsonObject2[propertyName] ?? jsonObject2["default"]) is JsonObject source))
			{
				return 0;
			}
			int num2 = CombatSkill.ReadInt(source, "chaCap");
			int num3 = ((num2 > 0) ? Math.Min(num, num2) : num) + CombatSkill.ReadInt(source, "chaBonus");
			int num4 = CombatSkill.ReadInt(jsonObject, "minimumBudget");
			return (num3 - Math.Max(0, existingPetCost) >= num4) ? 1 : 0;
		}
		int num5 = CombatSkill.ReadInt(jsonObject, "unitCost");
		if (num5 <= 0)
		{
			return 0;
		}
		int num6 = Math.Min(num, CombatSkill.ReadInt(jsonObject, "chaCap")) + CombatSkill.ReadInt(jsonObject, "chaBonus") - Math.Max(0, existingPetCost);
		return Math.Max(0, num6 / num5);
	}

	public static int PetCostPerUnit(JsonObject roster, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (!(roster["cost"] is JsonObject jsonObject))
		{
			return 0;
		}
		int num = CombatSkill.ReadInt(jsonObject, "unitCost");
		if (num > 0)
		{
			return num;
		}
		if (jsonObject["byClass"] is JsonObject)
		{
			return Math.Max(1, CombatSkill.ReadInt(jsonObject, "minimumBudget"));
		}
		if (CombatSkill.ReadBool(jsonObject, "requiresNoPets"))
		{
			return Math.Max(1, (int)Math.Floor(Math.Max(0.0, owner.D.Cha)) + 7);
		}
		return 0;
	}

	public static JsonObject? TierFor(JsonObject roster, int level)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		if (!(roster["tiers"] is JsonArray { Count: not 0 } jsonArray))
		{
			return null;
		}
		for (int i = 0; i < jsonArray.Count; i++)
		{
			if (jsonArray[i] is JsonObject jsonObject)
			{
				if (i == jsonArray.Count - 1)
				{
					return jsonObject;
				}
				if (!(jsonObject["maxLevelExclusive"] is JsonValue jsonValue))
				{
					return jsonObject;
				}
				if (level < jsonValue.GetValue<int>())
				{
					return jsonObject;
				}
			}
		}
		return jsonArray[jsonArray.Count - 1] as JsonObject;
	}

	public static JsonObject? FormFor(JsonObject roster, string element)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		if (!(roster["forms"] is JsonArray jsonArray))
		{
			return null;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject jsonObject && string.Equals(CombatSkill.ReadString(jsonObject, "element"), element, StringComparison.Ordinal))
			{
				return jsonObject;
			}
		}
		return null;
	}

	public static JsonObject? ZombieFor(JsonObject roster, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (!(roster["forms"] is JsonArray source))
		{
			return null;
		}
		string b = ClassKitRegistry.NormalizeClassId(owner.ClassId);
		JsonObject result = null;
		foreach (JsonObject item in source.OfType<JsonObject>())
		{
			string text = CombatSkill.ReadString(item, "classId");
			if (text == "default")
			{
				result = item;
			}
			else if (string.Equals(text, b, StringComparison.Ordinal))
			{
				int num = CombatSkill.ReadInt(item, "minLevel");
				if (owner.Level >= num && (!(item["maxLevelExclusive"] is JsonValue jsonValue) || owner.Level < jsonValue.GetValue<int>()))
				{
					return item;
				}
			}
		}
		return result;
	}

	public static SummonUnitPlan UnitPlan(JsonObject unit, bool rangedMagic = false)
	{
		ArgumentNullException.ThrowIfNull(unit, "unit");
		int num = Math.Max(1, CombatSkill.ReadInt(unit, "lv"));
		double num2 = CombatSkill.ReadDouble(unit, "attackRange");
		string element = CombatSkill.NormalizeElement(CombatSkill.ReadString(unit, "e"));
		double num3 = CombatSkill.ReadDouble(unit, "hit");
		double num4 = CombatSkill.ReadDouble(unit, "db");
		return new SummonUnitPlan(CombatSkill.ReadString(unit, "n"), num, Math.Max(1.0, CombatSkill.ReadDouble(unit, "hp")), Math.Max(0.1, CombatSkill.ReadDouble(unit, "atkSpd", 1.5)), rangedMagic ? 72.0 : ((num2 > 0.0) ? num2 : 12.0), CombatSkill.ReadDouble(unit, "ac"), CombatSkill.ReadDouble(unit, "dr"), rangedMagic ? 0.0 : num3, rangedMagic ? 0.0 : num4, num, element, rangedMagic ? new SummonMagicAttackProfile(1, num, num4, 1.0, num3, 0.0, element) : null, Array.Empty<SummonProcProfile>(), null, $"gfx:{CombatSkill.ReadInt(unit, "gfx")}", CombatSkill.ReadDouble(unit, "mr"), CombatSkill.ReadDouble(unit, "moveSpd"));
	}
}
