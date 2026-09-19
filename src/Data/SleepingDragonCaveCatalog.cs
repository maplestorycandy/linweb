using System.Collections.Generic;

namespace IdleLineage.Data;

public static class SleepingDragonCaveCatalog
{
	public const string SurfaceMapKey = "mainland_south";

	public const string SurfaceEntranceLandmarkId = "sleeping_dragon_cave_entrance";

	public static IReadOnlyList<SleepingDragonCavePortalLink> PortalLinks { get; } = new SleepingDragonCavePortalLink[14]
	{
		Link("mainland_south", "sleeping_dragon_cave_entrance", "zone_15", "sleeping_dragon_1f_surface_arrival"),
		Link("zone_15", "sleeping_dragon_1f_surface_exit", "mainland_south", "sleeping_dragon_cave_entrance"),
		Link("zone_15", "sleeping_dragon_1f_stairs_down", "zone_16", "sleeping_dragon_2f_arrival_from_1f"),
		Link("zone_16", "sleeping_dragon_2f_stairs_up", "zone_15", "sleeping_dragon_1f_stairs_return"),
		Link("zone_16", "sleeping_dragon_2f_stairs_down", "zone_17", "sleeping_dragon_3f_arrival_from_2f"),
		Link("zone_17", "sleeping_dragon_3f_stairs_up", "zone_16", "sleeping_dragon_2f_arrival_from_3f"),
		Internal("zone_15", "sleeping_dragon_1f_portal_01a", "sleeping_dragon_1f_portal_01b_arrival"),
		Internal("zone_15", "sleeping_dragon_1f_portal_01b", "sleeping_dragon_1f_portal_01a_arrival"),
		Internal("zone_15", "sleeping_dragon_1f_portal_02a", "sleeping_dragon_1f_portal_02b_arrival"),
		Internal("zone_15", "sleeping_dragon_1f_portal_02b", "sleeping_dragon_1f_portal_02a_arrival"),
		Internal("zone_16", "sleeping_dragon_2f_portal_01a", "sleeping_dragon_2f_portal_01b_arrival"),
		Internal("zone_16", "sleeping_dragon_2f_portal_01b", "sleeping_dragon_2f_portal_01a_arrival"),
		Internal("zone_17", "sleeping_dragon_3f_portal_02a", "sleeping_dragon_3f_portal_02b_arrival"),
		Internal("zone_17", "sleeping_dragon_3f_portal_02b", "sleeping_dragon_3f_portal_02a_arrival")
	};

	public static string DisplayName(string mapKey)
	{
		return mapKey switch
		{
			"zone_15" => "眠龍洞穴1樓", 
			"zone_16" => "眠龍洞穴2樓", 
			"zone_17" => "眠龍洞穴3樓", 
			"mainland_south" => "亞丁大陸", 
			_ => mapKey, 
		};
	}

	private static SleepingDragonCavePortalLink Link(string sourceMapKey, string portalLandmarkId, string destinationMapKey, string arrivalLandmarkId)
	{
		return new SleepingDragonCavePortalLink(sourceMapKey, portalLandmarkId, destinationMapKey, arrivalLandmarkId);
	}

	private static SleepingDragonCavePortalLink Internal(string mapKey, string portalLandmarkId, string arrivalLandmarkId)
	{
		return Link(mapKey, portalLandmarkId, mapKey, arrivalLandmarkId);
	}
}
