using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IdleLineage.Data;

public static class IvoryTowerCatalog
{
	public const string SurfaceMapKey = "mainland_south";

	public const string SurfaceEntranceLandmarkId = "ivory_tower_entrance";

	public const string TownKey = "town_ivory_tower";

	public const string FirstFloorMapKey = "ivory_tower_1f";

	public const double HiddenMapChance = 0.1;

	public const string HiddenDarknessTintHex = "#351015";

	public const double HiddenDarknessStrength = 0.72;

	public static IReadOnlyList<IvoryTowerFloorDefinition> Floors { get; } = Array.AsReadOnly(new IvoryTowerFloorDefinition[8]
	{
		new IvoryTowerFloorDefinition("ivory_tower_1f", 75, 1, "象牙塔1樓", IsServiceFloor: true),
		new IvoryTowerFloorDefinition("ivory_tower_2f", 76, 2, "象牙塔2樓", IsServiceFloor: true),
		new IvoryTowerFloorDefinition("ivory_tower_3f", 77, 3, "象牙塔3樓", IsServiceFloor: true),
		new IvoryTowerFloorDefinition("zone_37", 78, 4, "象牙塔4樓", IsServiceFloor: false),
		new IvoryTowerFloorDefinition("zone_38", 79, 5, "象牙塔5樓", IsServiceFloor: false),
		new IvoryTowerFloorDefinition("zone_39", 80, 6, "象牙塔6樓", IsServiceFloor: false),
		new IvoryTowerFloorDefinition("zone_40", 81, 7, "象牙塔7樓", IsServiceFloor: false),
		new IvoryTowerFloorDefinition("zone_41", 82, 8, "象牙塔8樓", IsServiceFloor: false)
	});

	public static IReadOnlyList<IvoryTowerHiddenDefinition> HiddenMaps { get; } = Array.AsReadOnly(new IvoryTowerHiddenDefinition[5]
	{
		new IvoryTowerHiddenDefinition("zone_37", "hidden_lab_nolife", "無生物研究室", "zone_37", "ivory_tower_4f_hidden_arrival"),
		new IvoryTowerHiddenDefinition("zone_38", "hidden_lab_darkmagic", "黑魔法研究室", "zone_38", "ivory_tower_5f_hidden_arrival"),
		new IvoryTowerHiddenDefinition("zone_39", "hidden_seal_spirit", "惡靈封印室", "zone_39", "ivory_tower_6f_hidden_arrival"),
		new IvoryTowerHiddenDefinition("zone_40", "hidden_seal_monster", "魔物封印室", "zone_40", "ivory_tower_7f_hidden_arrival"),
		new IvoryTowerHiddenDefinition("zone_41", "hidden_seal_demon", "惡魔封印室", "zone_41", "ivory_tower_8f_hidden_arrival")
	});

	private static readonly IReadOnlyDictionary<string, IvoryTowerFloorDefinition> FloorsByMapKey = new ReadOnlyDictionary<string, IvoryTowerFloorDefinition>(Floors.ToDictionary<IvoryTowerFloorDefinition, string>((IvoryTowerFloorDefinition floor) => floor.MapKey, StringComparer.Ordinal));

	private static readonly IReadOnlyDictionary<string, IvoryTowerHiddenDefinition> HiddenByParent = new ReadOnlyDictionary<string, IvoryTowerHiddenDefinition>(HiddenMaps.ToDictionary<IvoryTowerHiddenDefinition, string>((IvoryTowerHiddenDefinition hidden) => hidden.ParentMapKey, StringComparer.Ordinal));

	private static readonly IReadOnlyDictionary<string, IvoryTowerHiddenDefinition> HiddenByMapKey = new ReadOnlyDictionary<string, IvoryTowerHiddenDefinition>(HiddenMaps.ToDictionary<IvoryTowerHiddenDefinition, string>((IvoryTowerHiddenDefinition hidden) => hidden.HiddenMapKey, StringComparer.Ordinal));

