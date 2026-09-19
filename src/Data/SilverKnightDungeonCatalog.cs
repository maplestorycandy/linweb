using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class SilverKnightDungeonCatalog
{
	public static IReadOnlyList<SilverKnightDungeonFloor> Floors { get; } = new SilverKnightDungeonFloor[4]
	{
		new SilverKnightDungeonFloor("zone_22", 25, 1, "銀騎士洞窟1樓"),
		new SilverKnightDungeonFloor("zone_23", 26, 2, "銀騎士洞窟2樓"),
		new SilverKnightDungeonFloor("zone_24", 27, 3, "銀騎士洞窟3樓"),
		new SilverKnightDungeonFloor("zone_25", 28, 4, "銀騎士洞窟4樓")
	};

	public static IReadOnlyList<SilverKnightSurfacePassage> SurfacePassages { get; } = new SilverKnightSurfacePassage[5]
	{
		new SilverKnightSurfacePassage("silver_knight_cave_1f_entrance", "銀騎士洞窟1樓入口", 32970, 33511, 32970, 33512, "zone_22", "desert_dungeon_1f_main_entrance_arrival", "desert_dungeon_1f_main_entrance_exit"),
		new SilverKnightSurfacePassage("silver_knight_cave_1f_exit", "銀騎士洞窟1樓出口", 32951, 33502, 32951, 33503, "zone_22", "desert_dungeon_1f_route_to_2f_arrival", "desert_dungeon_1f_route_to_2f_exit"),
		new SilverKnightSurfacePassage("silver_knight_cave_2f_entrance", "銀騎士洞窟2樓入口", 32927, 33515, 32927, 33516, "zone_23", "desert_dungeon_2f_main_entrance_arrival", "desert_dungeon_2f_main_entrance_exit"),
		new SilverKnightSurfacePassage("silver_knight_cave_2f_exit", "銀騎士洞窟2樓出口", 32910, 33503, 32910, 33505, "zone_23", "desert_dungeon_2f_route_to_3f_arrival", "desert_dungeon_2f_route_to_3f_exit"),
		new SilverKnightSurfacePassage("silver_knight_cave_3f_entrance", "銀騎士洞窟3樓入口", 32880, 33511, 32880, 33511, "zone_24", "desert_dungeon_3f_surface_arrival", "desert_dungeon_3f_surface_exit")
	};

	public static IReadOnlyList<SilverKnightDungeonPortalLink> PortalLinks { get; } = new SilverKnightDungeonPortalLink[2]
	{
		new SilverKnightDungeonPortalLink("zone_24", "desert_dungeon_3f_stairs_down", "zone_25", "desert_dungeon_4f_arrival_from_3f"),
		new SilverKnightDungeonPortalLink("zone_25", "desert_dungeon_4f_stairs_up", "zone_24", "desert_dungeon_3f_arrival_from_4f")
	};

	public static string DisplayFloorName(string mapKey)
	{
		return Floors.FirstOrDefault((SilverKnightDungeonFloor floor) => string.Equals(floor.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public static bool IsSilverKnightDungeonMap(string mapKey)
	{
		return DisplayFloorName(mapKey).Length > 0;
	}
}
