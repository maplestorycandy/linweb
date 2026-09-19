using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jMobGroupCatalog
{
	public const string TableName = "L1J_MOB_GROUPS";

	public IReadOnlyDictionary<int, L1jMobGroupDefinition> Groups { get; }

	private L1jMobGroupCatalog(IReadOnlyDictionary<int, L1jMobGroupDefinition> groups)
	{
		Groups = groups;
	}

	public L1jMobGroupDefinition Require(int groupId)
	{
		if (!Groups.TryGetValue(groupId, out L1jMobGroupDefinition value))
		{
			throw new InvalidDataException($"{"L1J_MOB_GROUPS"} does not contain mob group {groupId}.");
		}
		return value;
	}

	public static L1jMobGroupCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonArray obj = (((data.Table("L1J_MOB_GROUPS") as JsonObject) ?? throw new InvalidDataException("L1J_MOB_GROUPS must be a JSON object."))["groups"] as JsonArray) ?? throw new InvalidDataException("L1J_MOB_GROUPS.groups must be an array.");
		Dictionary<int, L1jMobGroupDefinition> dictionary = new Dictionary<int, L1jMobGroupDefinition>();
		foreach (JsonNode item in obj)
		{
			if (!(item is JsonObject jsonObject))
			{
				throw new InvalidDataException("Every L1J_MOB_GROUPS row must be an object.");
			}
			int num = RequiredInt(jsonObject, "groupId");
			string text = RequiredString(jsonObject, "leaderMobKey");
			if (data.Mob(text) == null)
			{
				throw new InvalidDataException($"Mob group {num} references missing leader '{text}'.");
			}
			JsonArray obj2 = (jsonObject["minions"] as JsonArray) ?? throw new InvalidDataException($"Mob group {num}.minions must be an array.");
			List<L1jMobGroupMinionDefinition> list = new List<L1jMobGroupMinionDefinition>();
			HashSet<int> hashSet = new HashSet<int>();
			foreach (JsonNode item2 in obj2)
			{
				if (!(item2 is JsonObject row))
				{
					throw new InvalidDataException($"Mob group {num} has a non-object minion row.");
				}
				L1jMobGroupMinionDefinition l1jMobGroupMinionDefinition = new L1jMobGroupMinionDefinition(RequiredInt(row, "slot"), RequiredInt(row, "npcId"), RequiredString(row, "mobKey"), RequiredInt(row, "count"));
				int slot = l1jMobGroupMinionDefinition.Slot;
				bool flag = ((slot < 1 || slot > 7) ? true : false);
				if (flag || !hashSet.Add(l1jMobGroupMinionDefinition.Slot) || l1jMobGroupMinionDefinition.NpcId <= 0 || l1jMobGroupMinionDefinition.Count <= 0 || data.Mob(l1jMobGroupMinionDefinition.MobKey) == null)
				{
					throw new InvalidDataException($"Mob group {num} has an invalid minion slot {l1jMobGroupMinionDefinition.Slot}.");
				}
				list.Add(l1jMobGroupMinionDefinition);
			}
			L1jMobGroupDefinition l1jMobGroupDefinition = new L1jMobGroupDefinition(num, RequiredString(jsonObject, "note"), RequiredBool(jsonObject, "removeGroupIfLeaderDies"), RequiredInt(jsonObject, "leaderNpcId"), text, new ReadOnlyCollection<L1jMobGroupMinionDefinition>(list));
			if (l1jMobGroupDefinition.LeaderNpcId <= 0 || !dictionary.TryAdd(num, l1jMobGroupDefinition))
			{
				throw new InvalidDataException($"Duplicate or invalid mob group {num}.");
			}
		}
		if (dictionary.Count != 76)
		{
			throw new InvalidDataException($"{"L1J_MOB_GROUPS"} must contain main's 76 groups; got {dictionary.Count}.");
		}
		return new L1jMobGroupCatalog(new ReadOnlyDictionary<int, L1jMobGroupDefinition>(dictionary));
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_MOB_GROUPS." + name + " must be an integer.")).GetValue<int>();
	}

	private static bool RequiredBool(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_MOB_GROUPS." + name + " must be a boolean.")).GetValue<bool>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_MOB_GROUPS." + name + " must be a string.");
	}
}
