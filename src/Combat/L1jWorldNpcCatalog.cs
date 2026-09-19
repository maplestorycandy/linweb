using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jWorldNpcCatalog
{
	public static readonly IReadOnlyList<string> PreviousTakeoverMilestones = new string[23]
	{
		L1jHiddenValleyCatalog.MapKey,
		"behemoth",
		"hyperia",
		"pirate_wild",
		"talking_island",
		"oblivion_island",
		"rastabad_4f_council_kassandra_dantes",
		"silent_outer",
		"ivory_tower_1f",
		"ivory_tower_2f",
		"ivory_tower_3f",
		"mainland_south",
		"zone_03",
		"l1j_map_15",
		"l1j_map_29",
		"oum_dungeon",
		"zone_37",
		"zone_38",
		"zone_39",
		"zone_40",
		"zone_41",
		"flame_audience_hall",
		"flame_shadow_lab"
	};

	public static readonly IReadOnlyList<string> MainTerrainNpcAuditMapKeys = new string[22]
	{
		"oum_dungeon", "pride_f6", "pride_f7", "pride_f8", "pride_f9", "pride_f21", "pride_f26", "pride_f27", "pride_f28", "pride_f29",
		"pride_f46", "pride_f56", "pride_f66", "pride_f76", "pride_f86", "pride_f96", "zone_37", "zone_38", "zone_39", "zone_40",
		"zone_41", "l1j_map_2005"
	};

	public const string WarehouseImpl = "L1Dwarf";

	public const int ElfWarehouseNpcId = 60028;

	public const string HousekeeperImpl = "L1Housekeeper";

	public static readonly IReadOnlyList<int> ClanExecutorNpcIds = new int[7] { 70538, 70560, 70644, 70667, 70725, 70790, 70884 };

	public static readonly IReadOnlyDictionary<int, string> PortServiceNpcKeys = new Dictionary<int, string>
	{
		[71056] = "npc_shimizhe",
		[70011] = "npc_port_master_talking",
		[70760] = "npc_elion",
		[70022] = "npc_port_master_gludin",
		[70086] = "main_forgotten_island_ticket",
		[50064] = "npc_dufa",
		[70094] = "npc_duran",
		[80064] = "npc_desire_cave_consul",
		[71252] = "npc_zeus_golem",
		[70553] = "npc_ismael",
		[80052] = "npc_flame_lab_consul"
	};

	public static IReadOnlyList<string> MainNpcMapKeys(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return (from spawn in L1jShopCatalog.Spawns(data)
			select spawn.MapKey).Distinct<string>(StringComparer.Ordinal).OrderBy<string, string>((string mapKey) => mapKey, StringComparer.Ordinal).ToArray();
	}

	public static bool Owns(IGameData data, string? mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (mapKey != null)
		{
			return L1jShopCatalog.Spawns(data).Any((L1jNpcSpawn spawn) => string.Equals(spawn.MapKey, mapKey, StringComparison.Ordinal));
		}
		return false;
	}

	public static IReadOnlyList<L1jNpcSpawn> SpawnsOn(IGameData data, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		return (from spawn in L1jShopCatalog.Spawns(data)
			where string.Equals(spawn.MapKey, mapKey, StringComparison.Ordinal)
			select spawn).ToArray();
	}

	public static bool IsHousekeeper(L1jNpcSpawn spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		return string.Equals(spawn.Impl, "L1Housekeeper", StringComparison.Ordinal);
	}

	public static bool IsClanExecutor(L1jNpcSpawn spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		return ClanExecutorNpcIds.Contains(spawn.NpcId);
	}

	public static bool IsArenaManager(IGameData data, L1jNpcSpawn spawn)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		L1jUbArena arena;
		return L1jUltimateBattleCatalog.Load(data).TryResolveManager(spawn.NpcId, out arena);
	}

	public static bool IsWarehouseKeeper(L1jNpcSpawn spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		return string.Equals(spawn.Impl, "L1Dwarf", StringComparison.Ordinal);
	}

	public static bool CanUseWarehouse(L1jNpcSpawn spawn, Combatant player)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		ArgumentNullException.ThrowIfNull(player, "player");
		if (IsWarehouseKeeper(spawn))
		{
			if (spawn.NpcId == 60028)
			{
				return ElfElementRules.IsElf(player);
			}
			return true;
		}
		return false;
	}

	public static bool IsMainPetKeeper(int npcId)
	{
		if (npcId == 70723 || npcId == 80095)
		{
			return true;
		}
		return false;
	}

	public static bool TryPortServiceKey(int npcId, out string portNpcKey)
	{
		return PortServiceNpcKeys.TryGetValue(npcId, out portNpcKey);
	}

	public static bool HasShopOffers(IGameData data, int npcId)
	{
		return L1jShopCatalog.SellList(data, npcId).Count > 0;
	}

	public static IReadOnlyList<NpcActionDefinition> TeleportActionsFor(IGameData data, int npcId, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return (from action in NpcActionCatalog.AvailableFor(data, npcId, actor)
			where action.Effects.Any((NpcActionEffect effect) => string.Equals(effect.Kind, "teleport", StringComparison.Ordinal))
			select action).ToArray();
	}

	public static NpcActionEffect? TeleportOf(NpcActionDefinition action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		NpcActionEffect npcActionEffect = action.Effects.FirstOrDefault((NpcActionEffect candidate) => string.Equals(candidate.Kind, "teleport", StringComparison.Ordinal));
		if ((object)npcActionEffect != null)
		{
			return L1jDungeonExitReroutes.Apply(action, npcActionEffect);
		}
		return null;
	}

	public static string? TutorDestinationLabel(NpcActionDefinition action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		if (!action.NpcIds.Contains(80153))
		{
			return null;
		}
		return action.Name switch
		{
			"TUTOR_SILVER_KNIGHT" => "銀騎士村", 
			"TUTOR_ELF_FOREST" => "妖精森林", 
			"TUTOR_TALKING_ISLAND" => "說話之島", 
			"TUTOR_SILENT_CAVE" => "沉默洞穴", 
			_ => null, 
		};
	}

	public static IReadOnlyList<NpcActionDefinition> ActionsFor(IGameData data, int npcId, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return NpcActionCatalog.AvailableFor(data, npcId, actor);
	}

	public static bool IsInteractive(IGameData data, int npcId, Combatant actor)
	{
		if (!HasShopOffers(data, npcId))
		{
			return ActionsFor(data, npcId, actor).Any(HasContent);
		}
		return true;
	}

	public static bool IsInteractive(IGameData data, L1jNpcSpawn spawn, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		if (!IsWarehouseKeeper(spawn) && !IsHousekeeper(spawn) && !IsClanExecutor(spawn) && !L1jNpcSkillLearningRules.IsMainMagicInstructor(spawn.NpcId) && !IsArenaManager(data, spawn) && !PortServiceNpcKeys.ContainsKey(spawn.NpcId))
		{
			return IsInteractive(data, spawn.NpcId, actor);
		}
		return true;
	}

	private static bool HasContent(NpcActionDefinition action)
	{
		if (action.Effects.Count <= 0 && action.Outputs.Count <= 0)
		{
			return action.Succeed.Count > 0;
		}
		return true;
	}
}
