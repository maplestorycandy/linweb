using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class HeineDungeonCatalog
{
	public const string EvaKingdomMapKey = "eva_kingdom";

	public const string FafurionLairMapKey = "fafurion_lair";

	public const int FafurionLairSourceMapId = 65;

	public const string FafurionMobKey = "fafurion";

	public const string FafurionRoomLandmarkId = "fafurion_lair_boss_room";

	public static IReadOnlyList<HeineDungeonFloor> Floors { get; } = new HeineDungeonFloor[4]
	{
		new HeineDungeonFloor("zone_34", 59, 1, "海音地監1樓"),
		new HeineDungeonFloor("zone_35", 60, 2, "海音地監2樓"),
		new HeineDungeonFloor("zone_36", 61, 3, "海音地監3樓"),
		new HeineDungeonFloor("eva_kingdom", 63, 4, "伊娃王國", IsEvaKingdom: true)
	};

	public static HeineDungeonSurfaceEntrance SurfaceEntrance { get; } = new HeineDungeonSurfaceEntrance(33628, 33505, 33627, 33505, "zone_34", "heine_dungeon_1f_surface_arrival", "heine_dungeon_1f_surface_exit");

	public static IReadOnlyList<HeineDungeonPortalLink> PortalLinks { get; } = new HeineDungeonPortalLink[8]
	{
		Link("zone_34", "heine_dungeon_1f_stairs_down", "zone_35", "heine_dungeon_2f_arrival_from_1f"),
		Link("zone_35", "heine_dungeon_2f_stairs_up", "zone_34", "heine_dungeon_1f_arrival_from_2f"),
		Link("zone_35", "heine_dungeon_2f_stairs_down", "zone_36", "heine_dungeon_3f_arrival_from_2f"),
		Link("zone_36", "heine_dungeon_3f_stairs_up", "zone_35", "heine_dungeon_2f_arrival_from_3f"),
		Link("zone_36", "heine_dungeon_3f_to_eva_kingdom", "eva_kingdom", "eva_kingdom_arrival_from_heine_3f"),
		Link("eva_kingdom", "eva_kingdom_to_heine_3f", "zone_36", "heine_dungeon_3f_arrival_from_eva"),
		Link("eva_kingdom", "eva_kingdom_fafurion_seal", "fafurion_lair", "fafurion_lair_arrival"),
		Link("fafurion_lair", "fafurion_lair_exit", "eva_kingdom", "eva_kingdom_arrival_from_fafurion")
	};

	public static string DisplayFloorName(string mapKey)
	{
		return Floors.FirstOrDefault((HeineDungeonFloor floor) => string.Equals(floor.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public static bool IsHeineDungeonMap(string mapKey)
	{
		if (DisplayFloorName(mapKey).Length <= 0)
		{
			return string.Equals(mapKey, "fafurion_lair", StringComparison.Ordinal);
		}
		return true;
	}

	private static HeineDungeonPortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new HeineDungeonPortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}
}
