using System;
using System.Collections.Generic;

namespace IdleLineage.Data;

public static class WorldAtlasCatalog
{
	private static readonly IReadOnlyDictionary<string, string> AdenHuntBgmRegionByPlace = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["古魯丁村"] = "gludio",
		["史萊姆競技場"] = "gludio",
		["葡萄園"] = "gludio",
		["邪惡神殿"] = "gludio",
		["遠古戰場"] = "gludio",
		["海音村"] = "heine",
		["海音城"] = "heine",
		["鏡子森林"] = "heine",
		["龍之谷"] = "dragon_valley",
		["火龍窟"] = "fire_dragon",
		["威頓村"] = "fire_dragon",
		["歐瑞村"] = "oren",
		["冰鏡湖"] = "oren",
		["冰原"] = "oren",
		["象牙塔"] = "oren",
		["國境要塞"] = "oren"
	};

	private const string TalkingIslandMapKey = "talking_island";

	private const string AdenContinentMapKey = "mainland_south";

	private const int TalkingIslandRegionRadiusCells = 100;

	private const int TalkingIslandFacilityRadiusCells = 50;

	private static readonly WorldAtlasPlaceAnchor[] TalkingIslandAnchors = new WorldAtlasPlaceAnchor[4]
	{
		new WorldAtlasPlaceAnchor("說話之島村莊", 32576, 32945, EntranceOnly: false, 100),
		new WorldAtlasPlaceAnchor("說話之島港口", 32652, 32984, EntranceOnly: false, 100),
		new WorldAtlasPlaceAnchor("吉倫之屋", 32562, 33082, EntranceOnly: false, 50),
		new WorldAtlasPlaceAnchor("冒險洞穴", 32492, 32852, EntranceOnly: true)
	};

	private static readonly WorldAtlasPlaceAnchor[] AdenContinentAnchors = new WorldAtlasPlaceAnchor[42]
	{
		new WorldAtlasPlaceAnchor("黑暗神殿", 32718, 32317),
		new WorldAtlasPlaceAnchor("燃柳村", 32745, 32443),
		new WorldAtlasPlaceAnchor("妖魔城", 32940, 32281),
		new WorldAtlasPlaceAnchor("眠龍洞穴", 32938, 32284, EntranceOnly: true),
		new WorldAtlasPlaceAnchor("正義神殿", 33138, 32237),
		new WorldAtlasPlaceAnchor("妖精森林", 33078, 32334),
		new WorldAtlasPlaceAnchor("寵物商人", 32886, 32438),
		new WorldAtlasPlaceAnchor("史萊姆競技場", 32640, 32674),
		new WorldAtlasPlaceAnchor("古魯丁村", 32629, 32808),
		new WorldAtlasPlaceAnchor("地下監獄", 32728, 32929, EntranceOnly: true),
		new WorldAtlasPlaceAnchor("葡萄園", 32795, 32829),
		new WorldAtlasPlaceAnchor("邪惡神殿", 32932, 32652),
		new WorldAtlasPlaceAnchor("遠古戰場", 32796, 32710),
		new WorldAtlasPlaceAnchor("風木村", 32610, 33185),
		new WorldAtlasPlaceAnchor("風木城", 32516, 33494),
		new WorldAtlasPlaceAnchor("沙漠", 32666, 33298),
		new WorldAtlasPlaceAnchor("綠洲", 32865, 33251),
		new WorldAtlasPlaceAnchor("肯特村", 33072, 32798),
		new WorldAtlasPlaceAnchor("肯特城", 33125, 32769),
		new WorldAtlasPlaceAnchor("奇岩村", 33421, 32813),
		new WorldAtlasPlaceAnchor("競技場", 33576, 32749),
		new WorldAtlasPlaceAnchor("奇岩城", 33695, 32733),
		new WorldAtlasPlaceAnchor("龍之谷", 33466, 32383),
		new WorldAtlasPlaceAnchor("火龍窟", 33710, 32407),
		new WorldAtlasPlaceAnchor("威頓村", 33724, 32490),
		new WorldAtlasPlaceAnchor("銀騎士村", 33080, 33395),
		new WorldAtlasPlaceAnchor("鏡子森林", 33766, 33236),
		new WorldAtlasPlaceAnchor("海音村", 33602, 33235),
		new WorldAtlasPlaceAnchor("海音城", 33528, 33312),
		new WorldAtlasPlaceAnchor("黎明森林", 34214, 32792),
		new WorldAtlasPlaceAnchor("古普賽村", 34007, 32842),
		new WorldAtlasPlaceAnchor("風龍的傷痕", 34030, 32960),
		new WorldAtlasPlaceAnchor("黃昏山脈", 34289, 32977),
		new WorldAtlasPlaceAnchor("亞丁城鎮", 33963, 33243),
		new WorldAtlasPlaceAnchor("亞丁城", 34111, 33051),
		new WorldAtlasPlaceAnchor("傲慢之塔", 34260, 33140),
		new WorldAtlasPlaceAnchor("歐瑞村", 34055, 32287),
		new WorldAtlasPlaceAnchor("冰鏡湖", 33956, 32357),
		new WorldAtlasPlaceAnchor("冰原", 34035, 32435),
		new WorldAtlasPlaceAnchor("象牙塔", 34041, 32155),
		new WorldAtlasPlaceAnchor("水晶洞穴", 34173, 32178, EntranceOnly: true),
		new WorldAtlasPlaceAnchor("國境要塞", 34303, 32261)
	};

	private static readonly IReadOnlyDictionary<string, IReadOnlyList<WorldAtlasPlaceAnchor>> AnchorsByMap = new Dictionary<string, IReadOnlyList<WorldAtlasPlaceAnchor>>(StringComparer.Ordinal)
	{
		["talking_island"] = Array.AsReadOnly(TalkingIslandAnchors),
		["mainland_south"] = Array.AsReadOnly(AdenContinentAnchors)
	};

	private static readonly IReadOnlyDictionary<string, string> TitleByMap = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["talking_island"] = "說話之島地圖",
		["mainland_south"] = "亞丁大陸地圖"
	};

	private static readonly double IsoPixelsPerCell = Math.Sqrt(720.0);

	public static bool TryGetTitle(string mapKey, out string? title)
	{
		return TitleByMap.TryGetValue(mapKey, out title);
	}

	public static bool TryGetAnchors(string mapKey, out IReadOnlyList<WorldAtlasPlaceAnchor>? anchors)
	{
		return AnchorsByMap.TryGetValue(mapKey, out anchors);
	}

	public static bool TryResolvePlaceName(string mapKey, int gameX, int gameY, out string placeName)
	{
		placeName = string.Empty;
		if (!AnchorsByMap.TryGetValue(mapKey, out IReadOnlyList<WorldAtlasPlaceAnchor> value))
		{
			return false;
		}
		double num = double.PositiveInfinity;
		foreach (WorldAtlasPlaceAnchor item in value)
		{
			if (item.EntranceOnly)
			{
				continue;
			}
			double num2 = 24.0 * (double)(gameX + gameY - (item.GameX + item.GameY));
			double num3 = 12.0 * (double)(gameY - gameX - (item.GameY - item.GameX));
			double num4 = num2 * num2 + num3 * num3;
			if (item.RadiusCells > 0)
			{
				double num5 = (double)item.RadiusCells * IsoPixelsPerCell;
				if (num4 > num5 * num5)
				{
					continue;
				}
			}
			if (!(num4 >= num))
			{
				num = num4;
				placeName = item.Name;
			}
		}
		return placeName.Length > 0;
	}

	public static string? ResolveHuntBgmRegionKey(string mapKey, int gameX, int gameY)
	{
		if (!string.Equals(mapKey, "mainland_south", StringComparison.Ordinal) || !TryResolvePlaceName(mapKey, gameX, gameY, out string placeName))
		{
			return null;
		}
		if (!AdenHuntBgmRegionByPlace.TryGetValue(placeName, out string value))
		{
			return null;
		}
		return value;
	}

	public static WorldAtlasDefinition CreateLocalDefinition(MapTopology topology, string mapKey, string title)
	{
		ArgumentNullException.ThrowIfNull(topology, "topology");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		ArgumentException.ThrowIfNullOrWhiteSpace(title, "title");
		double num = (double)topology.PreviewWidth / (double)topology.FullNativeWidth;
		double num2 = (double)topology.PreviewHeight / (double)topology.FullNativeHeight;
		double num3 = (double)(topology.ChunkColumns - 1) * 1536.0 / 2.0 + 768.0;
		double num4 = 24.0 * num;
		double num5 = 24.0 * num;
		double num6 = -12.0 * num2;
		double num7 = 12.0 * num2;
		double effectiveOriginX = (double)topology.GameOriginX - (double)topology.CellOriginX;
		double effectiveOriginY = (double)topology.GameOriginY - (double)topology.CellOriginY;
		double num8 = num * 24.0 * (1.0 - effectiveOriginX - effectiveOriginY);
		double num9 = num2 * (num3 + 12.0 * effectiveOriginX - 12.0 * effectiveOriginY);
		string text;
		if (!title.EndsWith("地圖", StringComparison.Ordinal))
		{
			text = title;
		}
		else
		{
			text = title.Substring(0, title.Length - 2);
		}
		string name = text;
		string title2 = (title.EndsWith("地圖", StringComparison.Ordinal) ? title : (title + "地圖"));
		IReadOnlyList<WorldAtlasPlace> places;
		if (AnchorsByMap.TryGetValue(mapKey, out IReadOnlyList<WorldAtlasPlaceAnchor> value))
		{
			WorldAtlasPlace[] array = new WorldAtlasPlace[value.Count];
			for (int i = 0; i < value.Count; i++)
			{
				WorldAtlasPlaceAnchor worldAtlasPlaceAnchor = value[i];
				array[i] = new WorldAtlasPlace(worldAtlasPlaceAnchor.Name, num4 * (double)worldAtlasPlaceAnchor.GameX + num5 * (double)worldAtlasPlaceAnchor.GameY + num8, num6 * (double)worldAtlasPlaceAnchor.GameX + num7 * (double)worldAtlasPlaceAnchor.GameY + num9, worldAtlasPlaceAnchor.EntranceOnly);
			}
			places = Array.AsReadOnly(array);
		}
		else
		{
			places = Array.AsReadOnly(new WorldAtlasPlace[1]
			{
				new WorldAtlasPlace(name, (double)topology.PreviewWidth * 0.5, (double)topology.PreviewHeight * 0.5)
			});
		}
		return new WorldAtlasDefinition(mapKey, title2, "res://assets/maps/" + topology.MapKey + "/" + topology.PreviewFile, topology.PreviewWidth, topology.PreviewHeight, num4, num5, num8, num6, num7, num9, places);
	}

	public static bool TryLocate(WorldAtlasDefinition definition, int gameX, int gameY, out WorldAtlasLocation location)
	{
		ArgumentNullException.ThrowIfNull(definition, "definition");
		double value = definition.BaseXFromGameX * (double)gameX + definition.BaseXFromGameY * (double)gameY + definition.BaseXOffset;
		double value2 = definition.BaseYFromGameX * (double)gameX + definition.BaseYFromGameY * (double)gameY + definition.BaseYOffset;
		value = Math.Clamp(value, 6.0, definition.PixelWidth - 6);
		value2 = Math.Clamp(value2, 6.0, definition.PixelHeight - 6);
		if (!TryResolvePlaceName(definition.MapKey, gameX, gameY, out string placeName))
		{
			placeName = string.Empty;
			double num = double.PositiveInfinity;
			foreach (WorldAtlasPlace place in definition.Places)
			{
				if (!place.EntranceOnly)
				{
					double num2 = value - place.PixelX;
					double num3 = value2 - place.PixelY;
					double num4 = num2 * num2 + num3 * num3;
					if (!(num4 >= num))
					{
						placeName = place.Name;
						num = num4;
					}
				}
			}
		}
		location = new WorldAtlasLocation(value, value2, placeName);
		return true;
	}
}
