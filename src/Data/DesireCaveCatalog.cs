using System;
using System.Collections.Generic;

namespace IdleLineage.Data;

public static class DesireCaveCatalog
{
	public const string EntranceMapKey = "desire_cave_entrance";

	public const string HallMapKey = "desire_cave_hall";

	public const string WindRealmMapKey = "desire_realm_wind";

	public const string WaterRealmMapKey = "desire_realm_water";

	public const string EarthRealmMapKey = "desire_realm_earth";

	public const string FireRealmMapKey = "desire_realm_fire";

	public const string SurfaceMapKey = "mainland_south";

	public const string SurfaceMapDisplayName = "亞丁大陸";

	public const string SurfacePortalLandmarkId = "desire_cave_portal";

	public const string SurfacePortalArrivalLandmarkId = "desire_cave_portal_arrival";

	public const int ConsulNpcId = 80064;

	public const string ConsulPortServiceKey = "npc_desire_cave_consul";

	public const string ConsulName = "執政官";

	public const string ConsulTitle = "[炎魔謁見所]";

	public const string FlameAudienceTownKey = "town_flame_audience";

	public static IReadOnlyList<DesireCaveMap> Maps { get; } = new DesireCaveMap[6]
	{
		new DesireCaveMap("desire_cave_entrance", 600, "慾望洞穴入口"),
		new DesireCaveMap("desire_cave_hall", 601, "慾望洞穴大廳"),
		new DesireCaveMap("desire_realm_water", 604, "水之領域"),
		new DesireCaveMap("desire_realm_fire", 605, "火之領域"),
		new DesireCaveMap("desire_realm_wind", 606, "風之領域"),
		new DesireCaveMap("desire_realm_earth", 607, "地之領域")
	};

	public static IReadOnlyList<string> RealmMapKeys { get; } = new string[4] { "desire_realm_wind", "desire_realm_water", "desire_realm_earth", "desire_realm_fire" };

	public static IReadOnlyList<DesireCavePortalLink> PortalLinks { get; } = new DesireCavePortalLink[12]
	{
		new DesireCavePortalLink("mainland_south", "desire_cave_portal", "desire_cave_entrance", "desire_cave_arrival"),
		new DesireCavePortalLink("desire_cave_entrance", "desire_cave_surface_exit", "mainland_south", "desire_cave_portal_arrival"),
		new DesireCavePortalLink("desire_cave_entrance", "desire_cave_hall_stairs", "desire_cave_hall", "desire_cave_hall_arrival_from_entrance"),
		new DesireCavePortalLink("desire_cave_hall", "desire_cave_hall_exit", "desire_cave_entrance", "desire_cave_arrival_from_hall"),
		new DesireCavePortalLink("desire_cave_hall", "desire_cave_hall_wind_gate", "desire_realm_wind", "desire_realm_wind_arrival_from_hall"),
		new DesireCavePortalLink("desire_realm_wind", "desire_realm_wind_exit", "desire_cave_hall", "desire_cave_hall_arrival_from_wind"),
		new DesireCavePortalLink("desire_cave_hall", "desire_cave_hall_water_gate", "desire_realm_water", "desire_realm_water_arrival_from_hall"),
		new DesireCavePortalLink("desire_realm_water", "desire_realm_water_exit", "desire_cave_hall", "desire_cave_hall_arrival_from_water"),
		new DesireCavePortalLink("desire_cave_hall", "desire_cave_hall_earth_gate", "desire_realm_earth", "desire_realm_earth_arrival_from_hall"),
		new DesireCavePortalLink("desire_realm_earth", "desire_realm_earth_exit", "desire_cave_hall", "desire_cave_hall_arrival_from_earth"),
		new DesireCavePortalLink("desire_cave_hall", "desire_cave_hall_fire_gate", "desire_realm_fire", "desire_realm_fire_arrival_from_hall"),
		new DesireCavePortalLink("desire_realm_fire", "desire_realm_fire_exit", "desire_cave_hall", "desire_cave_hall_arrival_from_fire")
	};

	public static string DisplayName(string mapKey)
	{
		foreach (DesireCaveMap map in Maps)
		{
			if (string.Equals(map.MapKey, mapKey, StringComparison.Ordinal))
			{
				return map.DisplayName;
			}
		}
		return mapKey;
	}

	public static bool IsDesireCaveMap(string mapKey)
	{
		foreach (DesireCaveMap map in Maps)
		{
			if (string.Equals(map.MapKey, mapKey, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
