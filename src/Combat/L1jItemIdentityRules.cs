using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jItemIdentityRules
{
	public static string DisplayName(IGameData data, ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		string text = DisplayName(data, item.ItemKey, item.IsIdentified);
		if (!item.IsIdentified)
		{
			return text;
		}
		return item.Blessing switch
		{
			ItemBlessing.Blessed => (text.StartsWith("受祝福的 ") || text.StartsWith("祝福的 ")) ? text : ("祝福的 " + text), 
			ItemBlessing.Cursed => (text.StartsWith("受詛咒的 ") || text.StartsWith("詛咒的 ")) ? text : ("受詛咒的 " + text), 
			_ => text, 
		};
	}

	public static string DisplayName(IGameData data, string itemKey, bool isIdentified)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (itemKey == "item_guardian_soul")
		{
			return "守護者的靈魂";
		}
		JsonObject definition = data.Item(itemKey);
		if (isIdentified)
		{
			return ReadText(definition, "l1jIdentifiedName") ?? ReadText(definition, "n") ?? itemKey;
		}
		string text = ReadText(definition, "l1jUnidentifiedName");
		if (text != null)
		{
			return text;
		}
		switch (ReadText(definition, "type"))
		{
		case "wpn":
			return "未鑑定的武器";
		case "arm":
		case "acc":
			return "未鑑定的防具";
		case "pot":
			return "未鑑定的藥水";
		case "skillbk":
			return "未鑑定的魔法書";
		default:
			return "未鑑定的物品";
		}
	}

	private static string? ReadText(JsonObject? definition, string field)
	{
		if (!(definition?[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		return value;
	}
}
