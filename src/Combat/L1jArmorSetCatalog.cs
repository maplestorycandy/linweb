using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jArmorSetCatalog
{
	public const string TableName = "L1J_ARMOR_SETS";

	private static readonly ConditionalWeakTable<IGameData, L1jArmorSetCatalog> Cache = new ConditionalWeakTable<IGameData, L1jArmorSetCatalog>();

	public IReadOnlyList<L1jArmorSetDefinition> Sets { get; }

	private L1jArmorSetCatalog(IReadOnlyList<L1jArmorSetDefinition> sets)
	{
		Sets = sets;
	}

	public static L1jArmorSetCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build);
	}

	private static L1jArmorSetCatalog Build(IGameData data)
	{
		JsonArray obj = (((data.Table("L1J_ARMOR_SETS") as JsonObject) ?? throw new InvalidDataException("L1J_ARMOR_SETS must be an object."))["sets"] as JsonArray) ?? throw new InvalidDataException("L1J_ARMOR_SETS.sets must be an array.");
		List<L1jArmorSetDefinition> list = new List<L1jArmorSetDefinition>(obj.Count);
		HashSet<int> hashSet = new HashSet<int>();
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		foreach (JsonNode item in obj)
		{
			JsonObject jsonObject = (item as JsonObject) ?? throw new InvalidDataException("L1J_ARMOR_SETS.sets row must be an object.");
			int num = RequiredInt(jsonObject, "setId");
			string text = RequiredString(jsonObject, "code");
			int[] array = ((jsonObject["itemIds"] as JsonArray) ?? throw new InvalidDataException("L1J_ARMOR_SETS." + text + ".itemIds must be an array.")).Select((JsonNode value) => value?.GetValue<int>() ?? 0).ToArray();
			JsonObject row = (jsonObject["bonuses"] as JsonObject) ?? throw new InvalidDataException("L1J_ARMOR_SETS." + text + ".bonuses must be an object.");
			if (!hashSet.Add(num) || !hashSet2.Add(text) || array.Length == 0 || array.Any((int id) => id <= 0) || array.Distinct().Count() != array.Length)
			{
				throw new InvalidDataException($"Invalid {"L1J_ARMOR_SETS"} row '{text}'.");
			}
			list.Add(new L1jArmorSetDefinition(num, text, RequiredString(jsonObject, "name"), new ReadOnlyCollection<int>(array), RequiredInt(jsonObject, "polyId"), OptionalString(jsonObject, "morphName"), RequiredString(jsonObject, "description"), new L1jArmorSetBonus(RequiredInt(row, "ac"), RequiredInt(row, "hp"), RequiredInt(row, "mp"), RequiredInt(row, "hpr"), RequiredInt(row, "mpr"), RequiredInt(row, "mr"), RequiredInt(row, "str"), RequiredInt(row, "dex"), RequiredInt(row, "con"), RequiredInt(row, "wis"), RequiredInt(row, "cha"), RequiredInt(row, "int"), RequiredInt(row, "resistWater"), RequiredInt(row, "resistWind"), RequiredInt(row, "resistFire"), RequiredInt(row, "resistEarth"))));
		}
		if (list.Count != 69)
		{
			throw new InvalidDataException($"{"L1J_ARMOR_SETS"} must contain main's 69 rows; got {list.Count}.");
		}
		return new L1jArmorSetCatalog(new ReadOnlyCollection<L1jArmorSetDefinition>(list.OrderBy((L1jArmorSetDefinition definition) => definition.SetId).ToArray()));
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_ARMOR_SETS." + name + " must be an integer.")).GetValue<int>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_ARMOR_SETS." + name + " must be a string.");
	}

	private static string? OptionalString(JsonObject row, string name)
	{
		if (!(row[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}
}
