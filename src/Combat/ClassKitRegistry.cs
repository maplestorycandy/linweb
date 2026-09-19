using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ClassKitRegistry
{
	private static readonly IReadOnlyDictionary<string, ClassKit> Kits = new Dictionary<string, ClassKit>(StringComparer.Ordinal)
	{
		["royal"] = new ClassKit("royal", "王子", RoyalLevel, RoyalWeapon),
		["knight"] = new ClassKit("knight", "男騎士", (JsonObject skill) => DirectLevel(skill, "reqK"), StandardWeapon),
		["elf"] = new ClassKit("elf", "男妖精", (JsonObject skill) => DirectLevel(skill, "reqE"), StandardWeapon),
		["mage"] = new ClassKit("mage", "男法師", (JsonObject skill) => DirectLevel(skill, "reqM"), StandardWeapon),
		["dark"] = new ClassKit("dark", "男黑暗妖精", DarkLevel, DarkWeapon),
		["dragon"] = new ClassKit("dragon", "男龍騎士", (JsonObject skill) => DirectLevel(skill, "reqDk"), DragonWeapon),
		["warrior"] = new ClassKit("warrior", "男戰士", WarriorLevel, WarriorWeapon),
		["illusion"] = new ClassKit("illusion", "男幻術士", (JsonObject skill) => DirectLevel(skill, "reqI"), IllusionWeapon)
	};

	public static IReadOnlyCollection<ClassKit> All { get; } = Kits.Values.ToArray();

	public static string NormalizeClassId(string? classId)
	{
		return classId switch
		{
			"darkelf" => "dark", 
			"dknight" => "dragon", 
			"illusionist" => "illusion", 
			_ => classId ?? string.Empty, 
		};
	}

	public static bool TryGet(string? classId, out ClassKit? kit)
	{
		return Kits.TryGetValue(NormalizeClassId(classId), out kit);
	}

	public static bool Bind(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!TryGet(actor.ClassId, out ClassKit kit) || kit == null)
		{
			return false;
		}
		actor.ClassId = kit.Id;
		actor.Kit = kit;
		if (string.IsNullOrWhiteSpace(actor.Avatar))
		{
			actor.Avatar = kit.DefaultAvatar;
		}
		return true;
	}

	public static bool CanLearnSkill(Combatant actor, string skillId, IGameData data, out int requiredLevel)
	{
		requiredLevel = 0;
		if (TryGet(actor.ClassId, out ClassKit kit) && kit != null)
		{
			JsonObject jsonObject = data.Skill(skillId);
			if (jsonObject != null)
			{
				if (!IsPlayerSkillAvailable(jsonObject))
				{
					return false;
				}
				if (!ClassSkillAccessRules.Allows(actor, skillId))
				{
					return false;
				}
				if (!kit.TryGetRequiredLevel(jsonObject, out requiredLevel))
				{
					return false;
				}
				if (actor.Level >= requiredLevel)
				{
					return ElementRequirementMet(actor, jsonObject);
				}
				return false;
			}
		}
		return false;
	}

	public static bool CanUseSkill(Combatant actor, string skillId, IGameData data)
	{
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject == null || !IsPlayerSkillAvailable(jsonObject))
		{
			return false;
		}
		if (actor.GrantedSkills.Contains(skillId) || ItemGrantedSkillRules.Grants(actor, skillId, data))
		{
			return true;
		}
		int requiredLevel;
		if (actor.LearnedSkills.Contains(skillId))
		{
			return CanLearnSkill(actor, skillId, data, out requiredLevel);
		}
		return false;
	}

	public static IReadOnlyList<ClassSkillEntry> SkillsFor(Combatant actor, IGameData data)
	{
		if (!TryGet(actor.ClassId, out ClassKit kit) || kit == null)
		{
			return Array.Empty<ClassSkillEntry>();
		}
		List<ClassSkillEntry> list = new List<ClassSkillEntry>();
		IReadOnlySet<string> readOnlySet = ItemGrantedSkillRules.GrantedSkillIds(actor, data);
		foreach (var (text2, jsonNode2) in data.Skills)
		{
			if (jsonNode2 is JsonObject jsonObject && IsPlayerSkillAvailable(jsonObject))
			{
				bool flag = actor.GrantedSkills.Contains(text2) || readOnlySet.Contains(text2);
				int requiredLevel;
				bool flag2 = kit.TryGetRequiredLevel(jsonObject, out requiredLevel) && ClassSkillAccessRules.Allows(actor, text2);
				if (flag2 || flag)
				{
					list.Add(new ClassSkillEntry(text2, flag2 ? requiredLevel : 0, ReadInt(jsonObject, "tier"), flag || actor.Level >= requiredLevel, flag || ElementRequirementMet(actor, jsonObject), actor.LearnedSkills.Contains(text2), flag));
				}
			}
		}
		return (from entry in list
			orderby entry.Tier, entry.RequiredLevel
			select entry).ThenBy<ClassSkillEntry, string>((ClassSkillEntry entry) => entry.SkillId, StringComparer.Ordinal).ToArray();
	}

	public static bool IsPlayerSkillAvailable(IGameData data, string skillId)
	{
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject != null)
		{
			return IsPlayerSkillAvailable(jsonObject);
		}
		return false;
	}

	public static bool CanEquipWeapon(Combatant actor, string weaponId, IGameData data)
	{
		if (TryGet(actor.ClassId, out ClassKit kit) && kit != null)
		{
			JsonObject jsonObject = data.Item(weaponId);
			if (jsonObject != null && !(WeaponCombatProfile.ReadString(jsonObject, "type") != "wpn"))
			{
				WeaponRuleContext context = new WeaponRuleContext(kit.Id, jsonObject);
				if (!WeaponCombatProfile.ReadBool(jsonObject, "relic"))
				{
					return kit.CanEquipWeapon(context);
				}
				return ReqAllowsClass(jsonObject, kit.Id);
			}
		}
		return false;
	}

	private static int? RoyalLevel(JsonObject skill)
	{
		int? result = DirectLevel(skill, "reqRoy");
		if (result.HasValue)
		{
			return result;
		}
		if (!DirectLevel(skill, "reqM").HasValue)
		{
			return null;
		}
		return ReadInt(skill, "tier") switch
		{
			1 => 10, 
			2 => 20, 
			_ => null, 
		};
	}

	private static int? DarkLevel(JsonObject skill)
	{
		int? result = DirectLevel(skill, "reqD");
		if (result.HasValue)
		{
			return result;
		}
		if (!DirectLevel(skill, "reqM").HasValue)
		{
			return null;
		}
		return ReadInt(skill, "tier") switch
		{
			1 => 12, 
			2 => 24, 
			_ => null, 
		};
	}

	private static int? WarriorLevel(JsonObject skill)
	{
		int? result = DirectLevel(skill, "reqW");
		if (result.HasValue)
		{
			return result;
		}
		if (!DirectLevel(skill, "reqM").HasValue || ReadInt(skill, "tier") != 1)
		{
			return null;
		}
		return 15;
	}

	private static int? DirectLevel(JsonObject skill, string field)
	{
		if (!TryReadNumber(skill[field], out var value))
		{
			return null;
		}
		return Math.Max(0, (int)Math.Floor(value));
	}

	private static bool ElementRequirementMet(Combatant actor, JsonObject skill)
	{
		string text = WeaponCombatProfile.ReadString(skill, "reqEle");
		if (text.Length > 0 && actor.ElfElement != text)
		{
			return false;
		}
		if (WeaponCombatProfile.ReadBool(skill, "reqEleAny"))
		{
			return actor.ElfElement.Length > 0;
		}
		return true;
	}

	private static bool StandardWeapon(WeaponRuleContext context)
	{
		return ReqAllowsClass(context.Item, context.ClassId);
	}

	private static bool DarkWeapon(WeaponRuleContext context)
	{
		return StandardWeapon(context);
	}

	private static bool IllusionWeapon(WeaponRuleContext context)
	{
		return StandardWeapon(context);
	}

	private static bool DragonWeapon(WeaponRuleContext context)
	{
		return StandardWeapon(context);
	}

	private static bool WarriorWeapon(WeaponRuleContext context)
	{
		if (WeaponCombatProfile.ReadBool(context.Item, "isArrow"))
		{
			return false;
		}
		if (WeaponCombatProfile.ReadBool(context.Item, "warriorEquip"))
		{
			return true;
		}
		return RequirementContains(WeaponCombatProfile.ReadString(context.Item, "req"), "warrior");
	}

	private static bool RoyalWeapon(WeaponRuleContext context)
	{
		return StandardWeapon(context);
	}

	private static bool ReqAllowsClass(JsonObject item, string classId)
	{
		string text = WeaponCombatProfile.ReadString(item, "req");
		if (text.Length != 0 && !(text == "all"))
		{
			return RequirementContains(text, classId);
		}
		return true;
	}

	private static bool RequirementContains(string requirement, string classId)
	{
		return requirement.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains<string>(classId, StringComparer.Ordinal);
	}

	private static bool TryReadNumber(JsonNode? node, out double value)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<double>(out value))
		{
			return true;
		}
		value = 0.0;
		return false;
	}

	private static int ReadInt(JsonObject source, string field)
	{
		if (!TryReadNumber(source[field], out var value))
		{
			return 0;
		}
		return (int)Math.Floor(value);
	}

	private static bool IsPlayerSkillAvailable(JsonObject skill)
	{
		bool value = default(bool);
		return !(skill["playerAvailable"] is JsonValue jsonValue) || !jsonValue.TryGetValue<bool>(out value) || value;
	}
}
