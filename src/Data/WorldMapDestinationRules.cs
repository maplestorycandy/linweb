using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class WorldMapDestinationRules
{
	public const string WitonRegionKey = "witon";

	public const string FireDragonCaveDestinationKey = "mainland_south_fire_dragon_cave";

	public const string FireDragonCaveMapKey = "mainland_south";

	public const int FireDragonCaveLandingX = 33742;

	public const int FireDragonCaveLandingY = 32396;

	public const long FireDragonCaveTravelPriceAdena = 663L;

	public const string GilenHouseDestinationKey = "talking_island_gilen_house";

	public const string GilenHouseMapKey = "talking_island";

	public const int GilenHouseLandingX = 32562;

	public const int GilenHouseLandingY = 33082;

	private static readonly (string RegionKey, string TownKey, string Name)[] CastleRegionTowns = new(string, string, string)[2]
	{
		("kent", "town_kent_castle", "肯特村莊"),
		("windwood", "town_windwood_castle", "風木村莊")
	};

	private static readonly Dictionary<string, string> RenamedDestinations = new Dictionary<string, string>(StringComparer.Ordinal) { ["shadow_temple"] = "暗影神殿外圍" };

	private const string CharacterStartRegionKey = "talkingisland";

	private const string CharacterStartMapKey = "l1j_map_2005";

	private const string CharacterStartName = "隱藏之谷";

	public static string DisplayName(string key, string tableName)
	{
		if (!RenamedDestinations.TryGetValue(key, out string value))
		{
			return tableName;
		}
		return value;
	}

	public static IReadOnlyList<MapDestination> Effective(MapRegionDefinition region)
	{
		ArgumentNullException.ThrowIfNull(region, "region");
		List<MapDestination> list = region.Destinations.ToList();
		if (string.Equals(region.Key, "talkingisland", StringComparison.Ordinal) && !list.Any((MapDestination destination) => string.Equals(destination.Key, "talking_island_gilen_house", StringComparison.Ordinal)))
		{
			int num = list.FindIndex((MapDestination destination) => string.Equals(destination.Key, "talking_island", StringComparison.Ordinal));
			int num2 = ((num < 0) ? list.Count : num);
			list.Insert(num2, new MapDestination(region.Key, region.Name, region.RegionIndex, num2, "talking_island_gilen_house", "吉倫之屋", "village", "村莊", null, MapDestinationKind.Hunt, ClassicHide: false, null, null, null, null, null, "talking_island", 32562, 33082));
		}
		if (string.Equals(region.Key, "talkingisland", StringComparison.Ordinal) && !list.Any((MapDestination destination) => string.Equals(destination.Key, "l1j_map_2005", StringComparison.Ordinal)))
		{
			list.Add(new MapDestination(region.Key, region.Name, region.RegionIndex, list.Count, "l1j_map_2005", "隱藏之谷", "field", "野外", null, MapDestinationKind.Hunt, ClassicHide: false, null, null, null, null, null));
		}
		(string, string, string)[] castleRegionTowns = CastleRegionTowns;
		for (int num3 = 0; num3 < castleRegionTowns.Length; num3++)
		{
			var (a, townKey, name) = castleRegionTowns[num3];
			if (string.Equals(a, region.Key, StringComparison.Ordinal) && !list.Any((MapDestination d) => string.Equals(d.Key, townKey, StringComparison.Ordinal)))
			{
				int num4 = Math.Clamp(region.CastleInsertIndex.GetValueOrDefault(), 0, list.Count);
				list.Insert(num4, new MapDestination(region.Key, region.Name, region.RegionIndex, num4, townKey, name, "town", "村莊", null, MapDestinationKind.Town, ClassicHide: false, null, null, null, null, null));
			}
		}
		if (string.Equals(region.Key, "witon", StringComparison.Ordinal) && !list.Any((MapDestination destination) => string.Equals(destination.Key, "mainland_south_fire_dragon_cave", StringComparison.Ordinal)))
		{
			list.Add(new MapDestination(region.Key, region.Name, region.RegionIndex, list.Count, "mainland_south_fire_dragon_cave", "火龍窟", "wild", "野外", null, MapDestinationKind.Hunt, ClassicHide: false, null, null, null, null, null, "mainland_south", 33742, 32396, 663L));
		}
		return list;
	}
}
