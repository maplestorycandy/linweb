using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class PirateIslandCatalog
{
	public const string FrontMapKey = "pirate_wild";

	public const string BackMapKey = "pirate_back";

	public const string Dungeon1FMapKey = "pirate_dungeon";

	public const string Dungeon2FMapKey = "pirate_dungeon_2f";

	public const string Dungeon3FMapKey = "pirate_dungeon_3f";

	public const string Dungeon4FMapKey = "pirate_dungeon_4f";

	public const string HiddenDockMapKey = "hidden_dock";

	public const string ElfGraveMapKey = "elf_grave";

	public const string VillageTownKey = "town_pirate_village";

	public const string VillageLandmarkId = "pirate_front_village";

	public const string DufaNpcId = "npc_dufa";

	public const string BurningWillowTownKey = "town_gludio";

	public const int DuranNpcId = 70094;

	public const string DuranPortServiceKey = "npc_duran";

	public const string DuranName = "杜蘭";

	public const string DockArrivalLandmarkId = "hidden_dock_arrival";

	public const string FrontPortArrivalLandmarkId = "pirate_front_port_arrival";

	public static IReadOnlyList<PirateIslandMap> Maps { get; } = new PirateIslandMap[7]
	{
		new PirateIslandMap("hidden_dock", 445, "隱藏之港"),
		new PirateIslandMap("pirate_wild", 440, "海賊島前半部"),
		new PirateIslandMap("pirate_back", 480, "海賊島後半部"),
		new PirateIslandMap("pirate_dungeon", 441, "海賊島地監1樓"),
		new PirateIslandMap("pirate_dungeon_2f", 442, "海賊島地監2樓"),
		new PirateIslandMap("pirate_dungeon_3f", 443, "海賊島地監3樓"),
		new PirateIslandMap("pirate_dungeon_4f", 444, "海賊島地監4樓")
	};

	public static IReadOnlyList<PirateIslandPortalLink> PortalLinks { get; } = new PirateIslandPortalLink[15]
	{
		Link("pirate_wild", "pirate_front_dungeon_west_entrance", "pirate_dungeon", "pirate_dungeon_1f_west_arrival"),
		Link("pirate_dungeon", "pirate_dungeon_1f_west_exit", "pirate_wild", "pirate_front_dungeon_west_return"),
		Link("pirate_wild", "pirate_front_dungeon_south_entrance", "pirate_dungeon", "pirate_dungeon_1f_north_arrival"),
		Link("pirate_dungeon", "pirate_dungeon_1f_north_exit", "pirate_wild", "pirate_front_dungeon_south_return"),
		Link("pirate_wild", "pirate_front_back_north_crossing", "pirate_back", "pirate_back_front_north_arrival"),
		Link("pirate_back", "pirate_back_front_north_crossing", "pirate_wild", "pirate_front_back_north_return"),
		Link("pirate_wild", "pirate_front_back_south_crossing", "pirate_back", "pirate_back_front_south_arrival"),
		Link("pirate_back", "pirate_back_front_south_crossing", "pirate_wild", "pirate_front_back_south_return"),
		Link("pirate_wild", "pirate_front_port_pier", "hidden_dock", "hidden_dock_arrival"),
		Link("pirate_dungeon", "pirate_dungeon_1f_stairs_down", "pirate_dungeon_2f", "pirate_dungeon_2f_arrival_from_1f"),
		Link("pirate_dungeon_2f", "pirate_dungeon_2f_stairs_up", "pirate_dungeon", "pirate_dungeon_1f_arrival_from_2f"),
		Link("pirate_dungeon_2f", "pirate_dungeon_2f_stairs_down", "pirate_dungeon_3f", "pirate_dungeon_3f_arrival_from_2f"),
		Link("pirate_dungeon_3f", "pirate_dungeon_3f_stairs_up", "pirate_dungeon_2f", "pirate_dungeon_2f_arrival_from_3f"),
		Link("pirate_dungeon_3f", "pirate_dungeon_3f_stairs_down", "pirate_dungeon_4f", "pirate_dungeon_4f_arrival_from_3f"),
		Link("pirate_dungeon_4f", "pirate_dungeon_4f_stairs_up", "pirate_dungeon_3f", "pirate_dungeon_3f_arrival_from_4f")
	};

	public static string DisplayName(string mapKey)
	{
		return Maps.FirstOrDefault((PirateIslandMap map) => string.Equals(map.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public static bool IsPirateIslandMap(string mapKey)
	{
		return DisplayName(mapKey).Length > 0;
	}

	private static PirateIslandPortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new PirateIslandPortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}
}
