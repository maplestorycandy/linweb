using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal static class L1jSkillHandover
{
	private static readonly HashSet<int> DedicatedOfficialIds = new HashSet<int>
	{
		5, 9, 18, 23, 36, 37, 39, 41, 44, 51,
		58, 61, 78, 80, 87, 100, 108, 116, 130, 131,
		132, 146, 154, 157, 158, 162, 187, 203, 208
	};

	private static readonly HashSet<string> LiveCategories = new HashSet<string>(StringComparer.Ordinal) { "attack-dice", "heal", "probability", "curse", "change" };

	public static bool IsLive(JsonObject? skill)
	{
		if (!(skill?["l1j"] is JsonObject source))
		{
			return false;
		}
		if (DedicatedOfficialIds.Contains(CombatSkill.ReadInt(source, "officialId")))
		{
			return true;
		}
		string text = CombatSkill.ReadString(source, "category");
		if (!LiveCategories.Contains(text))
		{
			return false;
		}
		return IsRoutable(text, CombatSkill.ReadString(skill, "type"), CombatSkill.ReadString(skill, "dmgType"), skill["status"] is JsonObject || skill["fixedStatus"] is JsonObject);
	}

	public static bool IsLive(CombatSkill skill)
	{
		ArgumentNullException.ThrowIfNull(skill, "skill");
		L1jSkillFields l1j = skill.L1j;
		if (l1j == null)
		{
			return false;
		}
		if (DedicatedOfficialIds.Contains(l1j.OfficialId))
		{
			return true;
		}
		if (LiveCategories.Contains(l1j.Category))
		{
			return IsRoutable(l1j.Category, skill.Type, skill.DamageType, skill.Status != null);
		}
		return false;
	}

	public static bool UsesL1jMagicDamage(CombatSkill skill)
	{
		if (IsLive(skill))
		{
			return skill.L1j.Category == "attack-dice";
		}
		return false;
	}

	public static bool UsesL1jHealing(CombatSkill skill)
	{
		if (IsLive(skill))
		{
			return skill.L1j.Category == "heal";
		}
		return false;
	}

	public static bool UsesL1jProbability(CombatSkill skill)
	{
		bool flag = IsLive(skill);
		if (flag)
		{
			string category = skill.L1j.Category;
			bool flag2 = ((category == "probability" || category == "curse") ? true : false);
			flag = flag2;
		}
		bool flag3 = flag;
		if (flag3)
		{
			bool flag2;
			switch (skill.L1j.OfficialId)
			{
			case 18:
			case 36:
			case 39:
			case 44:
			case 87:
			case 157:
			case 208:
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag3 = !flag2;
		}
		return flag3;
	}

	public static bool HasDedicatedHandler(JsonObject? skill)
	{
		if (skill?["l1j"] is JsonObject source)
		{
			return DedicatedOfficialIds.Contains(CombatSkill.ReadInt(source, "officialId"));
		}
		return false;
	}

	public static bool HasDedicatedHandler(CombatSkill skill)
	{
		L1jSkillFields l1j = skill.L1j;
		if (l1j != null)
		{
			return DedicatedOfficialIds.Contains(l1j.OfficialId);
		}
		return false;
	}

	public static double? L1jBuffDurationSeconds(JsonObject? skill)
	{
		if (!IsLive(skill) || !(skill["l1j"] is JsonObject source))
		{
			return null;
		}
		if (!string.Equals(CombatSkill.ReadString(source, "category"), "change", StringComparison.Ordinal))
		{
			return null;
		}
		int num = CombatSkill.ReadInt(source, "buffDuration");
		if (num <= 0)
		{
			return null;
		}
		return num;
	}

	public static JsonObject? L1jBuffModifiers(JsonObject? skill)
	{
		if (!IsLive(skill) || !(skill["l1j"] is JsonObject jsonObject))
		{
			return null;
		}
		if (!string.Equals(CombatSkill.ReadString(jsonObject, "category"), "change", StringComparison.Ordinal))
		{
			return null;
		}
		if (CombatSkill.ReadBool(jsonObject, "buffModifiersGuarded"))
		{
			return null;
		}
		return jsonObject["buffModifiers"] as JsonObject;
	}

	private static bool IsRoutable(string category, string portType, string portDamageType, bool hasStatus)
	{
		switch (category)
		{
		case "attack-dice":
			return string.Equals(portType, "atk", StringComparison.Ordinal) && string.Equals(portDamageType, "magic", StringComparison.Ordinal);
		case "heal":
			return string.Equals(portType, "heal", StringComparison.Ordinal);
		case "probability":
		case "curse":
			return hasStatus;
		case "change":
			return string.Equals(portType, "buff", StringComparison.Ordinal);
		default:
			throw new InvalidOperationException("L1J skill category '" + category + "' is declared live but has no routing rule. Add one in L1jSkillHandover.IsRoutable, or remove it from LiveCategories.");
		}
	}

	public static int BaseMpCost(JsonObject skill)
	{
		ArgumentNullException.ThrowIfNull(skill, "skill");
		if (UsesCustomResourceModel(skill))
		{
			return CombatSkill.ReadInt(skill, "mp");
		}
		if (!IsLive(skill) || !(skill["l1j"] is JsonObject source))
		{
			return CombatSkill.ReadInt(skill, "mp");
		}
		return CombatSkill.ReadInt(source, "mpConsume");
	}

	public static int BaseHpCost(JsonObject skill)
	{
		ArgumentNullException.ThrowIfNull(skill, "skill");
		if (UsesCustomResourceModel(skill))
		{
			return CombatSkill.ReadInt(skill, "hpCost");
		}
		if (!IsLive(skill) || !(skill["l1j"] is JsonObject source))
		{
			return CombatSkill.ReadInt(skill, "hpCost");
		}
		return CombatSkill.ReadInt(source, "hpConsume");
	}

	private static bool UsesCustomResourceModel(JsonObject skill)
	{
		if (skill["resourceModel"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			return !string.IsNullOrWhiteSpace(value);
		}
		return false;
	}
}
