using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Combat;

namespace IdleLineage.Data;

public static class TowerOfInsolenceCatalog
{
	public const int FirstFloor = 1;

	public const int LastFloor = 100;

	public static IReadOnlyList<TowerFloor> Floors { get; } = (from floor in Enumerable.Range(1, 100)
		select new TowerFloor(floor, $"pride_f{floor}", 100 + floor, $"傲慢之塔{floor}F")).ToArray();

	private static readonly Dictionary<string, TowerFloor> FloorsByMapKey = Floors.ToDictionary<TowerFloor, string>((TowerFloor floor) => floor.MapKey, StringComparer.Ordinal);

	public const int FloorsPerSegment = 10;

	public static TowerSurfaceEntrance SurfaceEntrance { get; } = new TowerSurfaceEntrance(34260, 33140, 34259, 33140, "pride_f1", "tower_1f_surface_arrival", "tower_1f_surface_exit");

	public static IReadOnlyList<TowerPortalLink> PortalLinks { get; } = (from floor in Enumerable.Range(1, 99)
		where !IsSegmentTop(floor)
		select floor).SelectMany((int floor) => new TowerPortalLink[2]
	{
		new TowerPortalLink($"pride_f{floor}", $"pride_f{floor}_stairs_up", $"pride_f{floor + 1}", $"pride_f{floor + 1}_arrival_from_below"),
		new TowerPortalLink($"pride_f{floor + 1}", $"pride_f{floor + 1}_stairs_down", $"pride_f{floor}", $"pride_f{floor}_arrival_from_above")
	}).ToArray();

	public static bool IsSegmentTop(int floorNumber)
	{
		return floorNumber % 10 == 0;
	}

	public static bool IsSegmentEntrance(int floorNumber)
	{
		return floorNumber % 10 == 1;
	}

	public static string MapKey(int floorNumber)
	{
		if (floorNumber < 1 || floorNumber > 100)
		{
			return string.Empty;
		}
		return $"pride_f{floorNumber}";
	}

	public static int FloorNumber(string mapKey)
	{
		if (!FloorsByMapKey.TryGetValue(mapKey ?? string.Empty, out TowerFloor value))
		{
			return 0;
		}
		return value.FloorNumber;
	}

	public static bool IsTowerFloor(string? mapKey)
	{
		if (!string.IsNullOrEmpty(mapKey))
		{
			return FloorsByMapKey.ContainsKey(mapKey);
		}
		return false;
	}

	public static bool RecordsReturnPosition(string? mapKey)
	{
		return !IsTowerFloor(mapKey);
	}

	public static bool TryNormalizeTravelTier(int tier, out int floorNumber)
	{
		bool flag;
		switch (tier)
		{
		case 1:
		case 11:
		case 21:
		case 31:
		case 41:
		case 51:
		case 61:
		case 71:
		case 81:
		case 91:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		floorNumber = (flag ? tier : 0);
		return floorNumber != 0;
	}

	public static bool TryResolveTravelItem(IGameData data, string itemKey, out TowerTravelItem travel)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		travel = default(TowerTravelItem);
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return false;
		}
		int num = ReadInt(jsonObject, "l1jItemId");
		int num2 = ((num >= 40104 && num <= 40112) ? (11 + (num - 40104) * 10) : ((num == 40113) ? 100 : 0));
		if (num2 > 0)
		{
			travel = new TowerTravelItem(TowerTravelItemKind.TeleportScroll, num2, MapKey(num2), ScrollArrivalLandmarkId(num2));
			return true;
		}
		if (!TryNormalizeTravelTier(ReadInt(jsonObject, "prideTier"), out var floorNumber))
		{
			return false;
		}
		if (ReadString(jsonObject, "prideKind") != "pass")
		{
			return false;
		}
		travel = new TowerTravelItem(TowerTravelItemKind.TeleportTalisman, floorNumber, MapKey(floorNumber), ScrollArrivalLandmarkId(floorNumber));
		return true;
	}

	public static string ScrollArrivalLandmarkId(int floorNumber)
	{
		if (floorNumber != 1)
		{
			if (IsSegmentEntrance(floorNumber))
			{
				return $"pride_f{floorNumber}_arrival_from_above";
			}
			return $"pride_f{floorNumber}_arrival_from_below";
		}
		return "tower_1f_surface_arrival";
	}

	public static bool AllowsRandomTeleport(IGameData data, Combatant player, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(player, "player");
		return FloorNumber(mapKey) == 0;
	}

	private static int ReadInt(JsonObject source, string key)
	{
		if (!(source[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}

	private static string ReadString(JsonObject source, string key)
	{
		if (!(source[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}
}
