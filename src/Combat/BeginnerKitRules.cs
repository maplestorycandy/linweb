using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class BeginnerKitRules
{
	public const string TableName = "BEGINNER_KIT";

	public const string CommonGroup = "A";

	public const string StarterHealingPotionKey = "potion_heal";

	public const long StarterHealingPotionCount = 100L;

	public const string StarterHastePotionKey = "potion_haste";

	public const long StarterHastePotionCount = 20L;

	public const string StarterMageLightArrowBookKey = "bk_lightarrow";

	public const long StarterMageLightArrowBookCount = 1L;

	public const string StarterCharmCardKey = "unsealed_card_normal";

	public const long StarterCharmCardCount = 10L;

	public const string MageUnusableStarterSwordKey = "l1j_item_35";

	public const string LegacyMageCreationSkill = "sk_lightarrow";

	public static string GroupFor(string classId)
	{
		return ClassKitRegistry.NormalizeClassId(classId) switch
		{
			"royal" => "P", 
			"knight" => "K", 
			"elf" => "E", 
			"mage" => "W", 
			"dark" => "D", 
			"dragon" => "R", 
			"illusion" => "I", 
			"warrior" => "T", 
			_ => throw new InvalidDataException("Class '" + classId + "' has no beginner group."), 
		};
	}

	public static IReadOnlyList<(string ItemKey, long Count)> Items(IGameData data, string classId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		string text = ClassKitRegistry.NormalizeClassId(classId);
		JsonObject jsonObject = ReadGroups(data);
		List<(string, long)> list = new List<(string, long)>();
		string[] array = new string[2]
		{
			"A",
			GroupFor(classId)
		};
		foreach (string text2 in array)
		{
			foreach (JsonNode item in (jsonObject[text2] as JsonArray) ?? throw new InvalidDataException("BEGINNER_KIT group '" + text2 + "' is missing."))
			{
				JsonObject jsonObject2 = item.AsObject();
				string value = jsonObject2["key"].GetValue<string>();
				long value2 = jsonObject2["count"].GetValue<long>();
				if (!(text == "mage") || !(value == "l1j_item_35"))
				{
					list.Add((value, value2));
				}
			}
		}
		list.Add(("potion_heal", 100L));
		list.Add(("potion_haste", 20L));
		list.Add(("unsealed_card_normal", 10L));
		if (string.Equals(text, "mage", StringComparison.Ordinal))
		{
			list.Add(("bk_lightarrow", 1L));
		}
		return list;
	}

	public static void Grant(IGameData data, Combatant player, string classId)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		foreach (var item3 in Items(data, classId))
		{
			string item = item3.ItemKey;
			long item2 = item3.Count;
			int num = PreEnhancedLootRules.SafeEnchantOf(data.Item(item) ?? throw new InvalidDataException($"Beginner item '{item}' is missing from DB.items（LoadError: {LoadError(data, "DB")}）."));
			if (num <= 0)
			{
				CombatInventory.Add(data, player, item, item2);
				continue;
			}
			CombatInventory.Add(data, player, new ItemStack(CombatInventory.NextUid(player), item, item2)
			{
				Enhancement = num
			});
		}
	}

	public static void GrantCreationSkills(Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		player.LearnedSkills.Add("sk_charm");
	}

	public static bool RemoveLegacyCreationSkill(Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (IsLegacyCreationSkill(player, "sk_lightarrow"))
		{
			return player.LearnedSkills.Remove("sk_lightarrow");
		}
		return false;
	}

	public static bool IsLegacyCreationSkill(Combatant player, string skillId)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (string.Equals(skillId, "sk_lightarrow", StringComparison.Ordinal) && player.Level <= 1)
		{
			return string.Equals(ClassKitRegistry.NormalizeClassId(player.ClassId), "mage", StringComparison.Ordinal);
		}
		return false;
	}

	private static JsonObject ReadGroups(IGameData data)
	{
		if (!(data.Table("BEGINNER_KIT") is JsonObject jsonObject) || !(jsonObject["groups"] is JsonObject result))
		{
			throw new InvalidDataException($"BEGINNER_KIT table failed to load（LoadError: {LoadError(data, "BEGINNER_KIT")}·DB: {LoadError(data, "DB")}）.");
		}
		return result;
	}

	private static string LoadError(IGameData data, string tableName)
	{
		if (!(data is GameData gameData))
		{
			return "n/a";
		}
		return gameData.LoadError(tableName) ?? "null";
	}
}