	public static IReadOnlyList<IvoryTowerPortalLink> PortalLinks { get; } = Array.AsReadOnly(new IvoryTowerPortalLink[16]
	{
		new IvoryTowerPortalLink("mainland_south", "ivory_tower_entrance", "ivory_tower_1f", "ivory_tower_1f_surface_arrival"),
		new IvoryTowerPortalLink("ivory_tower_1f", "ivory_tower_1f_surface_exit", "mainland_south", "ivory_tower_entrance"),
		LinkUp(1),
		LinkDown(2),
		LinkUp(2),
		LinkDown(3),
		LinkUp(3),
		LinkDown(4),
		LinkUp(4),
		LinkDown(5),
		LinkUp(5),
		LinkDown(6),
		LinkUp(6),
		LinkDown(7),
		LinkUp(7),
		LinkDown(8)
	});

	public static bool IsIvoryTowerMap(string? mapKey)
	{
		if (mapKey != null)
		{
			if (!FloorsByMapKey.ContainsKey(mapKey))
			{
				return HiddenByMapKey.ContainsKey(mapKey);
			}
			return true;
		}
		return false;
	}

	public static string TopologyMapKeyFor(string mapKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		if (!HiddenByMapKey.TryGetValue(mapKey, out IvoryTowerHiddenDefinition value))
		{
			return mapKey;
		}
		return value.TopologyMapKey;
	}

	public static string DisplayNameFor(string mapKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		if (HiddenByMapKey.TryGetValue(mapKey, out IvoryTowerHiddenDefinition value))
		{
			return value.DisplayName;
		}
		if (!FloorsByMapKey.TryGetValue(mapKey, out IvoryTowerFloorDefinition value2))
		{
			return mapKey;
		}
		return value2.DisplayName;
	}

	public static bool CanTriggerHiddenMap(string? currentMapKey, IvoryTowerTeleportSource source)
	{
		bool flag = currentMapKey != null && HiddenByParent.ContainsKey(currentMapKey);
		if (flag)
		{
			bool flag2 = (uint)(source - 1) <= 1u;
			flag = flag2;
		}
		return flag;
	}

	public static bool TryResolveHiddenMap(string? currentMapKey, IvoryTowerTeleportSource source, double randomSample, out string destinationMapKey, out string arrivalLandmarkId)
	{
		if (!double.IsFinite(randomSample) || randomSample < 0.0 || randomSample > 1.0)
		{
			throw new ArgumentOutOfRangeException("randomSample");
		}
		if (CanTriggerHiddenMap(currentMapKey, source) && randomSample < 0.1 && HiddenByParent.TryGetValue(currentMapKey, out IvoryTowerHiddenDefinition value))
		{
			destinationMapKey = value.HiddenMapKey;
			arrivalLandmarkId = value.ArrivalLandmarkId;
			return true;
		}
		destinationMapKey = string.Empty;
		arrivalLandmarkId = string.Empty;
		return false;
	}

	private static IvoryTowerPortalLink LinkUp(int sourceFloor)
	{
		IvoryTowerFloorDefinition ivoryTowerFloorDefinition = Floors[sourceFloor - 1];
		return new IvoryTowerPortalLink(DestinationMapKey: Floors[sourceFloor].MapKey, SourceMapKey: ivoryTowerFloorDefinition.MapKey, PortalLandmarkId: $"ivory_tower_{sourceFloor}f_stairs_up", ArrivalLandmarkId: $"ivory_tower_{sourceFloor + 1}f_arrival_from_{sourceFloor}f");
	}

	private static IvoryTowerPortalLink LinkDown(int sourceFloor)
	{
		IvoryTowerFloorDefinition ivoryTowerFloorDefinition = Floors[sourceFloor - 1];
		return new IvoryTowerPortalLink(DestinationMapKey: Floors[sourceFloor - 2].MapKey, SourceMapKey: ivoryTowerFloorDefinition.MapKey, PortalLandmarkId: $"ivory_tower_{sourceFloor}f_stairs_down", ArrivalLandmarkId: $"ivory_tower_{sourceFloor - 1}f_arrival_from_{sourceFloor}f");
	}
}
