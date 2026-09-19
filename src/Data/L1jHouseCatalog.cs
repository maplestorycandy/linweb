using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public sealed class L1jHouseCatalog
{
	public const string TableName = "L1J_HOUSES";

	public const int ExpectedSourceCount = 129;

	public const int ExpectedOperationalCount = 62;

	public const int ExpectedBasementCount = 56;

	public const string NpcKeyPrefix = "l1j_npc_";

	private readonly IReadOnlyDictionary<int, L1jHouseDefinition> _byKeeper;

	private readonly IReadOnlyDictionary<string, L1jHouseDefinition> _byBasementMap;

	public IReadOnlyDictionary<int, L1jHouseDefinition> ById { get; }

	public IReadOnlyList<L1jHouseDefinition> Operational { get; }

	private L1jHouseCatalog(IReadOnlyDictionary<int, L1jHouseDefinition> byId, IReadOnlyDictionary<int, L1jHouseDefinition> byKeeper, IReadOnlyDictionary<string, L1jHouseDefinition> byBasementMap)
	{
		ById = byId;
		_byKeeper = byKeeper;
		_byBasementMap = byBasementMap;
		Operational = new ReadOnlyCollection<L1jHouseDefinition>(byId.Values.Where((L1jHouseDefinition house) => house.Operational).ToArray());
	}

	public bool TryByKeeper(int npcId, out L1jHouseDefinition? house)
	{
		return _byKeeper.TryGetValue(npcId, out house);
	}

	public bool TryByBasementMap(string mapKey, out L1jHouseDefinition? house)
	{
		return _byBasementMap.TryGetValue(mapKey, out house);
	}

	public bool TryByNpcKey(string npcKey, out L1jHouseDefinition? house)
	{
		house = null;
		if (npcKey.StartsWith("l1j_npc_", StringComparison.Ordinal) && int.TryParse(npcKey.AsSpan("l1j_npc_".Length), out var result))
		{
			return TryByKeeper(result, out house);
		}
		return false;
	}

	public static bool TryResolveBasementArrival(MapTopology topology, L1jHouseBasement basement, out int cellX, out int cellY)
	{
		ArgumentNullException.ThrowIfNull(topology, "topology");
		ArgumentNullException.ThrowIfNull(basement, "basement");
		cellX = basement.GameX - topology.GameOriginX;
		cellY = basement.GameY - topology.GameOriginY;
		if (!topology.ContainsGameCell(basement.GameX, basement.GameY))
		{
			return false;
		}
		if (topology.IsWalkableCell(cellX, cellY))
		{
			return true;
		}
		int num = cellX;
		int num2 = cellY;
		int num3 = int.MaxValue;
		int num4 = 0;
		int num5 = 0;
		for (int i = num2 - 16; i <= num2 + 16; i++)
		{
			for (int j = num - 16; j <= num + 16; j++)
			{
				if (topology.IsWalkableCell(j, i))
				{
					int num6 = j - num;
					int num7 = i - num2;
					int num8 = num6 * num6 + num7 * num7;
					if (num8 < num3)
					{
						num3 = num8;
						num4 = j;
						num5 = i;
					}
				}
			}
		}
		if (num3 == int.MaxValue)
		{
			return false;
		}
		cellX = num4;
		cellY = num5;
		return true;
	}

	public static L1jHouseCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonArray jsonArray = (((data.Table("L1J_HOUSES") as JsonObject) ?? throw Invalid("table must be an object"))["houses"] as JsonArray) ?? throw Invalid("houses must be an array");
		if (jsonArray.Count != 129)
		{
			throw Invalid($"source count must be {129}, got {jsonArray.Count}");
		}
		Dictionary<int, L1jHouseDefinition> dictionary = new Dictionary<int, L1jHouseDefinition>();
		Dictionary<int, L1jHouseDefinition> dictionary2 = new Dictionary<int, L1jHouseDefinition>();
		Dictionary<string, L1jHouseDefinition> dictionary3 = new Dictionary<string, L1jHouseDefinition>(StringComparer.Ordinal);
		foreach (JsonNode item in jsonArray)
		{
			JsonObject jsonObject = (item as JsonObject) ?? throw Invalid("house row must be an object");
			int num = RequiredInt(jsonObject, "houseId");
			bool flag = RequiredBool(jsonObject, "operational");
			L1jHouseKeeper l1jHouseKeeper = null;
			if (jsonObject["keeper"] is JsonObject owner)
			{
				JsonObject owner2 = RequiredObject(owner, "spawn");
				l1jHouseKeeper = new L1jHouseKeeper(RequiredInt(owner, "npcId"), RequiredString(owner, "name"), RequiredString(owner, "title"), RequiredString(owner, "impl"), RequiredInt(owner, "gfx"), RequiredInt(owner2, "mapId"), RequiredInt(owner2, "gameX"), RequiredInt(owner2, "gameY"), RequiredInt(owner2, "heading"));
			}
			L1jHouseBasement l1jHouseBasement = null;
			if (jsonObject["basement"] is JsonObject owner3)
			{
				l1jHouseBasement = new L1jHouseBasement(RequiredInt(owner3, "mapId"), RequiredString(owner3, "mapKey"), RequiredInt(owner3, "gameX"), RequiredInt(owner3, "gameY"));
			}
			L1jHouseDefinition value = new L1jHouseDefinition(num, RequiredString(jsonObject, "name"), RequiredInt(jsonObject, "area"), RequiredString(jsonObject, "location"), RequiredInt(jsonObject, "keeperId"), RequiredString(jsonObject, "city"), RequiredInt(jsonObject, "number"), flag, l1jHouseKeeper, l1jHouseBasement);
			if (!dictionary.TryAdd(num, value))
			{
				throw Invalid($"duplicate house id {num}");
			}
			if (!flag)
			{
				if ((object)l1jHouseKeeper != null || (object)l1jHouseBasement != null)
				{
					throw Invalid($"non-operational house {num} has runtime data");
				}
				continue;
			}
			if ((object)l1jHouseKeeper == null || l1jHouseKeeper.Impl != "L1Housekeeper" || !dictionary2.TryAdd(l1jHouseKeeper.NpcId, value))
			{
				throw Invalid($"operational house {num} has invalid keeper data");
			}
			if ((object)l1jHouseBasement != null && !dictionary3.TryAdd(l1jHouseBasement.MapKey, value))
			{
				throw Invalid("duplicate basement map key " + l1jHouseBasement.MapKey);
			}
		}
		if (dictionary2.Count != 62)
		{
			throw Invalid($"operational count must be {62}, got {dictionary2.Count}");
		}
		if (dictionary3.Count != 56)
		{
			throw Invalid($"basement count must be {56}, got {dictionary3.Count}");
		}
		return new L1jHouseCatalog(new ReadOnlyDictionary<int, L1jHouseDefinition>(dictionary), new ReadOnlyDictionary<int, L1jHouseDefinition>(dictionary2), new ReadOnlyDictionary<string, L1jHouseDefinition>(dictionary3));
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

	private static bool RequiredBool(JsonObject owner, string name)
	{
		return (owner[name] ?? throw Invalid(name + " must be a boolean")).GetValue<bool>();
	}

	private static InvalidDataException Invalid(string message)
	{
		return new InvalidDataException("L1J_HOUSES: " + message);
	}
}
