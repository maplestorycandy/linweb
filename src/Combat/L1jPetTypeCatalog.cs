using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jPetTypeCatalog
{
	public const string TableName = "L1J_PET_TYPES";

	private static readonly ConditionalWeakTable<IGameData, L1jPetTypeCatalog> Cache = new ConditionalWeakTable<IGameData, L1jPetTypeCatalog>();

	public IReadOnlyDictionary<string, L1jPetTypeDefinition> ByForm { get; }

	private L1jPetTypeCatalog(IReadOnlyDictionary<string, L1jPetTypeDefinition> byForm)
	{
		ByForm = byForm;
	}

	public bool TryGet(string form, out L1jPetTypeDefinition definition)
	{
		if (ByForm.TryGetValue(form, out definition))
		{
			return true;
		}
		string text = form switch
		{
			"真‧虎男" => "真．虎男", 
			"高等淘氣龍" => "高級淘氣龍", 
			"高等頑皮龍" => "高級頑皮龍", 
			_ => string.Empty, 
		};
		if (text.Length > 0)
		{
			return ByForm.TryGetValue(text, out definition);
		}
		return false;
	}

	public static L1jPetTypeCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build);
	}

	private static L1jPetTypeCatalog Build(IGameData data)
	{
		JsonObject obj = (((data.Table("L1J_PET_TYPES") as JsonObject) ?? throw new InvalidDataException("L1J_PET_TYPES must be an object."))["byForm"] as JsonObject) ?? throw new InvalidDataException("L1J_PET_TYPES.byForm must be an object.");
		Dictionary<string, L1jPetTypeDefinition> dictionary = new Dictionary<string, L1jPetTypeDefinition>(StringComparer.Ordinal);
		HashSet<int> hashSet = new HashSet<int>();
		foreach (KeyValuePair<string, JsonNode> item in obj)
		{
			item.Deconstruct(out var key, out var value);
			string text = key;
			JsonObject jsonObject = (value as JsonObject) ?? throw new InvalidDataException("L1J_PET_TYPES." + text + " must be an object.");
			JsonArray jsonArray = RequiredPair(jsonObject, "hpGrowth");
			JsonArray jsonArray2 = RequiredPair(jsonObject, "mpGrowth");
			JsonArray jsonArray3 = (jsonObject["messageIds"] as JsonArray) ?? throw new InvalidDataException("L1J_PET_TYPES." + text + ".messageIds must be an array.");
			if (jsonArray3.Count != 5)
			{
				throw new InvalidDataException("L1J_PET_TYPES." + text + " must preserve five message IDs.");
			}
			L1jPetTypeDefinition l1jPetTypeDefinition = new L1jPetTypeDefinition(RequiredInt(jsonObject, "baseNpcId"), text, RequiredInt(jsonObject, "tamingItemId"), OptionalString(jsonObject, "tamingItemKey"), jsonArray[0].GetValue<int>(), jsonArray[1].GetValue<int>(), jsonArray2[0].GetValue<int>(), jsonArray2[1].GetValue<int>(), RequiredInt(jsonObject, "evolutionItemId"), OptionalString(jsonObject, "evolutionItemKey"), RequiredInt(jsonObject, "evolutionNpcId"), OptionalString(jsonObject, "evolutionForm"), new ReadOnlyCollection<int>(jsonArray3.Select((JsonNode jsonNode) => jsonNode.GetValue<int>()).ToArray()), RequiredInt(jsonObject, "defyMessageId"), OptionalBool(jsonObject, "userEvolutionRuling") ? "explicit user evolution ruling" : null);
			if (!hashSet.Add(l1jPetTypeDefinition.BaseNpcId) || l1jPetTypeDefinition.HpGrowthMin > l1jPetTypeDefinition.HpGrowthMax || l1jPetTypeDefinition.MpGrowthMin > l1jPetTypeDefinition.MpGrowthMax || (l1jPetTypeDefinition.TamingItemId > 0 && l1jPetTypeDefinition.TamingItemKey == null) || (l1jPetTypeDefinition.EvolutionItemId > 0 && l1jPetTypeDefinition.EvolutionItemKey == null) || (l1jPetTypeDefinition.EvolutionNpcId > 0 && l1jPetTypeDefinition.EvolutionForm == null))
			{
				throw new InvalidDataException("Invalid pettypes row for '" + text + "'.");
			}
			dictionary.Add(text, l1jPetTypeDefinition);
		}
		if (dictionary.Count != 37)
		{
			throw new InvalidDataException($"{"L1J_PET_TYPES"} must contain main's 37 rows; got {dictionary.Count}.");
		}
		return new L1jPetTypeCatalog(new ReadOnlyDictionary<string, L1jPetTypeDefinition>(dictionary));
	}

	private static JsonArray RequiredPair(JsonObject row, string name)
	{
		if (!(row[name] is JsonArray { Count: 2 } jsonArray))
		{
			throw new InvalidDataException("L1J_PET_TYPES." + name + " must be a pair.");
		}
		return jsonArray;
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_PET_TYPES." + name + " must be an integer.")).GetValue<int>();
	}

	private static string? OptionalString(JsonObject row, string name)
	{
		if (!(row[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}

	private static bool OptionalBool(JsonObject row, string name)
	{
		bool value = default(bool);
		return row[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
