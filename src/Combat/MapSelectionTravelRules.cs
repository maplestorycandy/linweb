using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MapSelectionTravelRules
{
	private sealed record MMenuPriceFloor(long ReturnScrollMaximumPurchasePrice, long MinimumTravelPrice);

	public const int ReturnScrollItemId = 40079;

	private const long MMenuPriceStepAdena = 100L;

	private static readonly ConditionalWeakTable<IGameData, MMenuPriceFloor> MMenuPriceFloors = new ConditionalWeakTable<IGameData, MMenuPriceFloor>();

	public static string PhysicalMapKey(MapDestination destination)
	{
		ArgumentNullException.ThrowIfNull(destination, "destination");
		if (destination.Kind != MapDestinationKind.Town)
		{
			return destination.MapKey;
		}
		return IntegratedTownCatalog.FindByTown(destination.Key)?.MapKey ?? destination.MapKey;
	}

	public static bool CanDepart(L1jMapRule sourceRule, bool samePhysicalMap)
	{
		ArgumentNullException.ThrowIfNull(sourceRule, "sourceRule");
		if (!samePhysicalMap)
		{
			return sourceRule.Escapable;
		}
		return sourceRule.Teleportable;
	}

	public static long ReturnScrollMaximumPurchasePriceAdena(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return MMenuPriceFloors.GetValue(data, BuildMMenuPriceFloor).ReturnScrollMaximumPurchasePrice;
	}

	private static MMenuPriceFloor BuildMMenuPriceFloor(IGameData data)
	{
		long num = 0L;
		foreach (L1jShopDefinition value in L1jShopCatalog.Shops(data).Values)
		{
			foreach (L1jShopItem item in value.Items)
			{
				if (item.L1jItemId == 40079 && item.SellPrice >= 0)
				{
					long num2 = L1jShopRules.BuyTotalPrice(data, value.NpcId, item, 1L);
					if (num2 > num)
					{
						num = num2;
					}
				}
			}
		}
		if (num <= 0)
		{
			throw new InvalidDataException($"main item {40079} has no positive shop purchase price.");
		}
		checked
		{
			long minimumTravelPrice = (unchecked(num / 100) + 1) * 100;
			return new MMenuPriceFloor(num, minimumTravelPrice);
		}
	}

	public static long MinimumMMenuTravelPriceAdena(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return MMenuPriceFloors.GetValue(data, BuildMMenuPriceFloor).MinimumTravelPrice;
	}

	public static long MMenuTravelPriceOf(IGameData data, L1jGetbackCatalog getback, MapDestination destination)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(getback, "getback");
		ArgumentNullException.ThrowIfNull(destination, "destination");
		long? travelPriceAdena = destination.TravelPriceAdena;
		long val;
		if (travelPriceAdena.HasValue)
		{
			long valueOrDefault = travelPriceAdena.GetValueOrDefault();
			val = Math.Max(0L, valueOrDefault);
		}
		else
		{
			string mapKey = PhysicalMapKey(destination);
			val = ((!L1jMapRuleCatalog.Load(data).TryForMapKey(mapKey, out L1jMapRule rule) || (object)rule == null) ? 0 : ((destination.Kind == MapDestinationKind.Town) ? TeleportPriceOfTown(data, rule.MapId, destination.Key, getback) : TeleportPriceOf(data, rule.MapId)));
		}
		return Math.Max(MinimumMMenuTravelPriceAdena(data), val);
	}

	public static bool IsCurrentDestination(MapDestination destination, string currentMapKey, bool insideIntegratedTown, string? currentIntegratedTownKey)
	{
		ArgumentNullException.ThrowIfNull(destination, "destination");
		ArgumentException.ThrowIfNullOrWhiteSpace(currentMapKey, "currentMapKey");
		if (destination.HasFixedLanding)
		{
			return false;
		}
		if (destination.Kind != MapDestinationKind.Town)
		{
			return string.Equals(destination.MapKey, currentMapKey, StringComparison.Ordinal);
		}
		string a = PhysicalMapKey(destination);
		if (insideIntegratedTown && string.Equals(a, currentMapKey, StringComparison.Ordinal))
		{
			return string.Equals(destination.Key, currentIntegratedTownKey, StringComparison.Ordinal);
		}
		return false;
	}

	public static MapEntryLanding? Resolve(IGameData data, MapTopology topology, int mapId, L1jGetbackCatalog? getback = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(topology, "topology");
		long price = TeleportPriceOf(data, mapId);
		if (TryTeleporterDestination(data, topology, mapId, out var localX, out var localY))
		{
			return Land(topology, localX, localY, MapEntryLandingSource.Teleporter, price);
		}
		if (TryGetbackDestination(getback, topology, mapId, out localX, out localY))
		{
			return Land(topology, localX, localY, MapEntryLandingSource.Getback, price);
		}
		if (TrySafeZoneCentroid(topology, out localX, out localY))
		{
			return Land(topology, localX, localY, MapEntryLandingSource.SafeZoneCentroid, price);
		}
		if (TryWalkableSearch(topology, out localX, out localY))
		{
			return Land(topology, localX, localY, MapEntryLandingSource.WalkableSearch, price);
		}
		return null;
	}

	public static long TeleportPriceOf(IGameData data, int mapId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		long num = -1L;
		foreach (NpcActionDefinition item in NpcActionCatalog.All(data))
		{
			foreach (NpcActionEffect effect in item.Effects)
			{
				if (string.Equals(effect.Kind, "teleport", StringComparison.Ordinal) && effect.MapId == mapId)
				{
					long num2 = effect.Price;
					if (num2 > 0 && (num < 0 || num2 < num))
					{
						num = num2;
					}
				}
			}
		}
		if (num >= 0)
		{
			return num;
		}
		return 0L;
	}

	public static long TeleportPriceOfTown(IGameData data, int mapId, string townKey, L1jGetbackCatalog getback)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(townKey, "townKey");
		ArgumentNullException.ThrowIfNull(getback, "getback");
		L1jTownGetbackLocation l1jTownGetbackLocation = getback.TownLocations.Values.FirstOrDefault((L1jTownGetbackLocation candidate) => candidate.MapId == mapId && string.Equals(candidate.TownKey, townKey, StringComparison.Ordinal));
		if ((object)l1jTownGetbackLocation == null || l1jTownGetbackLocation.Destinations.Count == 0)
		{
			return 0L;
		}
		long num = long.MaxValue;
		long num2 = long.MaxValue;
		foreach (NpcActionDefinition item in NpcActionCatalog.All(data))
		{
			foreach (NpcActionEffect effect in item.Effects)
			{
				if (string.Equals(effect.Kind, "teleport", StringComparison.Ordinal) && effect.MapId == mapId && effect.Price > 0)
				{
					long num3 = l1jTownGetbackLocation.Destinations.Min((L1jGetbackDestination destination) => (long)Math.Abs(destination.GameX - effect.X) + (long)Math.Abs(destination.GameY - effect.Y));
					if (num3 <= num && (num3 != num || effect.Price < num2))
					{
						num = num3;
						num2 = effect.Price;
					}
				}
			}
		}
		if (num > 192)
		{
			return 0L;
		}
		return num2;
	}

	private static bool TryTeleporterDestination(IGameData data, MapTopology topology, int mapId, out int localX, out int localY)
	{
		localX = 0;
		localY = 0;
		foreach (NpcActionDefinition item in NpcActionCatalog.All(data))
		{
			foreach (NpcActionEffect effect in item.Effects)
			{
				if (string.Equals(effect.Kind, "teleport", StringComparison.Ordinal) && effect.MapId == mapId && topology.ContainsGameCell(effect.X, effect.Y))
				{
					var (num, num2) = topology.ToLocalCell(effect.X, effect.Y);
					if (topology.IsWalkableCell(num, num2))
					{
						localX = num;
						localY = num2;
						return true;
					}
				}
			}
		}
		return false;
	}

	private static bool TryGetbackDestination(L1jGetbackCatalog? getback, MapTopology topology, int mapId, out int localX, out int localY)
	{
		localX = 0;
		localY = 0;
		if (getback == null)
		{
			return false;
		}
		foreach (L1jGetbackDestination item in getback.Restart.Select((L1jGetbackRestartRow row) => row.Destination).Concat(getback.Getback.SelectMany((L1jGetbackRow row) => row.Destinations)))
		{
			if (item.MapId == mapId && topology.ContainsGameCell(item.GameX, item.GameY))
			{
				var (num, num2) = topology.ToLocalCell(item.GameX, item.GameY);
				if (topology.IsWalkableCell(num, num2))
				{
					localX = num;
					localY = num2;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TrySafeZoneCentroid(MapTopology topology, out int localX, out int localY)
	{
		localX = 0;
		localY = 0;
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		for (int i = 0; i < topology.HeightCells; i++)
		{
			for (int j = 0; j < topology.WidthCells; j++)
			{
				if (topology.IsSafeCell(j, i))
				{
					num += j;
					num2 += i;
					num3++;
				}
			}
		}
		if (num3 == 0L)
		{
			return false;
		}
		return TryNearestWalkable(topology, (int)(num / num3), (int)(num2 / num3), out localX, out localY);
	}

	private static bool TryWalkableSearch(MapTopology topology, out int localX, out int localY)
	{
		return TryNearestWalkable(topology, topology.WidthCells / 2, topology.HeightCells / 2, out localX, out localY);
	}

	private static bool TryNearestWalkable(MapTopology topology, int originX, int originY, out int localX, out int localY)
	{
		localX = 0;
		localY = 0;
		if (topology.IsWalkableCell(originX, originY))
		{
			localX = originX;
			localY = originY;
			return true;
		}
		int num = Math.Max(topology.WidthCells, topology.HeightCells);
		for (int i = 1; i <= num; i++)
		{
			for (int j = -i; j <= i; j++)
			{
				for (int k = -i; k <= i; k++)
				{
					if (Math.Max(Math.Abs(k), Math.Abs(j)) == i)
					{
						int num2 = originX + k;
						int num3 = originY + j;
						if (topology.IsWalkableCell(num2, num3))
						{
							localX = num2;
							localY = num3;
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	private static MapEntryLanding Land(MapTopology topology, int localX, int localY, MapEntryLandingSource source, long price)
	{
		return new MapEntryLanding(topology.MapKey, localX, localY, source, price);
	}
}
