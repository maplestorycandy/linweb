using System;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class ClassicInventoryTabRules
{
	public static bool Matches(IGameData data, string itemKey, ClassicInventoryTab tab)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject? item = data.Item(itemKey);
		ClassicInventoryTab resolvedTab = ResolveTab(data, itemKey, item);
		return resolvedTab == tab;
	}

	public static ClassicInventoryTab ResolveTab(IGameData data, string itemKey, JsonObject? item)
	{
		if (IsEquipment(itemKey, item))
		{
			return ClassicInventoryTab.Equipment;
		}
		if (IsPotion(data, itemKey, item))
		{
			return ClassicInventoryTab.Potion;
		}
		if (IsScroll(data, itemKey, item))
		{
			return ClassicInventoryTab.Scroll;
		}
		return ClassicInventoryTab.Other;
	}

	private static bool IsEquipment(string itemKey, JsonObject? item)
	{
		if (item == null) return false;
		string type = TypeOf(item) ?? "";
		if (type == "wpn" || type == "arm" || type == "acc") return true;
		if (ReadBool(item, "isArrow") || ReadBool(item, "isSting")) return true;
		string slot = ReadString(item, "slot");
		if (slot.Length > 0)
		{
			switch (slot)
			{
			case "helm":
			case "armor":
			case "tshirt":
			case "cloak":
			case "gloves":
			case "boots":
			case "shield":
			case "amulet":
			case "belt":
			case "ring":
			case "ear":
			case "lantern":
			case "ring1":
			case "ring2":
			case "ring3":
			case "ring4":
			case "ear1":
			case "ear2":
				return true;
			default:
				if (slot.StartsWith("rem_", StringComparison.Ordinal)) return true;
				break;
			}
		}
		return false;
	}

	private static bool IsPotion(IGameData data, string itemKey, JsonObject? item)
	{
		if (item == null) return false;
		if (ConsumableRules.IsConsumableItem(data, itemKey, item)) return true;
		if (L1jElixirRules.TryRead(data, itemKey, out _)) return true;
		if (ReadBool(item, "thirdHaste") || ReadString(item, "eff") == "third_haste" || itemKey == "l1j_item_49138") return true;
		return false;
	}

	private static bool IsScroll(IGameData data, string itemKey, JsonObject? item)
	{
		if (item == null) return false;
		string type = TypeOf(item) ?? "";
		if (type == "scroll" || type == "skillbk") return true;
		if (itemKey.StartsWith("scroll_", StringComparison.OrdinalIgnoreCase) ||
		    itemKey.StartsWith("bk_", StringComparison.OrdinalIgnoreCase) ||
		    itemKey.StartsWith("skill_", StringComparison.OrdinalIgnoreCase) ||
		    itemKey.StartsWith("card_", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string eff = ReadString(item, "eff");
		switch (eff)
		{
		case "scroll":
		case "teleport":
		case "poly":
		case "return":
		case "uncurse":
		case "res":
		case "pride_unseal":
			return true;
		}
		int l1jId = CombatSkill.ReadInt(item, "l1jItemId");
		if (l1jId is 40079 or 40095 or 40119 or 40090 or 40091 or 40092 or 40093 or 40094 or 40098 or 40110 or 40113 or 40126 or 40168)
		{
			return true;
		}
		string name = ReadString(item, "n");
		if (name.Contains("魔法書", StringComparison.Ordinal) ||
		    name.Contains("卷軸", StringComparison.Ordinal) ||
		    name.Contains("水晶", StringComparison.Ordinal))
		{
			return true;
		}
		return false;
	}

	private static string? TypeOf(JsonObject? definition)
	{
		if (!(definition?["type"] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}

	private static string ReadString(JsonObject? item, string key)
	{
		return item?[key]?.GetValue<string>() ?? "";
	}

	private static bool ReadBool(JsonObject? item, string key)
	{
		return item?[key] is JsonValue val && val.TryGetValue<bool>(out bool b) && b;
	}
}
