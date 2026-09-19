using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jPetItemCatalog
{
	public const string TableName = "L1J_PET_ITEMS";

	private static readonly ConditionalWeakTable<IGameData, L1jPetItemCatalog> Cache = new ConditionalWeakTable<IGameData, L1jPetItemCatalog>();

	public IReadOnlyDictionary<string, L1jPetItemDefinition> ByItemKey { get; }

	public IReadOnlyDictionary<int, L1jPetItemDefinition> ByItemId { get; }

	private L1jPetItemCatalog(IReadOnlyDictionary<string, L1jPetItemDefinition> byItemKey, IReadOnlyDictionary<int, L1jPetItemDefinition> byItemId)
	{
		ByItemKey = byItemKey;
		ByItemId = byItemId;
	}

	public bool TryGet(string itemKey, out L1jPetItemDefinition definition)
	{
		return ByItemKey.TryGetValue(itemKey, out definition);
	}

	public static L1jPetItemCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build);
	}

	private static L1jPetItemCatalog Build(IGameData data)
	{
		JsonArray obj = (((data.Table("L1J_PET_ITEMS") as JsonObject) ?? throw new InvalidDataException("L1J_PET_ITEMS must be an object."))["items"] as JsonArray) ?? throw new InvalidDataException("L1J_PET_ITEMS.items must be an array.");
		Dictionary<string, L1jPetItemDefinition> dictionary = new Dictionary<string, L1jPetItemDefinition>(StringComparer.Ordinal);
		Dictionary<int, L1jPetItemDefinition> dictionary2 = new Dictionary<int, L1jPetItemDefinition>();
		foreach (JsonNode item in obj)
		{
			JsonObject jsonObject = (item as JsonObject) ?? throw new InvalidDataException("L1J_PET_ITEMS.items row must be an object.");
			string text = RequiredString(jsonObject, "itemKey");
			string text2 = RequiredString(jsonObject, "slot");
			JsonObject row = (jsonObject["stats"] as JsonObject) ?? throw new InvalidDataException("L1J_PET_ITEMS." + text + ".stats must be an object.");
			L1jPetItemDefinition l1jPetItemDefinition = new L1jPetItemDefinition(RequiredInt(jsonObject, "itemId"), text, RequiredString(jsonObject, "name"), text2, new L1jPetItemStats(RequiredInt(row, "hitModifier"), RequiredInt(row, "damageModifier"), RequiredInt(row, "armorClass"), RequiredInt(row, "strength"), RequiredInt(row, "constitution"), RequiredInt(row, "dexterity"), RequiredInt(row, "intelligence"), RequiredInt(row, "wisdom"), RequiredInt(row, "maxHp"), RequiredInt(row, "maxMp"), RequiredInt(row, "spellPower"), RequiredInt(row, "magicResist")));
			if ((!(text2 == "petwpn") && !(text2 == "petarm")) || data.Item(text) == null || !dictionary.TryAdd(text, l1jPetItemDefinition) || !dictionary2.TryAdd(l1jPetItemDefinition.ItemId, l1jPetItemDefinition))
			{
				throw new InvalidDataException($"Invalid {"L1J_PET_ITEMS"} row '{text}'.");
			}
		}
		if (dictionary.Count != 13 || dictionary.Values.Count((L1jPetItemDefinition value) => value.Slot == "petwpn") != 7 || dictionary.Values.Count((L1jPetItemDefinition value) => value.Slot == "petarm") != 6)
		{
			throw new InvalidDataException("L1J_PET_ITEMS must contain main's 7 weapons and 6 armors.");
		}
		return new L1jPetItemCatalog(new ReadOnlyDictionary<string, L1jPetItemDefinition>(dictionary), new ReadOnlyDictionary<int, L1jPetItemDefinition>(dictionary2));
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_PET_ITEMS." + name + " must be an integer.")).GetValue<int>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_PET_ITEMS." + name + " must be a string.");
	}
}
