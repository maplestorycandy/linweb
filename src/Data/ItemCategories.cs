using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class ItemCategories
{
	public static readonly IReadOnlyList<ItemCategory> Selectable = new ItemCategory[7]
	{
		ItemCategory.All,
		ItemCategory.Weapon,
		ItemCategory.Armor,
		ItemCategory.Accessory,
		ItemCategory.Consumable,
		ItemCategory.SkillBook,
		ItemCategory.Other
	};

	public static string Name(ItemCategory category)
	{
		return category switch
		{
			ItemCategory.All => "全部", 
			ItemCategory.Weapon => "武器", 
			ItemCategory.Armor => "防具", 
			ItemCategory.Accessory => "飾品", 
			ItemCategory.Consumable => "消耗", 
			ItemCategory.SkillBook => "魔法書", 
			_ => "其他", 
		};
	}

	public static ItemCategory Of(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrEmpty(itemKey))
		{
			return ItemCategory.Other;
		}
		return FromType(TypeOf(data.Item(itemKey)));
	}

	public static ItemCategory FromType(string? type)
	{
		switch (type)
		{
		case "wpn":
			return ItemCategory.Weapon;
		case "arm":
			return ItemCategory.Armor;
		case "acc":
			return ItemCategory.Accessory;
		case "pot":
		case "scroll":
			return ItemCategory.Consumable;
		case "skillbk":
			return ItemCategory.SkillBook;
		default:
			return ItemCategory.Other;
		}
	}

	public static bool Matches(IGameData data, string itemKey, ItemCategory filter)
	{
		if (filter != ItemCategory.All)
		{
			return Of(data, itemKey) == filter;
		}
		return true;
	}

	private static string? TypeOf(JsonObject? definition)
	{
		if (!(definition?["type"] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}
}
