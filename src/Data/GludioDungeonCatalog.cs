using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class GludioDungeonCatalog
{
	public const string OrimLandmarkId = "gludio_dungeon_7f_orim";

	public static IReadOnlyList<GludioDungeonFloor> Floors { get; } = (from floor in Enumerable.Range(1, 7)
		select new GludioDungeonFloor($"zone_{floor + 5:00}", floor + 6, floor, $"古魯丁地監{floor}樓")).ToArray();

	public static IReadOnlyList<GludioDungeonPortalLink> PortalLinks { get; } = new GludioDungeonPortalLink[34]
	{
		Link("zone_06", "gludio_dungeon_1f_stairs_down", "zone_07", "gludio_dungeon_2f_arrival_from_1f"),
		Link("zone_07", "gludio_dungeon_2f_stairs_up", "zone_06", "gludio_dungeon_1f_arrival_from_2f"),
		Link("zone_07", "gludio_dungeon_2f_stairs_down", "zone_08", "gludio_dungeon_3f_arrival_from_2f"),
		Link("zone_08", "gludio_dungeon_3f_stairs_up", "zone_07", "gludio_dungeon_2f_arrival_from_3f"),
		Link("zone_08", "gludio_dungeon_3f_stairs_down", "zone_09", "gludio_dungeon_4f_arrival_from_3f"),
		Link("zone_09", "gludio_dungeon_4f_stairs_up", "zone_08", "gludio_dungeon_3f_arrival_from_4f"),
		Link("zone_09", "gludio_dungeon_4f_stairs_down", "zone_10", "gludio_dungeon_5f_arrival_from_4f"),
		Link("zone_10", "gludio_dungeon_5f_stairs_up", "zone_09", "gludio_dungeon_4f_arrival_from_5f"),
		Link("zone_10", "gludio_dungeon_5f_stairs_down", "zone_11", "gludio_dungeon_6f_arrival_from_5f"),
		Link("zone_11", "gludio_dungeon_6f_stairs_up", "zone_10", "gludio_dungeon_5f_arrival_from_6f"),
		Link("zone_11", "gludio_dungeon_6f_stairs_down", "zone_12", "gludio_dungeon_7f_arrival_from_6f"),
		Link("zone_12", "gludio_dungeon_7f_stairs_up", "zone_11", "gludio_dungeon_6f_arrival_from_7f"),
		Internal("zone_07", "gludio_dungeon_2f_portal_01a", "gludio_dungeon_2f_portal_01b_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_01b", "gludio_dungeon_2f_portal_01a_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_02a", "gludio_dungeon_2f_portal_02b_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_02b", "gludio_dungeon_2f_portal_02a_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_03a", "gludio_dungeon_2f_portal_03b_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_03b", "gludio_dungeon_2f_portal_03a_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_04a", "gludio_dungeon_2f_portal_04b_arrival"),
		Internal("zone_07", "gludio_dungeon_2f_portal_04b", "gludio_dungeon_2f_portal_04a_arrival"),
		Internal("zone_10", "gludio_dungeon_5f_portal_01a", "gludio_dungeon_5f_portal_01b_arrival"),
		Internal("zone_10", "gludio_dungeon_5f_portal_01b", "gludio_dungeon_5f_portal_01a_arrival"),
		Internal("zone_10", "gludio_dungeon_5f_portal_02a", "gludio_dungeon_5f_portal_02b_arrival"),
		Internal("zone_10", "gludio_dungeon_5f_portal_02b", "gludio_dungeon_5f_portal_02a_arrival"),
		Internal("zone_10", "gludio_dungeon_5f_portal_03a", "gludio_dungeon_5f_portal_03b_arrival"),
		Internal("zone_10", "gludio_dungeon_5f_portal_03b", "gludio_dungeon_5f_portal_03a_arrival"),
		Internal("zone_11", "gludio_dungeon_6f_portal_01a", "gludio_dungeon_6f_portal_01b_arrival"),
		Internal("zone_11", "gludio_dungeon_6f_portal_01b", "gludio_dungeon_6f_portal_01a_arrival"),
		Internal("zone_12", "gludio_dungeon_7f_portal_01a", "gludio_dungeon_7f_portal_01b_arrival"),
		Internal("zone_12", "gludio_dungeon_7f_portal_01b", "gludio_dungeon_7f_portal_01a_arrival"),
		Internal("zone_12", "gludio_dungeon_7f_portal_02a", "gludio_dungeon_7f_portal_02b_arrival"),
		Internal("zone_12", "gludio_dungeon_7f_portal_02b", "gludio_dungeon_7f_portal_02a_arrival"),
		Internal("zone_12", "gludio_dungeon_7f_portal_03a", "gludio_dungeon_7f_portal_03b_arrival"),
		Internal("zone_12", "gludio_dungeon_7f_portal_03b", "gludio_dungeon_7f_portal_03a_arrival")
	};

	public static IReadOnlyList<GludioDungeonExternalExit> ExternalExits { get; } = new GludioDungeonExternalExit[3]
	{
		new GludioDungeonExternalExit("zone_06", "gludio_dungeon_1f_surface_exit", 4, "mainland", "gludio_dungeon_1f_surface_arrival"),
		new GludioDungeonExternalExit("zone_12", "gludio_dungeon_7f_undersea_passage", 14, "undersea_passage", "gludio_dungeon_7f_undersea_arrival"),
		new GludioDungeonExternalExit("zone_12", "gludio_dungeon_7f_jim_dungeon_portal", 237, "jim_dungeon", "gludio_dungeon_7f_jim_dungeon_arrival")
	};

	public static string DisplayFloorName(string mapKey)
	{
		return Floors.FirstOrDefault((GludioDungeonFloor floor) => string.Equals(floor.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public static bool IsGludioDungeonMap(string mapKey)
	{
		return DisplayFloorName(mapKey).Length > 0;
	}

	private static GludioDungeonPortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new GludioDungeonPortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}

	private static GludioDungeonPortalLink Internal(string mapKey, string portalLandmarkId, string arrivalLandmarkId)
	{
		return new GludioDungeonPortalLink(mapKey, portalLandmarkId, mapKey, arrivalLandmarkId, IsInternalPortal: true);
	}
}
