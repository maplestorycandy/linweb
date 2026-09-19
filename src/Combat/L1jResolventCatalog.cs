using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jResolventCatalog
{
	public const string TableName = "L1J_RESOLVENT";

	private readonly IReadOnlyDictionary<(string, ItemBlessing), L1jResolventDefinition> _rules;

	public int RuntimeRuleCount => _rules.Count;

	private L1jResolventCatalog(IReadOnlyDictionary<(string, ItemBlessing), L1jResolventDefinition> rules)
	{
		_rules = rules;
	}

	public bool TryResolve(ItemStack stack, out L1jResolventDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		return _rules.TryGetValue((stack.ItemKey, stack.Blessing), out definition);
	}

	public static L1jResolventCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonArray jsonArray = (((data.Table("L1J_RESOLVENT") as JsonObject) ?? throw new InvalidDataException("L1J_RESOLVENT must be an object."))["rows"] as JsonArray) ?? throw new InvalidDataException("L1J_RESOLVENT.rows must be an array.");
		if (jsonArray.Count != 511)
		{
			throw new InvalidDataException($"{"L1J_RESOLVENT"} must contain 511 source rows; got {jsonArray.Count}.");
		}
		Dictionary<(string, ItemBlessing), L1jResolventDefinition> dictionary = new Dictionary<(string, ItemBlessing), L1jResolventDefinition>();
		ItemBlessing itemBlessing = default(ItemBlessing);
		foreach (JsonNode item in jsonArray)
		{
			if (!(item is JsonObject jsonObject))
			{
				throw new InvalidDataException("Every L1J_RESOLVENT row must be an object.");
			}
			JsonNode? jsonNode = jsonObject["runtimeResolved"];
			if (jsonNode == null || !jsonNode.GetValue<bool>())
			{
				continue;
			}
			string text = RequiredString(jsonObject, "runtimeItemKey");
			if (data.Item(text) == null)
			{
				throw new InvalidDataException("L1J_RESOLVENT references missing item '" + text + "'.");
			}
			string text2 = RequiredString(jsonObject, "runtimeBlessing");
			switch (text2)
			{
			default:
				if (text2 != null)
				{
					throw new InvalidDataException("L1J_RESOLVENT has invalid blessing '" + text2 + "'.");
				}
				throw new System.Runtime.CompilerServices.SwitchExpressionException(text2);
				break;
			case "normal":
				itemBlessing = ItemBlessing.Normal;
				break;
			case "blessed":
				itemBlessing = ItemBlessing.Blessed;
				break;
			case "cursed":
				itemBlessing = ItemBlessing.Cursed;
				break;
			}
			ItemBlessing itemBlessing2 = itemBlessing;
			L1jResolventDefinition l1jResolventDefinition = new L1jResolventDefinition(RequiredInt(jsonObject, "itemId"), text, itemBlessing2, RequiredString(jsonObject, "note"), RequiredInt(jsonObject, "crystalCount"));
			if (l1jResolventDefinition.CrystalCount <= 0)
			{
				throw new InvalidDataException($"{"L1J_RESOLVENT"} item {l1jResolventDefinition.ItemId} has no yield.");
			}
			(string, ItemBlessing) tuple = (text, itemBlessing2);
			if (dictionary.TryGetValue(tuple, out var value))
			{
				if (value.CrystalCount != l1jResolventDefinition.CrystalCount)
				{
					throw new InvalidDataException($"{"L1J_RESOLVENT"} has conflicting rules for {tuple}.");
				}
			}
			else
			{
				dictionary.Add(tuple, l1jResolventDefinition);
			}
		}
		if (dictionary.Count != 497)
		{
			throw new InvalidDataException($"{"L1J_RESOLVENT"} must expose 497 runtime rules; got {dictionary.Count}.");
		}
		return new L1jResolventCatalog(new ReadOnlyDictionary<(string, ItemBlessing), L1jResolventDefinition>(dictionary));
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_RESOLVENT." + name + " must be an integer.")).GetValue<int>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_RESOLVENT." + name + " must be a string.");
	}
}
