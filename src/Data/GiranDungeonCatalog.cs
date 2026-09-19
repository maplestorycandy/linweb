using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class GiranDungeonCatalog
{
	public static IReadOnlyList<GiranDungeonFloor> Floors { get; } = new GiranDungeonFloor[4]
	{
		new GiranDungeonFloor("zone_18", 53, 1, "奇岩地監1樓"),
		new GiranDungeonFloor("zone_19", 54, 2, "奇岩地監2樓"),
		new GiranDungeonFloor("zone_20", 55, 3, "奇岩地監3樓"),
		new GiranDungeonFloor("zone_21", 56, 4, "奇岩地監4樓")
	};

	public static GiranDungeonSurfaceEntrance SurfaceEntrance { get; } = new GiranDungeonSurfaceEntrance(33313, 33061, 33313, 33060, "zone_18", "giran_dungeon_1f_surface_arrival", "giran_dungeon_1f_surface_exit");

	public static IReadOnlyList<GiranDungeonPortalLink> PortalLinks { get; } = new GiranDungeonPortalLink[6]
	{
		Link("zone_18", "giran_dungeon_1f_stairs_down", "zone_19", "giran_dungeon_2f_arrival_from_1f"),
		Link("zone_19", "giran_dungeon_2f_stairs_up", "zone_18", "giran_dungeon_1f_arrival_from_2f"),
		Link("zone_19", "giran_dungeon_2f_stairs_down", "zone_20", "giran_dungeon_3f_arrival_from_2f"),
		Link("zone_20", "giran_dungeon_3f_stairs_up", "zone_19", "giran_dungeon_2f_arrival_from_3f"),
		Link("zone_20", "giran_dungeon_3f_stairs_down", "zone_21", "giran_dungeon_4f_arrival_from_3f"),
		Link("zone_21", "giran_dungeon_4f_stairs_up", "zone_20", "giran_dungeon_3f_arrival_from_4f")
	};

	public static string DisplayFloorName(string mapKey)
	{
		return Floors.FirstOrDefault((GiranDungeonFloor floor) => string.Equals(floor.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public static bool IsGiranDungeonMap(string mapKey)
	{
		return DisplayFloorName(mapKey).Length > 0;
	}

	private static GiranDungeonPortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new GiranDungeonPortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}
}
