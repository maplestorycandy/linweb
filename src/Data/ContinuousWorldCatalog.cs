using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace IdleLineage.Data;

public static class ContinuousWorldCatalog
{
	private static readonly ContinuousWorldRegion[] Definitions = new ContinuousWorldRegion[2]
	{
		new ContinuousWorldRegion("mainland_south", "亞丁大陸西部、南部、龍之谷、火龍窟、歐瑞、肯特、奇岩與亞丁", Array.AsReadOnly(new string[14]
		{
			"elf_forest", "zone_01", "orc_fortress", "gludio", "windwood", "desert", "silver_knight", "heine", "kent", "giran",
			"dragon_valley", "fire_dragon", "zone_02", "zone_03"
		}), Array.AsReadOnly(new string[10] { "town_elf", "town_gludin", "town_windwood_castle", "town_silver_knight", "town_heine", "town_kent_castle", "town_giran", "town_aden", "town_oren", "town_ivory_tower" }), Array.AsReadOnly(new WorldTravelRoute[2]
		{
			new WorldTravelRoute("talking_island_to_gludio", "npc_ship", "talking_island", "talking_island_port_bridge", "mainland_south", "gludio_village", RequiresNpcInteraction: true),
			new WorldTravelRoute("gludio_to_talking_island", "npc_ship", "mainland_south", "gludio_village", "talking_island", "talking_island_port_bridge", RequiresNpcInteraction: true)
		})),
		new ContinuousWorldRegion("silent_outer", "沉默洞穴與沉默洞穴周邊", Array.AsReadOnly(new string[1] { "silent_outer" }), Array.AsReadOnly(new string[1] { "town_silent" }), Array.AsReadOnly(new WorldTravelRoute[2]
		{
			new WorldTravelRoute("mainland_to_silent_cave", "map_gate", "mainland_south", "silent_cave_entrance", "silent_outer", SilentCaveCatalog.SurfaceEntrance.ArrivalLandmarkId, RequiresNpcInteraction: false),
			new WorldTravelRoute("silent_cave_to_mainland", "map_gate", "silent_outer", SilentCaveCatalog.SurfaceEntrance.SurfaceExitLandmarkId, "mainland_south", "silent_cave_entrance", RequiresNpcInteraction: false)
		}))
	};

	private static readonly IReadOnlyDictionary<string, ContinuousWorldRegion> ByMap = BuildIndex(Definitions, (ContinuousWorldRegion region) => new string[1] { region.MapKey });

	private static readonly IReadOnlyDictionary<string, ContinuousWorldRegion> ByDestination = BuildIndex(Definitions, (ContinuousWorldRegion region) => region.AreaKeys.Concat(region.TownKeys));

	private static readonly IReadOnlyDictionary<string, WorldTravelRoute> RoutesById = new ReadOnlyDictionary<string, WorldTravelRoute>(Definitions.SelectMany((ContinuousWorldRegion region) => region.Routes).ToDictionary<WorldTravelRoute, string>((WorldTravelRoute route) => route.Id, StringComparer.Ordinal));

	public static IReadOnlyList<ContinuousWorldRegion> All => Definitions;

	public static ContinuousWorldRegion? FindByMap(string mapKey)
	{
		return ByMap.GetValueOrDefault(mapKey);
	}

	public static ContinuousWorldRegion? FindByDestination(string destinationKey)
	{
		return ByDestination.GetValueOrDefault(destinationKey);
	}

	public static WorldTravelRoute? FindRoute(string routeId)
	{
		return RoutesById.GetValueOrDefault(routeId);
	}

	private static IReadOnlyDictionary<string, ContinuousWorldRegion> BuildIndex(IEnumerable<ContinuousWorldRegion> definitions, Func<ContinuousWorldRegion, IEnumerable<string>> keys)
	{
		Dictionary<string, ContinuousWorldRegion> dictionary = new Dictionary<string, ContinuousWorldRegion>(StringComparer.Ordinal);
		foreach (ContinuousWorldRegion definition in definitions)
		{
			foreach (string item in keys(definition))
			{
				if (!dictionary.TryAdd(item, definition))
				{
					throw new InvalidDataException("Continuous-world key '" + item + "' is duplicated.");
				}
			}
		}
		return new ReadOnlyDictionary<string, ContinuousWorldRegion>(dictionary);
	}
}
