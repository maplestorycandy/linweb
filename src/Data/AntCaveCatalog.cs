using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class AntCaveCatalog
{
	public const string FirstFloorName = "螞蟻洞穴1F";

	public const string SecondFloorName = "螞蟻洞穴2F";

	public const string QueenLairMapKey = "ant_queen_lair";

	public const string QueenLairName = "巨蟻女皇棲息地";

	public const string QueenRoomLandmarkId = "ant_cave_queen_room";

	public static IReadOnlyList<AntCaveSegment> Segments { get; } = new AntCaveSegment[10]
	{
		new AntCaveSegment("zone_32", 49, "A", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_b", 44, "B", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_c", 47, "C", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_d", 48, "D", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_e", 45, "E", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_f", 46, "F", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_g", 50, "G", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_32_h", 43, "H", "螞蟻洞穴1F"),
		new AntCaveSegment("zone_33", 51, "bottom", "螞蟻洞穴2F", IsBottomFloor: true),
		new AntCaveSegment("ant_queen_lair", 543, "queen", "巨蟻女皇棲息地")
	};

	public static IReadOnlyList<AntCaveSurfaceEntrance> SurfaceEntrances { get; } = new AntCaveSurfaceEntrance[8]
	{
		new AntCaveSurfaceEntrance("A", 32924, 33252, "zone_32", "ant_cave_a_surface_arrival", "ant_cave_a_surface_portal"),
		new AntCaveSurfaceEntrance("B", 32914, 33220, "zone_32_b", "ant_cave_b_surface_arrival", "ant_cave_b_surface_portal"),
		new AntCaveSurfaceEntrance("C", 32839, 33172, "zone_32_c", "ant_cave_c_surface_arrival", "ant_cave_c_surface_portal"),
		new AntCaveSurfaceEntrance("D", 32795, 33189, "zone_32_d", "ant_cave_d_surface_arrival", "ant_cave_d_surface_portal"),
		new AntCaveSurfaceEntrance("E", 32789, 33255, "zone_32_e", "ant_cave_e_surface_arrival", "ant_cave_e_surface_portal"),
		new AntCaveSurfaceEntrance("F", 32848, 33294, "zone_32_f", "ant_cave_f_surface_arrival", "ant_cave_f_surface_portal"),
		new AntCaveSurfaceEntrance("G", 32788, 33149, "zone_32_g", "ant_cave_g_surface_arrival", "ant_cave_g_surface_portal"),
		new AntCaveSurfaceEntrance("H", 32755, 33208, "zone_32_h", "ant_cave_h_surface_arrival", "ant_cave_h_surface_portal")
	};

	public static IReadOnlyList<AntCavePortalLink> PortalLinks { get; } = new AntCavePortalLink[18]
	{
		Link("zone_32", "ant_cave_a_to_b_portal", "zone_32_b", "ant_cave_b_from_a_arrival"),
		Link("zone_32_b", "ant_cave_b_to_a_portal", "zone_32", "ant_cave_a_from_b_arrival"),
		Link("zone_32", "ant_cave_a_to_f_portal", "zone_32_f", "ant_cave_f_from_a_arrival"),
		Link("zone_32_f", "ant_cave_f_to_a_portal", "zone_32", "ant_cave_a_from_f_arrival"),
		Link("zone_32_c", "ant_cave_c_to_h_portal", "zone_32_h", "ant_cave_h_from_c_arrival"),
		Link("zone_32_h", "ant_cave_h_to_c_portal", "zone_32_c", "ant_cave_c_from_h_arrival"),
		Link("zone_32_d", "ant_cave_d_to_g_portal", "zone_32_g", "ant_cave_g_from_d_arrival"),
		Link("zone_32_g", "ant_cave_g_to_d_portal", "zone_32_d", "ant_cave_d_from_g_arrival"),
		Link("zone_32_e", "ant_cave_e_to_g_portal", "zone_32_g", "ant_cave_g_from_e_arrival"),
		Link("zone_32_g", "ant_cave_g_to_e_portal", "zone_32_e", "ant_cave_e_from_g_arrival"),
		Link("zone_32", "ant_cave_a_to_bottom_portal", "zone_33", "ant_cave_bottom_from_a_arrival"),
		Link("zone_33", "ant_cave_bottom_to_a_portal", "zone_32", "ant_cave_a_from_bottom_arrival"),
		Link("zone_32_c", "ant_cave_c_to_bottom_portal", "zone_33", "ant_cave_bottom_from_c_arrival"),
		Link("zone_33", "ant_cave_bottom_to_c_portal", "zone_32_c", "ant_cave_c_from_bottom_arrival"),
		Link("zone_32_d", "ant_cave_d_to_bottom_portal", "zone_33", "ant_cave_bottom_from_d_arrival"),
		Link("zone_33", "ant_cave_bottom_to_d_portal", "zone_32_d", "ant_cave_d_from_bottom_arrival"),
		Link("zone_33", "ant_cave_queen_room", "ant_queen_lair", "ant_queen_lair_arrival"),
		Link("ant_queen_lair", "ant_queen_lair_passage_up", "zone_33", "ant_cave_bottom_from_queen_arrival")
	};

	public static string DisplayFloorName(string mapKey)
	{
		return Segments.FirstOrDefault((AntCaveSegment segment) => string.Equals(segment.MapKey, mapKey, StringComparison.Ordinal))?.DisplayFloorName ?? string.Empty;
	}

	public static bool IsAntCaveMap(string mapKey)
	{
		return DisplayFloorName(mapKey).Length > 0;
	}

	private static AntCavePortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new AntCavePortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}
}
