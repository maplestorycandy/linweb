using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jTrapCatalog
{
	public const string TableName = "L1J_TRAPS";

	public IReadOnlyDictionary<int, L1jTrapDefinition> Definitions { get; }

	public IReadOnlyDictionary<string, IReadOnlyList<L1jTrapPlacement>> Maps { get; }

	private L1jTrapCatalog(IReadOnlyDictionary<int, L1jTrapDefinition> definitions, IReadOnlyDictionary<string, IReadOnlyList<L1jTrapPlacement>> maps)
	{
		Definitions = definitions;
		Maps = maps;
	}

	public L1jTrapDefinition RequireDefinition(int trapId)
	{
		if (!Definitions.TryGetValue(trapId, out L1jTrapDefinition value))
		{
			throw new InvalidDataException($"{"L1J_TRAPS"} does not contain trap {trapId}.");
		}
		return value;
	}

	public IReadOnlyList<L1jTrapPlacement> PlacementsFor(string mapKey)
	{
		if (!Maps.TryGetValue(mapKey, out IReadOnlyList<L1jTrapPlacement> value))
		{
			return Array.Empty<L1jTrapPlacement>();
		}
		return value;
	}

	public static L1jTrapCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject jsonObject = (data.Table("L1J_TRAPS") as JsonObject) ?? throw new InvalidDataException("L1J_TRAPS must be an object.");
		JsonArray obj = (jsonObject["definitions"] as JsonArray) ?? throw new InvalidDataException("L1J_TRAPS.definitions must be an array.");
		Dictionary<int, L1jTrapDefinition> dictionary = new Dictionary<int, L1jTrapDefinition>();
		foreach (JsonNode item in obj)
		{
			JsonObject jsonObject2 = (item as JsonObject) ?? throw new InvalidDataException("Every trap definition must be an object.");
			int num = RequiredInt(jsonObject2, "trapId");
			string text = RequiredString(jsonObject2, "type");
			L1jTrapKind kind = text switch
			{
				"L1DamageTrap" => L1jTrapKind.Damage, 
				"L1HealingTrap" => L1jTrapKind.Healing, 
				"L1PoisonTrap" => L1jTrapKind.Poison, 
				"L1MonsterTrap" => L1jTrapKind.Monster, 
				"L1TeleportTrap" => L1jTrapKind.Teleport, 
				"L1SkillTrap" => L1jTrapKind.Skill, 
				_ => throw new InvalidDataException($"Trap {num} has unsupported type '{text}'."), 
			};
			JsonObject jsonObject3 = jsonObject2["teleport"] as JsonObject;
			JsonArray jsonArray = jsonObject3?["cell"] as JsonArray;
			L1jTrapDefinition l1jTrapDefinition = new L1jTrapDefinition(num, RequiredString(jsonObject2, "note"), kind, RequiredInt(jsonObject2, "gfxId"), RequiredBool(jsonObject2, "detectionable"), RequiredInt(jsonObject2, "base"), RequiredInt(jsonObject2, "dice"), RequiredInt(jsonObject2, "diceCount"), RequiredString(jsonObject2, "poisonType"), RequiredInt(jsonObject2, "poisonDelayMs"), RequiredInt(jsonObject2, "poisonTimeMs"), RequiredInt(jsonObject2, "poisonDamage"), OptionalString(jsonObject2, "monsterMobKey"), RequiredInt(jsonObject2, "monsterCount"), OptionalString(jsonObject3, "mapKey"), (jsonArray?[0]?.GetValue<int>()).GetValueOrDefault(), (jsonArray?[1]?.GetValue<int>()).GetValueOrDefault(), OptionalString(jsonObject2, "skillKey"), RequiredInt(jsonObject2, "skillTimeSeconds"));
			if (!dictionary.TryAdd(num, l1jTrapDefinition))
			{
				throw new InvalidDataException($"Duplicate trap id {num}.");
			}
			if (l1jTrapDefinition.Kind == L1jTrapKind.Monster && (l1jTrapDefinition.MonsterMobKey == null || data.Mob(l1jTrapDefinition.MonsterMobKey) == null || l1jTrapDefinition.MonsterCount <= 0))
			{
				throw new InvalidDataException($"Monster trap {num} has an invalid monster reference.");
			}
			if (l1jTrapDefinition.Kind == L1jTrapKind.Skill && (l1jTrapDefinition.SkillKey == null || data.Skill(l1jTrapDefinition.SkillKey) == null))
			{
				throw new InvalidDataException($"Skill trap {num} has an invalid skill reference.");
			}
		}
		if (dictionary.Count != 52)
		{
			throw new InvalidDataException($"{"L1J_TRAPS"} must contain main's 52 trap definitions; got {dictionary.Count}.");
		}
		JsonObject obj2 = (jsonObject["maps"] as JsonObject) ?? throw new InvalidDataException("L1J_TRAPS.maps must be an object.");
		Dictionary<string, IReadOnlyList<L1jTrapPlacement>> dictionary2 = new Dictionary<string, IReadOnlyList<L1jTrapPlacement>>(StringComparer.Ordinal);
		HashSet<int> hashSet = new HashSet<int>();
		foreach (KeyValuePair<string, JsonNode> item2 in obj2)
		{
			item2.Deconstruct(out var key, out var value);
			string text2 = key;
			JsonArray obj3 = (value as JsonArray) ?? throw new InvalidDataException("L1J_TRAPS.maps." + text2 + " must be an array.");
			List<L1jTrapPlacement> list = new List<L1jTrapPlacement>();
			foreach (JsonNode item3 in obj3)
			{
				JsonObject row = (item3 as JsonObject) ?? throw new InvalidDataException("Every trap placement must be an object.");
				JsonArray jsonArray2 = RequiredPair(row, "cell");
				JsonArray jsonArray3 = RequiredPair(row, "random");
				L1jTrapPlacement l1jTrapPlacement = new L1jTrapPlacement(RequiredInt(row, "spawnId"), RequiredString(row, "note"), RequiredInt(row, "trapId"), jsonArray2[0].GetValue<int>(), jsonArray2[1].GetValue<int>(), jsonArray3[0].GetValue<int>(), jsonArray3[1].GetValue<int>(), RequiredInt(row, "count"), RequiredInt(row, "spanMs"));
				if (!hashSet.Add(l1jTrapPlacement.SpawnId) || !dictionary.ContainsKey(l1jTrapPlacement.TrapId) || l1jTrapPlacement.Count <= 0 || l1jTrapPlacement.RandomX < 0 || l1jTrapPlacement.RandomY < 0 || l1jTrapPlacement.SpanMs < 0)
				{
					throw new InvalidDataException($"Invalid trap placement {l1jTrapPlacement.SpawnId}.");
				}
				list.Add(l1jTrapPlacement);
			}
			dictionary2.Add(text2, new ReadOnlyCollection<L1jTrapPlacement>(list));
		}
		JsonObject row2 = (jsonObject["counts"] as JsonObject) ?? throw new InvalidDataException("L1J_TRAPS.counts must be an object.");
		if (RequiredInt(row2, "placementRows") != 325 || RequiredInt(row2, "resolvedPlacementRows") != hashSet.Count || RequiredInt(row2, "runtimeInstances") != dictionary2.Values.SelectMany((IReadOnlyList<L1jTrapPlacement> rows) => rows).Sum((L1jTrapPlacement l1jTrapPlacement2) => l1jTrapPlacement2.Count))
		{
			throw new InvalidDataException("L1J_TRAPS count ledger does not match its rows.");
		}
		return new L1jTrapCatalog(new ReadOnlyDictionary<int, L1jTrapDefinition>(dictionary), new ReadOnlyDictionary<string, IReadOnlyList<L1jTrapPlacement>>(dictionary2));
	}

	private static JsonArray RequiredPair(JsonObject row, string name)
	{
		if (!(row[name] is JsonArray { Count: 2 } jsonArray))
		{
			throw new InvalidDataException("L1J_TRAPS." + name + " must be a pair.");
		}
		return jsonArray;
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_TRAPS." + name + " must be an integer.")).GetValue<int>();
	}

	private static bool RequiredBool(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_TRAPS." + name + " must be a boolean.")).GetValue<bool>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_TRAPS." + name + " must be a string.");
	}

	private static string? OptionalString(JsonObject? row, string name)
	{
		if (!(row?[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}
}
