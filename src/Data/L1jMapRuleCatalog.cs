using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public sealed class L1jMapRuleCatalog
{
	public const string TableName = "L1J_MAP_RULES";

	public const int ExpectedRuleCount = 555;

	private static readonly ConditionalWeakTable<IGameData, L1jMapRuleCatalog> Cache = new ConditionalWeakTable<IGameData, L1jMapRuleCatalog>();

	public IReadOnlyDictionary<int, L1jMapRule> ById { get; }

	public IReadOnlyDictionary<string, L1jMapRule> ByKey { get; }

	public IReadOnlyDictionary<int, string> TargetAliases { get; }

	private L1jMapRuleCatalog(IReadOnlyDictionary<int, L1jMapRule> byId, IReadOnlyDictionary<string, L1jMapRule> byKey, IReadOnlyDictionary<int, string> targetAliases)
	{
		ById = byId;
		ByKey = byKey;
		TargetAliases = targetAliases;
	}

	public static L1jMapRuleCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build);
	}

	private static L1jMapRuleCatalog Build(IGameData data)
	{
		JsonObject obj = (data.Table("L1J_MAP_RULES") as JsonObject) ?? throw Invalid("table must be a JSON object");
		if (!(obj["maps"] is JsonArray jsonArray))
		{
			throw Invalid("maps must be an array");
		}
		if (!(obj["mapIdByKey"] is JsonObject jsonObject))
		{
			throw Invalid("mapIdByKey must be an object");
		}
		if (!(obj["targetAliases"] is JsonObject jsonObject2))
		{
			throw Invalid("targetAliases must be an object");
		}
		Dictionary<int, L1jMapRule> dictionary = new Dictionary<int, L1jMapRule>();
		foreach (JsonNode item in jsonArray)
		{
			if (item is not JsonObject owner) continue;
			JsonObject? owner2 = owner["bounds"] as JsonObject;
			int startX = owner2?["startX"]?.GetValue<int>() ?? 0;
			int endX = owner2?["endX"]?.GetValue<int>() ?? 65535;
			int startY = owner2?["startY"]?.GetValue<int>() ?? 0;
			int endY = owner2?["endY"]?.GetValue<int>() ?? 65535;

			int num = owner["mapId"]?.GetValue<int>() ?? 0;
			string locName = owner["locationName"]?.GetValue<string>() ?? "";
			double monsterAmount = owner["monsterAmount"]?.GetValue<double>() ?? 1.0;
			double dropRate = owner["dropRate"]?.GetValue<double>() ?? 1.0;
			bool underwater = owner["underwater"]?.GetValue<bool>() ?? false;
			bool markable = owner["markable"]?.GetValue<bool>() ?? true;
			bool teleportable = owner["teleportable"]?.GetValue<bool>() ?? true;
			bool escapable = owner["escapable"]?.GetValue<bool>() ?? true;
			bool resurrection = owner["resurrection"]?.GetValue<bool>() ?? true;
			bool painwand = owner["painwand"]?.GetValue<bool>() ?? true;
			bool penalty = owner["penalty"]?.GetValue<bool>() ?? true;
			bool takePets = owner["takePets"]?.GetValue<bool>() ?? true;
			bool recallPets = owner["recallPets"]?.GetValue<bool>() ?? true;
			bool usableItem = owner["usableItem"]?.GetValue<bool>() ?? true;
			bool usableSkill = owner["usableSkill"]?.GetValue<bool>() ?? true;

			List<string> mapKeysList = new();
			if (owner["mapKeys"] is JsonArray mka)
			{
				foreach (var k in mka)
				{
					var ks = k?.GetValue<string>();
					if (!string.IsNullOrEmpty(ks)) mapKeysList.Add(ks);
				}
			}

			L1jMapRule value = new L1jMapRule(
				num,
				locName,
				new L1jMapBounds(startX, endX, startY, endY),
				monsterAmount,
				dropRate,
				underwater,
				markable,
				teleportable,
				escapable,
				resurrection,
				painwand,
				penalty,
				takePets,
				recallPets,
				usableItem,
				usableSkill,
				mapKeysList.AsReadOnly()
			);
			dictionary[num] = value;
		}

		Dictionary<string, L1jMapRule> dictionary2 = new Dictionary<string, L1jMapRule>(StringComparer.Ordinal);
		string key;
		JsonNode value2;
		foreach (KeyValuePair<string, JsonNode> item2 in jsonObject)
		{
			item2.Deconstruct(out key, out value2);
			string text = key;
			if (value2 == null) continue;
			int value3 = value2.GetValue<int>();
			if (!dictionary.TryGetValue(value3, out var value4))
			{
				continue;
			}
			dictionary2[text] = value4;
		}
		foreach (L1jMapRule value7 in dictionary.Values)
		{
			foreach (string mapKey in value7.MapKeys)
			{
				if (!dictionary2.ContainsKey(mapKey))
				{
					dictionary2[mapKey] = value7;
				}
			}
		}
		Dictionary<int, string> dictionary3 = new Dictionary<int, string>();
		foreach (KeyValuePair<string, JsonNode> item3 in jsonObject2)
		{
			item3.Deconstruct(out key, out value2);
			string text2 = key;
			JsonNode jsonNode = value2;
			if (!int.TryParse(text2, out var result) || !dictionary.TryGetValue(result, out var value6))
			{
				continue;
			}
			string? text3 = jsonNode?.GetValue<string>();
			if (!string.IsNullOrEmpty(text3) && dictionary2.ContainsKey(text3))
			{
				dictionary3[result] = text3;
			}
		}
		return new L1jMapRuleCatalog(new ReadOnlyDictionary<int, L1jMapRule>(dictionary), new ReadOnlyDictionary<string, L1jMapRule>(dictionary2), new ReadOnlyDictionary<int, string>(dictionary3));
	}

	public bool TryForMapId(int mapId, out L1jMapRule? rule)
	{
		return ById.TryGetValue(mapId, out rule);
	}

	public IReadOnlyList<string> RuntimeTargetKeys(int mapId)
	{
		if (!ById.TryGetValue(mapId, out L1jMapRule value))
		{
			return Array.Empty<string>();
		}
		if (value.MapKeys.Count > 0)
		{
			return value.MapKeys;
		}
		if (!TargetAliases.TryGetValue(mapId, out string value2))
		{
			return Array.Empty<string>();
		}
		return new string[1] { value2 };
	}

	public bool TryForMapKey(string mapKey, out L1jMapRule? rule)
	{
		if (string.IsNullOrWhiteSpace(mapKey))
		{
			rule = null;
			return false;
		}
		return ByKey.TryGetValue(mapKey, out rule);
	}

	public L1jMapRule RequireForMapKey(string mapKey)
	{
		if (TryForMapKey(mapKey, out L1jMapRule rule) && (object)rule != null)
		{
			return rule;
		}
		return new L1jMapRule(0, mapKey ?? "Unknown Map", new L1jMapBounds(0, 65535, 0, 65535), 1.0, 1.0, false, true, true, true, true, true, true, true, true, true, true, Array.Empty<string>());
	}

	private static IReadOnlyList<string> ReadStrings(JsonObject owner, string name)
	{
		return new ReadOnlyCollection<string>(((owner[name] as JsonArray) ?? throw Invalid(name + " must be an array")).Select((JsonNode node) => node?.GetValue<string>() ?? throw Invalid(name + " contains null")).ToList());
	}

	private static JsonObject RequiredObject(JsonObject owner, string name)
	{
		return (owner[name] as JsonObject) ?? throw Invalid(name + " must be an object");
	}

	private static string RequiredString(JsonObject owner, string name)
	{
		return owner[name]?.GetValue<string>() ?? throw Invalid(name + " must be a string");
	}

	private static int RequiredInt(JsonObject owner, string name)
	{
		return (owner[name] ?? throw Invalid(name + " must be an integer")).GetValue<int>();
	}

	private static double RequiredDouble(JsonObject owner, string name)
	{
		return (owner[name] ?? throw Invalid(name + " must be numeric")).GetValue<double>();
	}

	private static bool RequiredBool(JsonObject owner, string name)
	{
		return (owner[name] ?? throw Invalid(name + " must be a boolean")).GetValue<bool>();
	}

	private static InvalidDataException Invalid(string message)
	{
		return new InvalidDataException("L1J_MAP_RULES: " + message);
	}
}
