using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleLineage.Data;

public sealed class MapPageStreamingSession
{
	private readonly MapTopology _map;

	private readonly Dictionary<MapPageCoordinate, MapPage> _pages;

	private readonly Dictionary<MapPageCoordinate, MapPage> _loaded = new Dictionary<MapPageCoordinate, MapPage>();

	public int PrefetchPageRadius { get; }

	public IReadOnlyList<MapPage> LoadedPages => SortPages(_loaded.Values);

	public MapPageStreamingSession(MapTopology map, int prefetchPageRadius = 1)
	{
		_map = map ?? throw new ArgumentNullException("map");
		if (prefetchPageRadius < 0)
		{
			throw new ArgumentOutOfRangeException("prefetchPageRadius");
		}
		PrefetchPageRadius = prefetchPageRadius;
		_pages = map.Pages.ToDictionary((MapPage page) => new MapPageCoordinate(page.X, page.Y));
		if (_pages.Count != map.PageColumns * map.PageRows)
		{
			throw new InvalidDataException("Map pages must contain one unique entry for every page coordinate.");
		}
	}

	public MapPageStreamingDelta Update(double cameraDisplayX, double cameraDisplayY, double viewportWidth, double viewportHeight)
	{
		ValidateFinite(cameraDisplayX, "cameraDisplayX");
		ValidateFinite(cameraDisplayY, "cameraDisplayY");
		ValidatePositive(viewportWidth, "viewportWidth");
		ValidatePositive(viewportHeight, "viewportHeight");
		double num = (double)_map.PageWidth * _map.DisplayScale;
		double num2 = (double)_map.PageHeight * _map.DisplayScale;
		double x = (double)_map.FullNativeWidth * _map.DisplayScale;
		double x2 = (double)_map.FullNativeHeight * _map.DisplayScale;
		double num3 = Math.Clamp(cameraDisplayX, 0.0, Math.BitDecrement(x));
		double num4 = Math.Clamp(cameraDisplayY, 0.0, Math.BitDecrement(x2));
		double num5 = Math.Clamp(num3 - viewportWidth * 0.5, 0.0, Math.BitDecrement(x));
		double num6 = Math.Clamp(num3 + viewportWidth * 0.5, 0.0, Math.BitDecrement(x));
		double num7 = Math.Clamp(num4 - viewportHeight * 0.5, 0.0, Math.BitDecrement(x2));
		double num8 = Math.Clamp(num4 + viewportHeight * 0.5, 0.0, Math.BitDecrement(x2));
		int num9 = Math.Clamp((int)Math.Floor(num5 / num), 0, _map.PageColumns - 1);
		int num10 = Math.Clamp((int)Math.Floor(num6 / num), 0, _map.PageColumns - 1);
		int num11 = Math.Clamp((int)Math.Floor(num7 / num2), 0, _map.PageRows - 1);
		int num12 = Math.Clamp((int)Math.Floor(num8 / num2), 0, _map.PageRows - 1);
		int num13 = Math.Max(0, num9 - PrefetchPageRadius);
		int num14 = Math.Min(_map.PageColumns - 1, num10 + PrefetchPageRadius);
		int num15 = Math.Max(0, num11 - PrefetchPageRadius);
		int num16 = Math.Min(_map.PageRows - 1, num12 + PrefetchPageRadius);
		Dictionary<MapPageCoordinate, MapPage> desired = new Dictionary<MapPageCoordinate, MapPage>();
		for (int i = num15; i <= num16; i++)
		{
			for (int j = num13; j <= num14; j++)
			{
				MapPageCoordinate key = new MapPageCoordinate(j, i);
				desired.Add(key, _pages[key]);
			}
		}
		MapPage[] toLoad = SortPages(from pair in desired
			where !_loaded.ContainsKey(pair.Key)
			select pair.Value);
		MapPage[] toKeep = SortPages(from pair in desired
			where _loaded.ContainsKey(pair.Key)
			select pair.Value);
		MapPage[] toUnload = SortPages(from pair in _loaded
			where !desired.ContainsKey(pair.Key)
			select pair.Value);
		_loaded.Clear();
		foreach (var (key2, value) in desired)
		{
			_loaded.Add(key2, value);
		}
		return new MapPageStreamingDelta(toLoad, toKeep, toUnload);
	}

	public IReadOnlyList<MapPage> Reset()
	{
		MapPage[] result = SortPages(_loaded.Values);
		_loaded.Clear();
		return result;
	}

	private static MapPage[] SortPages(IEnumerable<MapPage> pages)
	{
		return (from page in pages
			orderby page.Y, page.X
			select page).ToArray();
	}

	private static void ValidateFinite(double value, string parameterName)
	{
		if (!double.IsFinite(value))
		{
			throw new ArgumentOutOfRangeException(parameterName);
		}
	}

	private static void ValidatePositive(double value, string parameterName)
	{
		if (!double.IsFinite(value) || value <= 0.0)
		{
			throw new ArgumentOutOfRangeException(parameterName);
		}
	}
}
