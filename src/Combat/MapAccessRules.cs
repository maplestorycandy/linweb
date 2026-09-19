using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MapAccessRules
{
	private readonly record struct PrideAccessItems(string PermanentItemKey, string ScrollItemKey);

	public static MapAccessResult Evaluate(IGameData data, Combatant player, MapAccessState state, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(player, "player");
		ArgumentNullException.ThrowIfNull(state, "state");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		return Evaluate(data, player, state, WorldMapCatalog.GetDestination(data, mapKey));
	}

	public static MapAccessResult Evaluate(IGameData data, Combatant player, MapAccessState state, MapDestination destination)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(player, "player");
		ArgumentNullException.ThrowIfNull(state, "state");
		ArgumentNullException.ThrowIfNull(destination, "destination");
		string consumedKeyRequirement = destination.ConsumedKeyRequirement;
		if (consumedKeyRequirement != null)
		{
			if (data.Item(consumedKeyRequirement) == null)
			{
				return MapAccessResult.Denied(MapAccessFailure.MissingItemDefinition, consumedKeyRequirement);
			}
			if (!HasItem(player, consumedKeyRequirement))
			{
				return MapAccessResult.Denied(MapAccessFailure.MissingConsumedKey, consumedKeyRequirement);
			}
		}
		string questRequirement = destination.QuestRequirement;
		if (questRequirement != null && !state.QuestFlags.Contains(questRequirement))
		{
			return MapAccessResult.Denied(MapAccessFailure.MissingQuest, questRequirement);
		}
		string heldKeyRequirement = destination.HeldKeyRequirement;
		if (heldKeyRequirement != null)
		{
			if (data.Item(heldKeyRequirement) == null)
			{
				return MapAccessResult.Denied(MapAccessFailure.MissingItemDefinition, heldKeyRequirement);
			}
			if (!HasItem(player, heldKeyRequirement))
			{
				return MapAccessResult.Denied(MapAccessFailure.MissingHeldKey, heldKeyRequirement);
			}
		}
		string prideBossRequirement = destination.PrideBossRequirement;
		if (prideBossRequirement != null && !state.DefeatedBossKeys.Contains(prideBossRequirement))
		{
			return MapAccessResult.Denied(MapAccessFailure.MissingPrideBoss, prideBossRequirement);
		}
		string consumedItemKey = destination.ConsumedKeyRequirement ?? "";
		int? prideFloorRequirement = destination.PrideFloorRequirement;
		if (prideFloorRequirement.HasValue)
		{
			int valueOrDefault = prideFloorRequirement.GetValueOrDefault();
			PrideAccessItems prideAccessItems = FindPrideAccessItems(data, player, valueOrDefault);
			if (prideAccessItems.PermanentItemKey.Length == 0 && prideAccessItems.ScrollItemKey.Length == 0)
			{
				return MapAccessResult.Denied(MapAccessFailure.MissingPrideAccessItem, "", valueOrDefault);
			}
			if (prideAccessItems.PermanentItemKey.Length == 0)
			{
				consumedItemKey = prideAccessItems.ScrollItemKey;
			}
		}
		return MapAccessResult.Granted(consumedItemKey);
	}

	public static MapAccessResult TryEnter(IGameData data, Combatant player, MapAccessState state, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(player, "player");
		ArgumentNullException.ThrowIfNull(state, "state");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		return TryEnter(data, player, state, WorldMapCatalog.GetDestination(data, mapKey));
	}

	public static MapAccessResult TryEnter(IGameData data, Combatant player, MapAccessState state, MapDestination destination)
	{
		MapAccessResult evaluation = Evaluate(data, player, state, destination);
		if (!evaluation.Allowed || !evaluation.ConsumesItem)
		{
			return evaluation;
		}
		List<ItemStack> list;
		try
		{
			list = (from item in ItemStackInventory.CopyAll(player.InventoryStacks)
				select item.Copy()).ToList();
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			return MapAccessResult.Denied(MapAccessFailure.CorruptInventory);
		}
		int num = list.FindIndex((ItemStack item) => item.ItemKey == evaluation.ConsumedItemKey && item.Quantity > 0);
		if (num < 0)
		{
			return MapAccessResult.Denied(MapAccessFailure.MissingConsumedKey, evaluation.ConsumedItemKey);
		}
		long value;
		if (list[num].Quantity == 1)
		{
			list.RemoveAt(num);
		}
		else
		{
			value = list[num].Quantity--;
		}
		IReadOnlyDictionary<string, long> readOnlyDictionary;
		try
		{
			readOnlyDictionary = ItemStackInventory.ToPlainCounts(list);
		}
		catch (OverflowException)
		{
			return MapAccessResult.Denied(MapAccessFailure.CorruptInventory);
		}
		player.InventoryStacks = list;
		player.Inventory.Clear();
		foreach (KeyValuePair<string, long> item in readOnlyDictionary)
		{
			item.Deconstruct(out var key, out value);
			string key2 = key;
			long value2 = value;
			player.Inventory[key2] = value2;
		}
		return evaluation;
	}

	private static bool HasItem(Combatant player, string itemKey)
	{
		return player.InventoryStacks.Any((ItemStack item) => item.ItemKey == itemKey && item.Quantity > 0);
	}

	private static PrideAccessItems FindPrideAccessItems(IGameData data, Combatant player, int floor)
	{
		string text = "";
		string text2 = "";
		foreach (ItemStack inventoryStack in player.InventoryStacks)
		{
			if (inventoryStack.Quantity <= 0)
			{
				continue;
			}
			JsonObject jsonObject = data.Item(inventoryStack.ItemKey);
			if (jsonObject == null || ReadInt(jsonObject, "prideTier") != floor)
			{
				continue;
			}
			switch (ReadString(jsonObject, "prideKind"))
			{
			case "pass":
			case "dom":
				text = inventoryStack.ItemKey;
				break;
			case "scroll":
				if (text2.Length == 0)
				{
					text2 = inventoryStack.ItemKey;
				}
				break;
			}
			if (text.Length > 0)
			{
				break;
			}
		}
		return new PrideAccessItems(text, text2);
	}

	private static int ReadInt(JsonObject source, string propertyName)
	{
		JsonNode jsonNode = source[propertyName];
		if (jsonNode is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value))
		{
			return value;
		}
		if (jsonNode is JsonValue jsonValue2 && jsonValue2.TryGetValue<long>(out var value2) && value2 >= int.MinValue && value2 <= int.MaxValue)
		{
			return (int)value2;
		}
		return 0;
	}

	private static string ReadString(JsonObject source, string propertyName)
	{
		if (!(source[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}
}
