using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ItemGrantedSkillRules
{
	private static readonly HashSet<string> InventoryGrantClasses = new HashSet<string>(StringComparer.Ordinal) { "knight", "royal", "warrior" };

	public static bool Grants(Combatant actor, string skillId, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		ArgumentNullException.ThrowIfNull(data, "data");
		if (data.Skill(skillId) == null)
		{
			return false;
		}
		foreach (ItemStack value in actor.EquippedItems.Values)
		{
			if (ItemGrants(data.Item(value.ItemKey), skillId))
			{
				return true;
			}
		}
		if (!InventoryGrantClasses.Contains(ClassKitRegistry.NormalizeClassId(actor.ClassId)))
		{
			return false;
		}
		foreach (ItemStack inventoryStack in actor.InventoryStacks)
		{
			JsonObject jsonObject = data.Item(inventoryStack.ItemKey);
			if (!ReadBool(jsonObject, "grantSkillsEquipOnly") && ItemGrants(jsonObject, skillId))
			{
				return true;
			}
		}
		return false;
	}

	public static IReadOnlySet<string> GrantedSkillIds(Combatant actor, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemStack value in actor.EquippedItems.Values)
		{
			AddItemSkills(data, value.ItemKey, hashSet);
		}
		if (InventoryGrantClasses.Contains(ClassKitRegistry.NormalizeClassId(actor.ClassId)))
		{
			foreach (ItemStack inventoryStack in actor.InventoryStacks)
			{
				JsonObject jsonObject = data.Item(inventoryStack.ItemKey);
				if (!ReadBool(jsonObject, "grantSkillsEquipOnly"))
				{
					AddItemSkills(data, jsonObject, hashSet);
				}
			}
		}
		return hashSet;
	}

	private static void AddItemSkills(IGameData data, string itemKey, HashSet<string> skills)
	{
		AddItemSkills(data, data.Item(itemKey), skills);
	}

	private static void AddItemSkills(IGameData data, JsonObject? definition, HashSet<string> skills)
	{
		if (!(definition?["grantSkills"] is JsonArray jsonArray))
		{
			return;
		}
		foreach (JsonNode item in jsonArray)
		{
			string text = item?.GetValue<string>() ?? "";
			if (text.Length > 0 && data.Skill(text) != null)
			{
				skills.Add(text);
			}
		}
	}

	private static bool ItemGrants(JsonObject? definition, string skillId)
	{
		if (!(definition?["grantSkills"] is JsonArray source))
		{
			return false;
		}
		return source.Any((JsonNode node) => string.Equals(node?.GetValue<string>(), skillId, StringComparison.Ordinal));
	}

	private static bool ReadBool(JsonObject? source, string key)
	{
		bool value = default(bool);
		return source?[key] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
