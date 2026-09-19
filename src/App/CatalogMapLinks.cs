using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class CatalogMapLinks
{
	private const string RetiredAntharasInstanceEndpoint = "antharas_lair";

	private static readonly object Sync = new object();

	private static readonly Dictionary<string, string> DisplayNames = BuildDisplayNames();

	private static readonly Dictionary<string, List<MapLinks.Gate>> Links = BuildStaticLinks();

	private static readonly HashSet<string> WalkOnlyMaps = BuildWalkOnlyMaps();

	private static bool _rastabadConfigured;

	private static bool _classicMapsConfigured;

	public static IReadOnlyList<MapLinks.Gate> For(string mapKey)
	{
		lock (Sync)
		{
			List<MapLinks.Gate> value;
			return Links.TryGetValue(mapKey, out value) ? value.ToArray() : Array.Empty<MapLinks.Gate>();
		}
	}

	public static bool HasGates(string mapKey)
	{
		lock (Sync)
		{
			List<MapLinks.Gate> value;
			return Links.TryGetValue(mapKey, out value) && value.Count > 0;
		}
	}

	public static bool IsWalkOnly(string mapKey)
	{
		lock (Sync)
		{
			return WalkOnlyMaps.Contains(mapKey);
		}
	}

	public static string DisplayName(IGameData data, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		lock (Sync)
		{
			if (DisplayNames.TryGetValue(mapKey, out string value))
			{
				return value;
			}
		}
		if (L1jHouseCatalog.Load(data).TryByBasementMap(mapKey, out L1jHouseDefinition house) && (object)house != null)
		{
			string text = house.Name + "地下盟屋";
			lock (Sync)
			{
				DisplayNames.TryAdd(mapKey, text);
				return text;
			}
		}
		if (!WorldMapCatalog.TryGetDestination(data, mapKey, out MapDestination destination) || (object)destination == null || string.IsNullOrWhiteSpace(destination.Name))
		{
			return mapKey;
		}
		lock (Sync)
		{
			DisplayNames.TryAdd(mapKey, destination.Name);
		}
		return destination.Name;
	}

	public static void ConfigureRastabad(string dataDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory, "dataDirectory");
		lock (Sync)
		{
			if (_rastabadConfigured)
			{
				return;
			}
			RastabadMapCatalog rastabadMapCatalog = RastabadMapCatalog.Load(dataDirectory);
			foreach (RastabadMap map in rastabadMapCatalog.Maps)
			{
				DisplayNames[map.MapKey] = map.DisplayName;
			}
			Add(rastabadMapCatalog.RemoteEntrance.SourceMapKey, rastabadMapCatalog.RemoteEntrance.DestinationMapKey, rastabadMapCatalog.DisplayName(rastabadMapCatalog.RemoteEntrance.DestinationMapKey), rastabadMapCatalog.RemoteEntrance.PortalLandmarkId, rastabadMapCatalog.RemoteEntrance.ArrivalLandmarkId);
			foreach (RastabadPortalLink portalLink in rastabadMapCatalog.PortalLinks)
			{
				Add(portalLink.SourceMapKey, portalLink.DestinationMapKey, rastabadMapCatalog.DisplayName(portalLink.DestinationMapKey), portalLink.PortalLandmarkId, portalLink.ArrivalLandmarkId);
			}
			foreach (RastabadMap map2 in rastabadMapCatalog.Maps)
			{
				WalkOnlyMaps.Add(map2.MapKey);
			}
			_rastabadConfigured = true;
		}
	}

	public static void ConfigureClassicMaps(string dataDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory, "dataDirectory");
		lock (Sync)
		{
			if (_classicMapsConfigured)
			{
				return;
			}
			L1jClassicMapCatalog l1jClassicMapCatalog = L1jClassicMapCatalog.Load(dataDirectory);
			foreach (L1jClassicMapDefinition map in l1jClassicMapCatalog.Maps)
			{
				DisplayNames[map.MapKey] = map.DisplayName;
				WalkOnlyMaps.Add(map.MapKey);
			}
			foreach (L1jClassicMapLink link in l1jClassicMapCatalog.Links)
			{
				if (!string.Equals(link.SourceMapKey, "antharas_lair", StringComparison.Ordinal) && !string.Equals(link.DestinationMapKey, "antharas_lair", StringComparison.Ordinal))
				{
					Add(Links, link.SourceMapKey, link.DestinationMapKey, link.DestinationName, null, null, link.SourceGameCell, link.DestinationGameCell);
				}
			}
			_classicMapsConfigured = true;
		}
	}

	private static Dictionary<string, string> BuildDisplayNames()
	{
		Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["mainland_south"] = "亞丁大陸",
			["talking_island"] = "說話之島",
			["aden_sewer"] = "亞丁下水道",
			["oblivion_island"] = "遺忘之島",
			["fafurion_lair"] = "法利昂巢穴",
			["balrog_room"] = "炎魔房間"
		};
		foreach (IntegratedTownDefinition item in IntegratedTownCatalog.All)
		{
			Add(item.MapKey, item.HuntingAreaName);
		}
		foreach (AntCaveSegment segment in AntCaveCatalog.Segments)
		{
			Add(segment.MapKey, segment.DisplayFloorName);
		}
		foreach (DragonValleyDungeonFloor floor in DragonValleyDungeonCatalog.Floors)
		{
			Add(floor.MapKey, floor.DisplayName);
		}
		foreach (GiranDungeonFloor floor2 in GiranDungeonCatalog.Floors)
		{
			Add(floor2.MapKey, floor2.DisplayName);
		}
		foreach (GludioDungeonFloor floor3 in GludioDungeonCatalog.Floors)
		{
			Add(floor3.MapKey, floor3.DisplayName);
		}
		foreach (HeineDungeonFloor floor4 in HeineDungeonCatalog.Floors)
		{
			Add(floor4.MapKey, floor4.DisplayName);
		}
		foreach (SilverKnightDungeonFloor floor5 in SilverKnightDungeonCatalog.Floors)
		{
			Add(floor5.MapKey, floor5.DisplayName);
		}
		foreach (TowerFloor floor6 in TowerOfInsolenceCatalog.Floors)
		{
			Add(floor6.MapKey, floor6.DisplayName);
		}
		foreach (IvoryTowerFloorDefinition floor7 in IvoryTowerCatalog.Floors)
		{
			Add(floor7.MapKey, floor7.DisplayName);
		}
		foreach (IvoryTowerHiddenDefinition hiddenMap in IvoryTowerCatalog.HiddenMaps)
		{
			Add(hiddenMap.HiddenMapKey, hiddenMap.DisplayName);
		}
		foreach (CrystalCaveMap map in CrystalCaveCatalog.Maps)
		{
			Add(map.MapKey, map.DisplayName);
		}
		Add("oum_dungeon", "歐姆地監");
		foreach (ShadowTempleFloor floor8 in ShadowTempleCatalog.Floors)
		{
			Add(floor8.MapKey, floor8.DisplayName);
		}
		foreach (DesireCaveMap map2 in DesireCaveCatalog.Maps)
		{
			Add(map2.MapKey, map2.DisplayName);
		}
		foreach (PirateIslandMap map3 in PirateIslandCatalog.Maps)
		{
			Add(map3.MapKey, map3.DisplayName);
		}
		foreach (ThebesMapDefinition map4 in ThebesMapCatalog.Maps)
		{
			Add(map4.MapKey, map4.DisplayName);
		}
		foreach (TikalMapDefinition map5 in TikalMapCatalog.Maps)
		{
			Add(map5.MapKey, map5.DisplayName);
		}
		Add("zone_15", SleepingDragonCaveCatalog.DisplayName("zone_15"));
		Add("zone_16", SleepingDragonCaveCatalog.DisplayName("zone_16"));
		Add("zone_17", SleepingDragonCaveCatalog.DisplayName("zone_17"));
		return names;
		void Add(string mapKey, string displayName)
		{
			if (!string.IsNullOrWhiteSpace(mapKey) && !string.IsNullOrWhiteSpace(displayName))
			{
				names.TryAdd(mapKey, displayName);
			}
		}
	}

	private static void RememberDisplayName(string mapKey, string displayName)
	{
		if (!string.IsNullOrWhiteSpace(mapKey) && !string.IsNullOrWhiteSpace(displayName))
		{
			DisplayNames.TryAdd(mapKey, displayName);
		}
	}

	private static Dictionary<string, List<MapLinks.Gate>> BuildStaticLinks()
	{
		Dictionary<string, List<MapLinks.Gate>> result = new Dictionary<string, List<MapLinks.Gate>>(StringComparer.Ordinal);
		foreach (AntCavePortalLink portalLink in AntCaveCatalog.PortalLinks)
		{
			Add(result, portalLink.SourceMapKey, portalLink.DestinationMapKey, AntCaveCatalog.DisplayFloorName(portalLink.DestinationMapKey), portalLink.PortalLandmarkId, portalLink.ArrivalLandmarkId);
		}
		foreach (AntCaveSurfaceEntrance surfaceEntrance2 in AntCaveCatalog.SurfaceEntrances)
		{
			string text = "ant_cave_entrance_" + surfaceEntrance2.EntranceLetter.ToLowerInvariant();
			Add(result, "mainland_south", surfaceEntrance2.DestinationMapKey, AntCaveCatalog.DisplayFloorName(surfaceEntrance2.DestinationMapKey), text, surfaceEntrance2.ArrivalLandmarkId);
			Add(result, surfaceEntrance2.DestinationMapKey, "mainland_south", "亞丁大陸", surfaceEntrance2.SurfacePortalLandmarkId, text);
		}
		foreach (DragonValleyDungeonPortalLink portalLink2 in DragonValleyDungeonCatalog.PortalLinks)
		{
			Add(result, portalLink2.SourceMapKey, portalLink2.DestinationMapKey, DragonValleyDungeonCatalog.DisplayFloorName(portalLink2.DestinationMapKey), portalLink2.PortalLandmarkId, portalLink2.ArrivalLandmarkId);
		}
		foreach (DragonValleySurfaceEntrance surfaceEntrance3 in DragonValleyDungeonCatalog.SurfaceEntrances)
		{
			string text2 = $"dragon_valley_dungeon_entrance_{surfaceEntrance3.EntranceNumber}";
			Add(result, "mainland_south", surfaceEntrance3.DestinationMapKey, DragonValleyDungeonCatalog.DisplayFloorName(surfaceEntrance3.DestinationMapKey), text2, surfaceEntrance3.ArrivalLandmarkId);
			Add(result, surfaceEntrance3.DestinationMapKey, "mainland_south", "亞丁大陸", surfaceEntrance3.SurfaceExitLandmarkId, text2);
		}
		foreach (GiranDungeonPortalLink portalLink3 in GiranDungeonCatalog.PortalLinks)
		{
			Add(result, portalLink3.SourceMapKey, portalLink3.DestinationMapKey, GiranDungeonCatalog.DisplayFloorName(portalLink3.DestinationMapKey), portalLink3.PortalLandmarkId, portalLink3.ArrivalLandmarkId);
		}
		AddSurface(result, "giran_dungeon_entrance", GiranDungeonCatalog.SurfaceEntrance.DestinationMapKey, GiranDungeonCatalog.SurfaceEntrance.ArrivalLandmarkId, GiranDungeonCatalog.SurfaceEntrance.SurfaceExitLandmarkId, GiranDungeonCatalog.DisplayFloorName(GiranDungeonCatalog.SurfaceEntrance.DestinationMapKey));
		foreach (GludioDungeonPortalLink portalLink4 in GludioDungeonCatalog.PortalLinks)
		{
			Add(result, portalLink4.SourceMapKey, portalLink4.DestinationMapKey, GludioDungeonCatalog.DisplayFloorName(portalLink4.DestinationMapKey), portalLink4.PortalLandmarkId, portalLink4.ArrivalLandmarkId);
		}
		AddSurface(result, "gludio_dungeon_entrance", "zone_06", "gludio_dungeon_1f_surface_arrival", "gludio_dungeon_1f_surface_exit", GludioDungeonCatalog.DisplayFloorName("zone_06"));
		foreach (HeineDungeonPortalLink portalLink5 in HeineDungeonCatalog.PortalLinks)
		{
			Add(result, portalLink5.SourceMapKey, portalLink5.DestinationMapKey, HeineName(portalLink5.DestinationMapKey), portalLink5.PortalLandmarkId, portalLink5.ArrivalLandmarkId);
		}
		AddSurface(result, "heine_dungeon_entrance", HeineDungeonCatalog.SurfaceEntrance.DestinationMapKey, HeineDungeonCatalog.SurfaceEntrance.ArrivalLandmarkId, HeineDungeonCatalog.SurfaceEntrance.SurfaceExitLandmarkId, HeineName(HeineDungeonCatalog.SurfaceEntrance.DestinationMapKey));
		foreach (SilverKnightDungeonPortalLink portalLink6 in SilverKnightDungeonCatalog.PortalLinks)
		{
			Add(result, portalLink6.SourceMapKey, portalLink6.DestinationMapKey, SilverKnightDungeonCatalog.DisplayFloorName(portalLink6.DestinationMapKey), portalLink6.PortalLandmarkId, portalLink6.ArrivalLandmarkId);
		}
		foreach (SilverKnightSurfacePassage surfacePassage in SilverKnightDungeonCatalog.SurfacePassages)
		{
			AddSurface(result, surfacePassage.Id, surfacePassage.DungeonMapKey, surfacePassage.DungeonArrivalLandmarkId, surfacePassage.DungeonExitLandmarkId, SilverKnightDungeonCatalog.DisplayFloorName(surfacePassage.DungeonMapKey));
		}
		foreach (TowerPortalLink portalLink7 in TowerOfInsolenceCatalog.PortalLinks)
		{
			Add(result, portalLink7.SourceMapKey, portalLink7.DestinationMapKey, TowerName(portalLink7.DestinationMapKey), portalLink7.PortalLandmarkId, portalLink7.ArrivalLandmarkId);
		}
		AddSurface(result, "tower_of_insolence_entrance", TowerOfInsolenceCatalog.SurfaceEntrance.DestinationMapKey, TowerOfInsolenceCatalog.SurfaceEntrance.ArrivalLandmarkId, TowerOfInsolenceCatalog.SurfaceEntrance.SurfaceExitLandmarkId, TowerName(TowerOfInsolenceCatalog.SurfaceEntrance.DestinationMapKey));
		foreach (IvoryTowerPortalLink portalLink8 in IvoryTowerCatalog.PortalLinks)
		{
			Add(result, portalLink8.SourceMapKey, portalLink8.DestinationMapKey, IvoryTowerCatalog.DisplayNameFor(portalLink8.DestinationMapKey), portalLink8.PortalLandmarkId, portalLink8.ArrivalLandmarkId);
		}
		AdenSewerSurfaceEntrance surfaceEntrance = AdenSewerCatalog.SurfaceEntrance;
		AddSurface(result, "aden_sewer_entrance", surfaceEntrance.DestinationMapKey, surfaceEntrance.ArrivalLandmarkId, surfaceEntrance.SurfaceExitLandmarkId, "亞丁下水道");
		foreach (SleepingDragonCavePortalLink portalLink9 in SleepingDragonCaveCatalog.PortalLinks)
		{
			Add(result, portalLink9.SourceMapKey, portalLink9.DestinationMapKey, SleepingDragonCaveCatalog.DisplayName(portalLink9.DestinationMapKey), portalLink9.PortalLandmarkId, portalLink9.ArrivalLandmarkId);
		}
		foreach (OumDungeonPortalLink portalLink10 in OumDungeonCatalog.PortalLinks)
		{
			Add(result, portalLink10.SourceMapKey, portalLink10.DestinationMapKey, portalLink10.DestinationName, portalLink10.PortalLandmarkId, portalLink10.ArrivalLandmarkId);
		}
		foreach (DesireCavePortalLink portalLink11 in DesireCaveCatalog.PortalLinks)
		{
			Add(result, portalLink11.SourceMapKey, portalLink11.DestinationMapKey, string.Equals(portalLink11.DestinationMapKey, "mainland_south", StringComparison.Ordinal) ? "亞丁大陸" : DesireCaveCatalog.DisplayName(portalLink11.DestinationMapKey), portalLink11.PortalLandmarkId, portalLink11.ArrivalLandmarkId);
		}
		return result;
	}

	private static HashSet<string> BuildWalkOnlyMaps()
	{
		HashSet<string> maps = new HashSet<string>(StringComparer.Ordinal);
		AddMaps(AntCaveCatalog.Segments.Select((AntCaveSegment map) => map.MapKey));
		AddMaps(DragonValleyDungeonCatalog.Floors.Select((DragonValleyDungeonFloor map) => map.MapKey));
		AddMaps(GiranDungeonCatalog.Floors.Select((GiranDungeonFloor map) => map.MapKey));
		AddMaps(GludioDungeonCatalog.Floors.Select((GludioDungeonFloor map) => map.MapKey));
		AddMaps(HeineDungeonCatalog.Floors.Select((HeineDungeonFloor map) => map.MapKey).Append("fafurion_lair"));
		AddMaps(SilverKnightDungeonCatalog.Floors.Select((SilverKnightDungeonFloor map) => map.MapKey));
		AddMaps(TowerOfInsolenceCatalog.Floors.Select((TowerFloor map) => map.MapKey));
		AddMaps(IvoryTowerCatalog.Floors.Select((IvoryTowerFloorDefinition map) => map.MapKey));
		maps.Add("aden_sewer");
		maps.Add("oum_dungeon");
		AddMaps(new string[3] { "zone_15", "zone_16", "zone_17" });
		AddMaps(DesireCaveCatalog.Maps.Select((DesireCaveMap map) => map.MapKey));
		return maps;
		void AddMaps(IEnumerable<string> keys)
		{
			foreach (string key in keys)
			{
				if (!string.Equals(key, "mainland_south", StringComparison.Ordinal))
				{
					maps.Add(key);
				}
			}
		}
	}

	private static void AddSurface(Dictionary<string, List<MapLinks.Gate>> result, string surfaceLandmarkId, string dungeonMapKey, string dungeonArrivalLandmarkId, string dungeonExitLandmarkId, string dungeonName)
	{
		Add(result, "mainland_south", dungeonMapKey, dungeonName, surfaceLandmarkId, dungeonArrivalLandmarkId);
		Add(result, dungeonMapKey, "mainland_south", "亞丁大陸", dungeonExitLandmarkId, surfaceLandmarkId);
	}

	private static void Add(string sourceMapKey, string destinationMapKey, string destinationName, string portalLandmarkId, string arrivalLandmarkId)
	{
		Add(Links, sourceMapKey, destinationMapKey, destinationName, portalLandmarkId, arrivalLandmarkId);
	}

	private static void Add(Dictionary<string, List<MapLinks.Gate>> result, string sourceMapKey, string destinationMapKey, string destinationName, string? portalLandmarkId, string? arrivalLandmarkId, (int X, int Y)? sourceGameCell = null, (int X, int Y)? destinationGameCell = null)
	{
		RememberDisplayName(destinationMapKey, destinationName);
		if (!result.TryGetValue(sourceMapKey, out List<MapLinks.Gate> value))
		{
			value = (result[sourceMapKey] = new List<MapLinks.Gate>());
		}
		if (!value.Any(delegate(MapLinks.Gate existing)
		{
			if (existing.TargetKey == destinationMapKey && existing.SourceLandmarkId == portalLandmarkId && existing.DestinationLandmarkId == arrivalLandmarkId)
			{
				(int, int)? sourceGameCell2 = existing.SourceGameCell;
				(int, int)? tuple = sourceGameCell;
				bool hasValue = sourceGameCell2.HasValue;
				if (hasValue == tuple.HasValue)
				{
					(int, int) valueOrDefault2;
					(int, int) valueOrDefault;
					if (hasValue)
					{
						valueOrDefault = sourceGameCell2.GetValueOrDefault();
						valueOrDefault2 = tuple.GetValueOrDefault();
						if (valueOrDefault.Item1 != valueOrDefault2.Item1 || valueOrDefault.Item2 != valueOrDefault2.Item2)
						{
							goto IL_00f8;
						}
					}
					tuple = existing.DestinationGameCell;
					sourceGameCell2 = destinationGameCell;
					hasValue = tuple.HasValue;
					if (hasValue != sourceGameCell2.HasValue)
					{
						return false;
					}
					if (!hasValue)
					{
						return true;
					}
					valueOrDefault2 = tuple.GetValueOrDefault();
					valueOrDefault = sourceGameCell2.GetValueOrDefault();
					if (valueOrDefault2.Item1 == valueOrDefault.Item1)
					{
						return valueOrDefault2.Item2 == valueOrDefault.Item2;
					}
					return false;
				}
			}
			goto IL_00f8;
			IL_00f8:
			return false;
		}))
		{
			value.Add(new MapLinks.Gate(MapLinks.Edge.North, destinationMapKey, string.IsNullOrWhiteSpace(destinationName) ? destinationMapKey : destinationName, ToTown: false, portalLandmarkId, arrivalLandmarkId, 80f, sourceGameCell, destinationGameCell));
		}
	}

	private static string HeineName(string mapKey)
	{
		if (!(mapKey == "fafurion_lair"))
		{
			return HeineDungeonCatalog.DisplayFloorName(mapKey);
		}
		return "法利昂巢穴";
	}

	private static string TowerName(string mapKey)
	{
		return TowerOfInsolenceCatalog.Floors.FirstOrDefault((TowerFloor floor) => floor.MapKey == mapKey)?.DisplayName ?? mapKey;
	}
}
