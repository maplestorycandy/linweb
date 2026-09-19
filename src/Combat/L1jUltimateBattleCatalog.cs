using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jUltimateBattleCatalog
{
	private readonly IReadOnlyList<L1jUbArena> _arenas;

	private readonly IReadOnlyDictionary<int, L1jUbArena> _byManager;

	private readonly IReadOnlyDictionary<int, IReadOnlyList<L1jUbSupply>> _supplies;

	private readonly IReadOnlyList<int> _roundWaitSeconds;

	public IReadOnlyList<L1jUbArena> Arenas => _arenas;

	private L1jUltimateBattleCatalog(IReadOnlyList<L1jUbArena> arenas, IReadOnlyDictionary<int, L1jUbArena> byManager, IReadOnlyDictionary<int, IReadOnlyList<L1jUbSupply>> supplies, IReadOnlyList<int> roundWaitSeconds)
	{
		_arenas = arenas;
		_byManager = byManager;
		_supplies = supplies;
		_roundWaitSeconds = roundWaitSeconds;
	}

	public bool TryResolveManager(int npcId, out L1jUbArena arena)
	{
		return _byManager.TryGetValue(npcId, out arena);
	}

	public L1jUbArena? Arena(int ubId)
	{
		return _arenas.FirstOrDefault((L1jUbArena arena) => arena.UbId == ubId);
	}

	public IReadOnlyList<L1jUbSupply> Supplies(int round)
	{
		if (!_supplies.TryGetValue(round, out IReadOnlyList<L1jUbSupply> value))
		{
			return Array.Empty<L1jUbSupply>();
		}
		return value;
	}

	public int RoundWaitSeconds(int round)
	{
		if (round < 1 || round > _roundWaitSeconds.Count)
		{
			return 0;
		}
		return _roundWaitSeconds[round - 1];
	}

	public static L1jUltimateBattleCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject jsonObject = (data.Table("L1J_ULTIMATE_BATTLE") as JsonObject) ?? throw new InvalidDataException("L1J_ULTIMATE_BATTLE must be an object.");
		List<L1jUbArena> list = new List<L1jUbArena>();
		Dictionary<int, L1jUbArena> dictionary = new Dictionary<int, L1jUbArena>();
		string key;
		JsonNode value;
		bool value2 = default(bool);
		foreach (JsonNode item in (jsonObject["arenas"] as JsonArray) ?? throw new InvalidDataException("L1J_ULTIMATE_BATTLE.arenas must be an array."))
		{
			if (!(item is JsonObject jsonObject2))
			{
				throw new InvalidDataException("Every arena row must be an object.");
			}
			Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<L1jUbWaveGroup>>> dictionary2 = new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<L1jUbWaveGroup>>>();
			foreach (KeyValuePair<string, JsonNode> item2 in (jsonObject2["patterns"] as JsonObject) ?? throw new InvalidDataException("Arena patterns must be an object."))
			{
				item2.Deconstruct(out key, out value);
				string text = key;
				JsonNode jsonNode = value;
				Dictionary<int, IReadOnlyList<L1jUbWaveGroup>> dictionary3 = new Dictionary<int, IReadOnlyList<L1jUbWaveGroup>>();
				foreach (KeyValuePair<string, JsonNode> item3 in (jsonNode as JsonObject) ?? throw new InvalidDataException("Arena pattern must be an object."))
				{
					item3.Deconstruct(out key, out value);
					string s = key;
					JsonNode jsonNode2 = value;
					List<L1jUbWaveGroup> list2 = new List<L1jUbWaveGroup>();
					foreach (JsonNode item4 in (jsonNode2 as JsonArray) ?? throw new InvalidDataException("Arena round must be an array."))
					{
						if (!(item4 is JsonObject row))
						{
							throw new InvalidDataException("Every wave group must be an object.");
						}
						string text2 = RequiredString(row, "mobKey");
						if (data.Mob(text2) == null)
						{
							throw new InvalidDataException("Ultimate battle wave references missing mob '" + text2 + "'.");
						}
						list2.Add(new L1jUbWaveGroup(text2, RequiredInt(row, "npcId"), RequiredInt(row, "count"), RequiredInt(row, "spawnDelaySeconds"), RequiredInt(row, "sealCount"), RequiredString(row, "note")));
					}
					dictionary3[int.Parse(s)] = new ReadOnlyCollection<L1jUbWaveGroup>(list2);
				}
				if (dictionary3.Count != 4)
				{
					throw new InvalidDataException("Ultimate battle pattern " + text + " must expose four rounds.");
				}
				dictionary2[int.Parse(text)] = new ReadOnlyDictionary<int, IReadOnlyList<L1jUbWaveGroup>>(dictionary3);
			}
			JsonObject obj = (jsonObject2["classes"] as JsonObject) ?? throw new InvalidDataException("Arena classes must be an object.");
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, JsonNode> item5 in obj)
			{
				item5.Deconstruct(out key, out value);
				string mainClass = key;
				if (value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value2) && value2)
				{
					hashSet.Add(MainClassToLocal(mainClass));
				}
			}
			L1jUbArena l1jUbArena = new L1jUbArena(RequiredInt(jsonObject2, "ubId"), RequiredString(jsonObject2, "name"), RequiredInt(jsonObject2, "mapId"), RequiredString(jsonObject2, "mapKey"), RequiredInt(jsonObject2, "minLevel"), RequiredInt(jsonObject2, "maxLevel"), RequiredInt(jsonObject2, "maxPlayer"), hashSet, RequiredBool(jsonObject2, "male"), RequiredBool(jsonObject2, "female"), RequiredBool(jsonObject2, "usePotion"), IntList(jsonObject2, "managerNpcIds"), IntList(jsonObject2, "openTimes"), new ReadOnlyDictionary<int, IReadOnlyDictionary<int, IReadOnlyList<L1jUbWaveGroup>>>(dictionary2));
			list.Add(l1jUbArena);
			foreach (int managerNpcId in l1jUbArena.ManagerNpcIds)
			{
				if (!dictionary.TryAdd(managerNpcId, l1jUbArena))
				{
					throw new InvalidDataException($"Arena manager {managerNpcId} serves two arenas.");
				}
			}
		}
		if (list.Count != 5)
		{
			throw new InvalidDataException($"main ships five arenas; got {list.Count}.");
		}
		if (dictionary.Count != 10)
		{
			throw new InvalidDataException($"main ships ten arena managers; got {dictionary.Count}.");
		}
		Dictionary<int, IReadOnlyList<L1jUbSupply>> dictionary4 = new Dictionary<int, IReadOnlyList<L1jUbSupply>>();
		foreach (KeyValuePair<string, JsonNode> item6 in (jsonObject["supplies"] as JsonObject) ?? throw new InvalidDataException("L1J_ULTIMATE_BATTLE.supplies must be an object."))
		{
			item6.Deconstruct(out key, out value);
			string s2 = key;
			JsonNode jsonNode3 = value;
			List<L1jUbSupply> list3 = new List<L1jUbSupply>();
			foreach (JsonNode item7 in (jsonNode3 as JsonArray) ?? throw new InvalidDataException("Supplies must be an array."))
			{
				if (!(item7 is JsonObject row2))
				{
					throw new InvalidDataException("Every supply must be an object.");
				}
				int num = RequiredInt(row2, "itemId");
				long? grantTotalOverride = ((num == 40308) ? new long?(L1jUltimateBattleCatalogConstants.BalancedAdenaGrant(int.Parse(s2))) : ((long?)null));
				list3.Add(new L1jUbSupply(num, ResolveSupplyItemKey(data, num), RequiredInt(row2, "stackCount"), RequiredInt(row2, "piles"), grantTotalOverride));
			}
			dictionary4[int.Parse(s2)] = new ReadOnlyCollection<L1jUbSupply>(list3);
		}
		IReadOnlyList<int> readOnlyList = IntList(jsonObject, "roundWaitSeconds");
		if (readOnlyList.Count != 4)
		{
			throw new InvalidDataException("roundWaitSeconds must cover four rounds.");
		}
		return new L1jUltimateBattleCatalog(new ReadOnlyCollection<L1jUbArena>(list), new ReadOnlyDictionary<int, L1jUbArena>(dictionary), new ReadOnlyDictionary<int, IReadOnlyList<L1jUbSupply>>(dictionary4), readOnlyList);
	}

	private static string MainClassToLocal(string mainClass)
	{
		return mainClass switch
		{
			"royal" => "royal", 
			"knight" => "knight", 
			"mage" => "mage", 
			"elf" => "elf", 
			"darkElf" => "dark", 
			"dragonKnight" => "dragon", 
			"illusionist" => "illusion", 
			_ => mainClass, 
		};
	}

	private static string ResolveSupplyItemKey(IGameData data, int itemId)
	{
		if (itemId == 40308)
		{
			return "";
		}
		foreach (var (result, jsonNode2) in data.Items)
		{
			if (jsonNode2 is JsonObject jsonObject && jsonObject["l1jItemId"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value) && value == itemId)
			{
				return result;
			}
		}
		throw new InvalidDataException($"Ultimate battle supply item {itemId} is missing.");
	}

	private static IReadOnlyList<int> IntList(JsonObject row, string name)
	{
		List<int> list = new List<int>();
		foreach (JsonNode item in (row[name] as JsonArray) ?? throw new InvalidDataException(name + " must be an array."))
		{
			if (item == null)
			{
				throw new InvalidDataException(name + " must contain integers.");
			}
			list.Add(item.GetValue<int>());
		}
		return new ReadOnlyCollection<int>(list);
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_ULTIMATE_BATTLE." + name + " must be an integer.")).GetValue<int>();
	}

	private static bool RequiredBool(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_ULTIMATE_BATTLE." + name + " must be a boolean.")).GetValue<bool>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_ULTIMATE_BATTLE." + name + " must be a string.");
	}
}
