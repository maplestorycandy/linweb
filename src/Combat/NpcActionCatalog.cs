using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class NpcActionCatalog
{
	public const string TableName = "NPC_ACTIONS";

	public const int QuestEndStep = 255;

	private static readonly ConditionalWeakTable<IGameData, IReadOnlyList<NpcActionDefinition>> Cache = new ConditionalWeakTable<IGameData, IReadOnlyList<NpcActionDefinition>>();

	public static void InvalidateCache()
	{
		Cache.Clear();
	}

	public static IReadOnlyList<NpcActionDefinition> All(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build);
	}

	public static char? ClassLetter(string classId)
	{
		switch (classId)
		{
		case "royal":
			return 'P';
		case "knight":
			return 'K';
		case "elf":
			return 'E';
		case "mage":
			return 'W';
		case "dark":
		case "darkelf":
			return 'D';
		case "dragon":
		case "dknight":
			return 'R';
		case "illusion":
		case "illusionist":
			return 'I';
		case "warrior":
			return 'A';
		default:
			return null;
		}
	}

	public static bool Accepts(NpcActionDefinition definition, int npcId, string actionName, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(definition, "definition");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (definition.NpcIds.Count > 0 && !definition.NpcIds.Contains(npcId))
		{
			return false;
		}
		if (actor.Level < definition.LevelMin || actor.Level > definition.LevelMax)
		{
			return false;
		}
		if (definition.QuestId != null)
		{
			int num = QuestStepOf(actor, definition.QuestId);
			if (!definition.QuestStep.HasValue)
			{
				if (num <= 0)
				{
					return false;
				}
			}
			else if (num != definition.QuestStep)
			{
				return false;
			}
		}
		if (definition.Classes.Length > 0)
		{
			char? c = ClassLetter(actor.ClassId);
			if (!c.HasValue || !definition.Classes.Contains(c.Value))
			{
				return false;
			}
		}
		foreach (NpcActionItem requiredHeldItem in definition.RequiredHeldItems)
		{
			if (requiredHeldItem.ItemKey == null || ItemStackInventory.CountByItemKey(actor.InventoryStacks, requiredHeldItem.ItemKey) < requiredHeldItem.Count)
			{
				return false;
			}
		}
		foreach (NpcActionItem forbiddenHeldItem in definition.ForbiddenHeldItems)
		{
			if (forbiddenHeldItem.ItemKey != null && ItemStackInventory.CountByItemKey(actor.InventoryStacks, forbiddenHeldItem.ItemKey) >= forbiddenHeldItem.Count)
			{
				return false;
			}
		}
		if (definition.Name.Length > 0 && !string.Equals(definition.Name, actionName, StringComparison.Ordinal))
		{
			return false;
		}
		return true;
	}

	public static NpcActionDefinition? Find(IGameData data, int npcId, string actionName, Combatant actor)
	{
		foreach (NpcActionDefinition item in All(data))
		{
			if (Accepts(item, npcId, actionName, actor))
			{
				return item;
			}
		}
		return null;
	}

	public static IReadOnlyList<NpcActionDefinition> AvailableFor(IGameData data, int npcId, Combatant actor)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		List<NpcActionDefinition> list = new List<NpcActionDefinition>();
		foreach (NpcActionDefinition item in All(data))
		{
			if (item.Name.Length != 0 && item.NpcIds.Count != 0 && item.NpcIds.Contains(npcId) && Accepts(item, npcId, item.Name, actor) && hashSet.Add(item.Name))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static int QuestStepOf(Combatant actor, string questId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return actor.Progress.QuestSteps.GetValueOrDefault(questId);
	}

	public static int KillCountOf(Combatant actor, string counterId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(counterId, "counterId");
		return actor.Progress.QuestKillCounts.GetValueOrDefault(counterId);
	}

	public static void RegisterMonsterKill(IGameData data, Combatant actor, string mobKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (string.IsNullOrWhiteSpace(mobKey))
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (NpcActionDefinition item in All(data))
		{
			NpcActionKillRequirement killRequirement = item.KillRequirement;
			if ((object)killRequirement != null && killRequirement.MobKeys.Contains<string>(mobKey, StringComparer.Ordinal) && IsKillObjectiveActive(item, actor) && hashSet.Add(killRequirement.CounterId))
			{
				int num = KillCountOf(actor, killRequirement.CounterId);
				if (num < killRequirement.RequiredCount)
				{
					actor.Progress.QuestKillCounts[killRequirement.CounterId] = num + 1;
				}
			}
		}
	}

	private static bool IsKillObjectiveActive(NpcActionDefinition definition, Combatant actor)
	{
		if (actor.Level < definition.LevelMin || actor.Level > definition.LevelMax)
		{
			return false;
		}
		if (definition.Classes.Length > 0)
		{
			char? c = ClassLetter(actor.ClassId);
			if (!c.HasValue || !definition.Classes.Contains(c.Value))
			{
				return false;
			}
		}
		if (definition.QuestId == null)
		{
			return true;
		}
		int num = QuestStepOf(actor, definition.QuestId);
		if (definition.QuestStep.HasValue)
		{
			return num == definition.QuestStep;
		}
		return num > 0;
	}

	private static IReadOnlyList<NpcActionDefinition> Build(IGameData data)
	{
		if (!(data.Table("NPC_ACTIONS") is JsonObject jsonObject) || !(jsonObject["actions"] is JsonArray jsonArray))
		{
			throw new InvalidDataException("NPC_ACTIONS table failed to load.");
		}
		List<NpcActionDefinition> list = new List<NpcActionDefinition>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			JsonObject jsonObject2 = item.AsObject();
			list.Add(new NpcActionDefinition
			{
				Seq = jsonObject2["seq"].GetValue<int>(),
				Source = jsonObject2["src"].GetValue<string>(),
				Kind = jsonObject2["kind"].GetValue<string>(),
				Name = jsonObject2["name"].GetValue<string>(),
				NpcIds = ((jsonObject2["npcIds"] is JsonArray source) ? source.Select((JsonNode id) => id.GetValue<int>()).ToArray() : Array.Empty<int>()),
				Classes = (jsonObject2["classes"]?.GetValue<string>() ?? ""),
				LevelMin = (jsonObject2["levelMin"]?.GetValue<int>() ?? 1),
				LevelMax = (jsonObject2["levelMax"]?.GetValue<int>() ?? 99),
				QuestId = jsonObject2["questId"]?.GetValue<string>(),
				QuestStep = ((jsonObject2.ContainsKey("questId") && jsonObject2["questStep"] is JsonValue jsonValue) ? new int?(jsonValue.GetValue<int>()) : ((int?)null)),
				AmountInputable = (jsonObject2["amountInputable"]?.GetValue<bool>() ?? false),
				Materials = ReadItems(jsonObject2["materials"]),
				Outputs = ReadItems(jsonObject2["outputs"]),
				KillRequirement = ReadKillRequirement(jsonObject2["killRequirement"]),
				Succeed = ReadEffects(jsonObject2["succeed"]),
				Fail = ReadEffects(jsonObject2["fail"]),
				Effects = ReadEffects(jsonObject2["effects"])
			});
		}
		if (list.Count == 0)
		{
			throw new InvalidDataException("NPC_ACTIONS is empty.");
		}
		list.AddRange(L1jHardcodedNpcActions.All);
		return list;
	}

	private static NpcActionKillRequirement? ReadKillRequirement(JsonNode? node)
	{
		if (node == null)
		{
			return null;
		}
		if (!(node is JsonObject jsonObject) || !(jsonObject["id"] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrWhiteSpace(value) || !(jsonObject["name"] is JsonValue jsonValue2) || !jsonValue2.TryGetValue<string>(out string value2) || string.IsNullOrWhiteSpace(value2) || !(jsonObject["count"] is JsonValue jsonValue3) || !jsonValue3.TryGetValue<int>(out var value3) || value3 <= 0 || !(jsonObject["mobKeys"] is JsonArray source))
		{
			throw new InvalidDataException("NPC action killRequirement is invalid.");
		}
		string[] array = source.Select((JsonNode jsonNode) => jsonNode?.GetValue<string>() ?? "").ToArray();
		if (array.Length == 0 || array.Any((string key) => !key.StartsWith("l1j_", StringComparison.Ordinal)) || array.Distinct<string>(StringComparer.Ordinal).Count() != array.Length)
		{
			throw new InvalidDataException("NPC action killRequirement '" + value + "' has invalid mob keys.");
		}
		return new NpcActionKillRequirement(value, value2, value3, array);
	}

	private static IReadOnlyList<NpcActionItem> ReadItems(JsonNode? node)
	{
		if (!(node is JsonArray source))
		{
			return Array.Empty<NpcActionItem>();
		}
		return source.Select((JsonNode row) => new NpcActionItem(row["id"].GetValue<int>(), row["count"].GetValue<int>(), (row["key"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value)) ? value : null, ReadBlessing(row["blessing"]?.GetValue<string>()))).ToArray();
	}

	private static ItemBlessing ReadBlessing(string? value)
	{
		if (!(value == "blessed"))
		{
			if (value == "cursed")
			{
				return ItemBlessing.Cursed;
			}
			return ItemBlessing.Normal;
		}
		return ItemBlessing.Blessed;
	}

	private static IReadOnlyList<NpcActionEffect> ReadEffects(JsonNode? node)
	{
		if (!(node is JsonArray jsonArray))
		{
			return Array.Empty<NpcActionEffect>();
		}
		List<NpcActionEffect> list = new List<NpcActionEffect>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			JsonObject jsonObject = item.AsObject();
			string value = jsonObject["t"].GetValue<string>();
			List<NpcActionEffect> list2 = list;
			list2.Add(value switch
			{
				"quest" => new NpcActionEffect
				{
					Kind = value,
					QuestId = jsonObject["id"].GetValue<string>(),
					QuestStep = jsonObject["step"].GetValue<int>(),
					IfQuestId = jsonObject["ifId"]?.GetValue<string>(),
					IfQuestStep = ((jsonObject.ContainsKey("ifId") && jsonObject["ifStep"] is JsonValue jsonValue) ? new int?(jsonValue.GetValue<int>()) : ((int?)null))
				}, 
				"killCount" => new NpcActionEffect
				{
					Kind = value,
					QuestId = jsonObject["id"].GetValue<string>(),
					QuestStep = jsonObject["count"].GetValue<int>()
				}, 
				"html" => new NpcActionEffect
				{
					Kind = value,
					HtmlId = jsonObject["id"].GetValue<string>()
				}, 
				"teleport" => new NpcActionEffect
				{
					Kind = value,
					X = jsonObject["x"].GetValue<int>(),
					Y = jsonObject["y"].GetValue<int>(),
					MapId = jsonObject["map"].GetValue<int>(),
					Heading = jsonObject["heading"].GetValue<int>(),
					Price = jsonObject["price"]?.GetValue<int>() ?? 0
				}, 
				"buff" => new NpcActionEffect
				{
					Kind = value,
					BuffKey = jsonObject["buff"]?.GetValue<string>() ?? "all",
					Duration = jsonObject["duration"]?.GetValue<double>() ?? 1800.0,
					Heal = jsonObject["heal"]?.GetValue<bool>() ?? true,
					Cure = jsonObject["cure"]?.GetValue<bool>() ?? true,
					Price = jsonObject["price"]?.GetValue<int>() ?? 0
				}, 
				_ => throw new InvalidDataException("Unknown effect kind '" + value + "'."), 
			});
		}
		return list;
	}
}
