using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class DragonValleyDungeonCatalog
{
	public const string AntharasMapKey = "dragon_valley_dungeon_7f";

	public const string AntharasMobKey = "l1j_45682";

	public const string AntharasRoomLandmarkId = "dragon_valley_7f_antharas_room";

	public static IReadOnlyList<DragonValleyDungeonFloor> Floors { get; } = new DragonValleyDungeonFloor[7]
	{
		new DragonValleyDungeonFloor("zone_26", 30, 1, "龍之谷地監1樓"),
		new DragonValleyDungeonFloor("zone_27", 31, 2, "龍之谷地監2樓"),
		new DragonValleyDungeonFloor("zone_28", 32, 3, "龍之谷地監3樓"),
		new DragonValleyDungeonFloor("zone_29", 33, 4, "龍之谷地監4樓"),
		new DragonValleyDungeonFloor("zone_30", 35, 5, "龍之谷地監5樓"),
		new DragonValleyDungeonFloor("zone_31", 36, 6, "龍之谷地監6樓"),
		new DragonValleyDungeonFloor("dragon_valley_dungeon_7f", 37, 7, "安塔瑞斯棲息地", IsAntharasHabitat: true)
	};

	public static IReadOnlyList<DragonValleySurfaceEntrance> SurfaceEntrances { get; } = new DragonValleySurfaceEntrance[4]
	{
		new DragonValleySurfaceEntrance(1, 33348, 32348, "zone_26", "dragon_valley_1f_surface_arrival_01", "dragon_valley_1f_surface_exit_01"),
		new DragonValleySurfaceEntrance(2, 33373, 32385, "zone_26", "dragon_valley_1f_surface_arrival_02", "dragon_valley_1f_surface_exit_02"),
		new DragonValleySurfaceEntrance(3, 33395, 32325, "zone_26", "dragon_valley_1f_surface_arrival_03", "dragon_valley_1f_surface_exit_03"),
		new DragonValleySurfaceEntrance(4, 33414, 32412, "zone_26", "dragon_valley_1f_surface_arrival_04", "dragon_valley_1f_surface_exit_04")
	};

	public static IReadOnlyList<DragonValleyDungeonPortalLink> PortalLinks { get; } = new DragonValleyDungeonPortalLink[19]
	{
		Link("zone_26", "dragon_valley_1f_to_2f_01", "zone_27", "dragon_valley_2f_from_1f_01_arrival"),
		Link("zone_27", "dragon_valley_2f_to_1f_01", "zone_26", "dragon_valley_1f_from_2f_01_arrival"),
		Link("zone_26", "dragon_valley_1f_to_2f_02", "zone_27", "dragon_valley_2f_from_1f_02_arrival"),
		Link("zone_27", "dragon_valley_2f_to_1f_02", "zone_26", "dragon_valley_1f_from_2f_02_arrival"),
		Link("zone_26", "dragon_valley_1f_to_2f_03", "zone_27", "dragon_valley_2f_from_1f_03_arrival"),
		Link("zone_27", "dragon_valley_2f_to_1f_03", "zone_26", "dragon_valley_1f_from_2f_03_arrival"),
		Link("zone_26", "dragon_valley_1f_to_2f_04", "zone_27", "dragon_valley_2f_from_1f_04_arrival"),
		Link("zone_27", "dragon_valley_2f_to_1f_04", "zone_26", "dragon_valley_1f_from_2f_04_arrival"),
		Link("zone_27", "dragon_valley_2f_to_3f_01", "zone_28", "dragon_valley_3f_from_2f_arrival"),
		Link("zone_27", "dragon_valley_2f_to_3f_02", "zone_28", "dragon_valley_3f_from_2f_arrival"),
		Link("zone_28", "dragon_valley_3f_to_2f", "zone_27", "dragon_valley_2f_from_3f_arrival"),
		Link("zone_28", "dragon_valley_3f_to_4f", "zone_29", "dragon_valley_4f_from_3f_arrival"),
		Link("zone_29", "dragon_valley_4f_to_3f", "zone_28", "dragon_valley_3f_from_4f_arrival"),
		Link("zone_29", "dragon_valley_4f_to_5f", "zone_30", "dragon_valley_5f_from_4f_arrival"),
		Link("zone_30", "dragon_valley_5f_to_4f", "zone_29", "dragon_valley_4f_from_5f_arrival"),
		Link("zone_30", "dragon_valley_5f_to_6f", "zone_31", "dragon_valley_6f_from_5f_arrival"),
		Link("zone_31", "dragon_valley_6f_to_5f", "zone_30", "dragon_valley_5f_from_6f_arrival"),
		Link("zone_31", "dragon_valley_6f_to_7f", "dragon_valley_dungeon_7f", "dragon_valley_7f_from_6f_arrival"),
		Link("dragon_valley_dungeon_7f", "dragon_valley_7f_to_6f", "zone_31", "dragon_valley_6f_from_7f_arrival")
	};

	public static string DisplayFloorName(string mapKey)
	{
		return Floors.FirstOrDefault((DragonValleyDungeonFloor floor) => string.Equals(floor.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public static bool IsDragonValleyDungeonMap(string mapKey)
	{
		return DisplayFloorName(mapKey).Length > 0;
	}

	private static DragonValleyDungeonPortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new DragonValleyDungeonPortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}
}
