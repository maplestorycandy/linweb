using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IdleLineage.Data;

public sealed class MapTopology
{
	private readonly byte[] _directions;

	private readonly byte[]? _illegal;

	private readonly byte[] _zones;

	private readonly byte[] _spawnRegionIds;

	private readonly uint[] _occlusionCellIndices;

	private readonly ushort[] _occlusionCellGroups;

	private bool? _hasSafeZone;

	public string MapKey { get; }

	public int GameOriginX { get; }

	public int GameOriginY { get; }

	public int WidthCells { get; }

	public int HeightCells { get; }

	public int ChunkColumns { get; }

	public int ChunkRows { get; }

	public int PageWidth { get; }

	public int PageHeight { get; }

	public int PageColumns { get; }

	public int PageRows { get; }

	public int FullNativeWidth { get; }

	public int FullNativeHeight { get; }

	public double DisplayScale { get; }

	public string PreviewFile { get; }

	public int PreviewWidth { get; }

	public int PreviewHeight { get; }

	public MapSpawnSettings SpawnSettings { get; }

	public IReadOnlyList<MapPage> Pages { get; }

	public IReadOnlyList<MapLandmark> Landmarks { get; }

	public IReadOnlyList<MapOcclusionGroup> OcclusionGroups { get; }

	public bool HasOcclusion => _occlusionCellIndices.Length != 0;

	public bool HasSafeZone
	{
		get
		{
			bool valueOrDefault = _hasSafeZone == true;
			if (!_hasSafeZone.HasValue)
			{
				valueOrDefault = Array.IndexOf(_zones, (byte)1) >= 0;
				_hasSafeZone = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
	}

	public IReadOnlyList<string> SpawnRegionKeys { get; }

	public bool HasSpawnRegions => SpawnRegionKeys.Count > 1;

	public bool HasLegalMask => _illegal != null;

	public int CellOriginX { get; }

	public int CellOriginY { get; }

	public int? SpawnTriggerRange { get; }

	public bool IsCustom { get; }

	private MapTopology(string mapKey, int gameOriginX, int gameOriginY, int cellOriginX, int cellOriginY, int widthCells, int heightCells, int chunkColumns, int chunkRows, int pageWidth, int pageHeight, int pageColumns, int pageRows, int fullNativeWidth, int fullNativeHeight, double displayScale, string previewFile, int previewWidth, int previewHeight, MapSpawnSettings spawnSettings, byte[] directions, byte[]? illegal, byte[] zones, byte[] spawnRegionIds, IReadOnlyList<string> spawnRegionKeys, IReadOnlyList<MapPage> pages, IReadOnlyList<MapLandmark> landmarks, uint[] occlusionCellIndices, ushort[] occlusionCellGroups, IReadOnlyList<MapOcclusionGroup> occlusionGroups, int? spawnTriggerRange = null, bool isCustom = false)
	{
		MapKey = mapKey;
		GameOriginX = gameOriginX;
		GameOriginY = gameOriginY;
		CellOriginX = cellOriginX;
		CellOriginY = cellOriginY;
		WidthCells = widthCells;
		HeightCells = heightCells;
		ChunkColumns = chunkColumns;
		ChunkRows = chunkRows;
		PageWidth = pageWidth;
		PageHeight = pageHeight;
		PageColumns = pageColumns;
		PageRows = pageRows;
		FullNativeWidth = fullNativeWidth;
		FullNativeHeight = fullNativeHeight;
		DisplayScale = displayScale;
		PreviewFile = previewFile;
		PreviewWidth = previewWidth;
		PreviewHeight = previewHeight;
		SpawnSettings = spawnSettings;
		SpawnTriggerRange = spawnTriggerRange;
		IsCustom = isCustom;
		_directions = directions;
		_illegal = illegal;
		_zones = zones;
		_spawnRegionIds = spawnRegionIds;
		SpawnRegionKeys = spawnRegionKeys;
		Pages = pages;
		Landmarks = landmarks;
		_occlusionCellIndices = occlusionCellIndices;
		_occlusionCellGroups = occlusionCellGroups;
		OcclusionGroups = occlusionGroups;
	}

	public static MapTopology Load(string directory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory, "directory");
		using JsonDocument jsonDocument = JsonDocument.Parse(DataFileSystem.ReadAllText(DataFileSystem.Combine(directory, "map.json")));
		JsonElement rootElement = jsonDocument.RootElement;
		JsonElement parent = RequireObject(rootElement, "source");
		JsonElement parent2 = RequireObject(rootElement, "projection");
		JsonElement parent3 = RequireObject(rootElement, "paging");
		JsonElement parent4 = RequireObject(rootElement, "preview");
		string tilesFile = "terrain-tiles.bin";
		string zonesFile = "terrain-zones.bin";
		if (rootElement.TryGetProperty("terrain", out var terrElem) && terrElem.ValueKind == JsonValueKind.Object)
		{
			if (terrElem.TryGetProperty("tiles", out var tElem) && tElem.ValueKind == JsonValueKind.Object && tElem.TryGetProperty("file", out var tfElem) && tfElem.ValueKind == JsonValueKind.String)
			{
				tilesFile = tfElem.GetString() ?? tilesFile;
			}
			if (terrElem.TryGetProperty("zones", out var zElem) && zElem.ValueKind == JsonValueKind.Object && zElem.TryGetProperty("file", out var zfElem) && zfElem.ValueKind == JsonValueKind.String)
			{
				zonesFile = zfElem.GetString() ?? zonesFile;
			}
		}
		(int X, int Y) tuple = ReadPair(parent, "chunkGrid");
		int item = tuple.X;
		int item2 = tuple.Y;
		(int X, int Y) tuple2 = ReadPair(parent, "gridCells");
		int item3 = tuple2.X;
		int item4 = tuple2.Y;
		(int X, int Y) tuple3 = ReadPair(parent, "gameOrigin");
		int item5 = tuple3.X;
		int item6 = tuple3.Y;
		int cellOriginX = 0;
		int cellOriginY = 0;
		if (parent.TryGetProperty("cellOrigin", out var cellOriginElem) && cellOriginElem.ValueKind == JsonValueKind.Array)
		{
			var (coX, coY) = ReadPair(parent, "cellOrigin");
			cellOriginX = coX;
			cellOriginY = coY;
		}
		(int X, int Y) tuple4 = ReadPair(parent3, "pagePixels");
		int item7 = tuple4.X;
		int item8 = tuple4.Y;
		(int X, int Y) tuple5 = ReadPair(parent3, "fullNativePixels");
		int item9 = tuple5.X;
		int item10 = tuple5.Y;
		(int X, int Y) tuple6 = ReadPair(parent4, "size");
		int item11 = tuple6.X;
		int item12 = tuple6.Y;
		string previewFile = RequireString(parent4, "file");
		int num = RequireInt(parent3, "columns");
		int num2 = RequireInt(parent3, "rows");
		double displayScale = RequireDouble(parent2, "displayScale");
		MapSpawnSettings spawnSettings = MapSpawnSettings.Default;
		byte[] directions = TryReadOptionalGrid(directory, tilesFile, item3, item4) ?? new byte[checked(item3 * item4)];
		if (directions.All(b => b == 0))
		{
			Array.Fill(directions, (byte)15);
		}
		byte[] array = TryReadOptionalGrid(directory, zonesFile, item3, item4) ?? new byte[checked(item3 * item4)];
		byte[] illegal = TryReadOptionalGrid(directory, "terrain-water.bin", item3, item4);
		string adminCollisionFile = DataFileSystem.Combine(directory, "admin-collision.json");
		if (DataFileSystem.Exists(adminCollisionFile))
		{
			ApplyAdminCollisionOverride(adminCollisionFile, directions, item3, item4);
		}
		if (array.Any((byte b) => b > 2))
		{
			throw new InvalidDataException("Terrain zone grid contains an unknown zone value.");
		}
		(byte[] Ids, IReadOnlyList<string> Keys) tuple7 = ReadSpawnRegions(rootElement, directory, item3, item4);
		byte[] item13 = tuple7.Ids;
		IReadOnlyList<string> item14 = tuple7.Keys;
		List<MapPage> list = new List<MapPage>();
		foreach (JsonElement item17 in RequireArray(rootElement, "pages").EnumerateArray())
		{
			(int X, int Y) tuple8 = ReadPair(item17, "pixelOrigin");
			int item15 = tuple8.X;
			int item16 = tuple8.Y;
			string foreground = null;
			string foregroundMask = null;
			IReadOnlyList<int> foregroundGroups = null;
			if (item17.TryGetProperty("foreground", out var value) && value.ValueKind == JsonValueKind.String)
			{
				foreground = value.GetString();
				if (item17.TryGetProperty("foregroundMask", out var value2) && value2.ValueKind == JsonValueKind.String && item17.TryGetProperty("foregroundGroups", out var value3) && value3.ValueKind == JsonValueKind.Array)
				{
					foregroundMask = value2.GetString();
					List<int> list2 = new List<int>();
					foreach (JsonElement item18 in value3.EnumerateArray())
					{
						list2.Add(item18.GetInt32());
					}
					foregroundGroups = list2;
				}
			}
			list.Add(new MapPage(RequireInt(item17, "x"), RequireInt(item17, "y"), RequireString(item17, "file"), item15, item16, foreground, foregroundMask, foregroundGroups));
		}
		if (list.Count != num * num2)
		{
			throw new InvalidDataException($"Map page count is {list.Count}; expected {num * num2}.");
		}
		IReadOnlyList<MapLandmark> landmarks = Array.Empty<MapLandmark>();
		if (rootElement.TryGetProperty("collisionOverrides", out var collElem) && collElem.ValueKind == JsonValueKind.String)
		{
			string relPath = collElem.GetString() ?? "";
			if (!string.IsNullOrEmpty(relPath))
			{
				string fullCollPath = DataFileSystem.Combine(directory, relPath);
				if (DataFileSystem.Exists(fullCollPath))
				{
					landmarks = LoadLandmarks(fullCollPath);
				}
			}
		}
		var (occlusionCellIndices, occlusionCellGroups, occlusionGroups) = LoadOcclusion(rootElement, directory, item3, item4);
		int? spawnTriggerRange = null;
		if (rootElement.TryGetProperty("spawnTriggerRange", out var strElem) && strElem.TryGetInt32(out var strVal))
		{
			spawnTriggerRange = Math.Max(0, strVal);
		}
		bool isCustom = false;
		if (rootElement.TryGetProperty("isCustom", out var isCustomElem) && isCustomElem.ValueKind == JsonValueKind.True)
		{
			isCustom = true;
		}
		return new MapTopology(RequireString(rootElement, "mapKey"), item5, item6, cellOriginX, cellOriginY, item3, item4, item, item2, item7, item8, num, num2, item9, item10, displayScale, previewFile, item11, item12, spawnSettings, directions, illegal, array, item13, item14, new ReadOnlyCollection<MapPage>(list), landmarks, occlusionCellIndices, occlusionCellGroups, occlusionGroups, spawnTriggerRange, isCustom);
	}

	private static void ApplyAdminCollisionOverride(string filePath, byte[] directions, int widthCells, int heightCells)
	{
		try
		{
			string json = DataFileSystem.ReadAllText(filePath);
			using JsonDocument doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("cells", out JsonElement cellsElem) && cellsElem.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement c in cellsElem.EnumerateArray())
				{
					if (c.TryGetProperty("x", out JsonElement xElem) && c.TryGetProperty("y", out JsonElement yElem) && c.TryGetProperty("walkable", out JsonElement wElem))
					{
						int cx = xElem.GetInt32();
						int cy = yElem.GetInt32();
						bool walkable = wElem.GetBoolean();
						if (cx >= 0 && cx < widthCells && cy >= 0 && cy < heightCells)
						{
							int idx = cy * widthCells + cx;
							directions[idx] = walkable ? (byte)15 : (byte)0;
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private static (uint[], ushort[], IReadOnlyList<MapOcclusionGroup>) LoadOcclusion(JsonElement root, string directory, int widthCells, int heightCells)
	{
		if (!root.TryGetProperty("occlusion", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return (Array.Empty<uint>(), Array.Empty<ushort>(), Array.Empty<MapOcclusionGroup>());
		}
		JsonElement parent = RequireObject(value, "cells");
		JsonElement parent2 = RequireObject(value, "groups");
		string occFilePath = DataFileSystem.Combine(directory, RequireString(parent, "file"));
		if (!DataFileSystem.Exists(occFilePath))
		{
			return (Array.Empty<uint>(), Array.Empty<ushort>(), Array.Empty<MapOcclusionGroup>());
		}
		byte[] array = DataFileSystem.ReadAllBytes(occFilePath);
		if (array.Length % 8 != 0)
		{
			throw new InvalidDataException("Occlusion cell registry is not 8-byte records.");
		}
		int num = array.Length / 8;
		if (num != RequireInt(parent, "entries"))
		{
			throw new InvalidDataException("Occlusion cell registry entry count mismatch.");
		}
		uint[] array2 = new uint[num];
		ushort[] array3 = new ushort[num];
		long num2 = (long)widthCells * (long)heightCells;
		for (int i = 0; i < num; i++)
		{
			uint num3 = BitConverter.ToUInt32(array, i * 8);
			if (num3 >= num2)
			{
				throw new InvalidDataException("Occlusion cell index is out of bounds.");
			}
			if (i > 0 && num3 < array2[i - 1])
			{
				throw new InvalidDataException("Occlusion cell registry is not sorted.");
			}
			array2[i] = num3;
			array3[i] = BitConverter.ToUInt16(array, i * 8 + 4);
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(DataFileSystem.ReadAllText(DataFileSystem.Combine(directory, RequireString(parent2, "file"))));
		List<MapOcclusionGroup> list = new List<MapOcclusionGroup>();
		foreach (JsonElement item in RequireArray(jsonDocument.RootElement, "groups").EnumerateArray())
		{
			JsonElement property = item.GetProperty("pixelRect");
			list.Add(new MapOcclusionGroup(RequireInt(item, "id"), property[0].GetInt32(), property[1].GetInt32(), property[2].GetInt32(), property[3].GetInt32(), RequireInt(item, "cells")));
		}
		if (list.Count != RequireInt(parent2, "count"))
		{
			throw new InvalidDataException("Occlusion group manifest count mismatch.");
		}
		ushort[] array4 = array3;
		for (int j = 0; j < array4.Length; j++)
		{
			if (array4[j] >= list.Count)
			{
				throw new InvalidDataException("Occlusion cell references an unknown group.");
			}
		}
		return (array2, array3, list);
	}

	public void GetOcclusionGroupsAt(int localX, int localY, List<int> results)
	{
		results.Clear();
		if (_occlusionCellIndices.Length == 0 || !ContainsLocalCell(localX, localY))
		{
			return;
		}
		uint num = (uint)(localY * WidthCells + localX);
		int i = Array.BinarySearch(_occlusionCellIndices, num);
		if (i >= 0)
		{
			while (i > 0 && _occlusionCellIndices[i - 1] == num)
			{
				i--;
			}
			for (; i < _occlusionCellIndices.Length && _occlusionCellIndices[i] == num; i++)
			{
				results.Add(_occlusionCellGroups[i]);
			}
		}
	}

	public bool ContainsLocalCell(int x, int y)
	{
		if (x >= 0 && y >= 0 && x < WidthCells)
		{
			return y < HeightCells;
		}
		return false;
	}

	public bool ContainsGameCell(int gameX, int gameY)
	{
		return ContainsLocalCell(gameX - GameOriginX, gameY - GameOriginY);
	}

	public (int X, int Y) ToLocalCell(int gameX, int gameY)
	{
		return (X: gameX - GameOriginX, Y: gameY - GameOriginY);
	}

	public (int X, int Y) ToGameCell(int localX, int localY)
	{
		EnsureCell(localX, localY);
		return (X: GameOriginX + localX, Y: GameOriginY + localY);
	}

	public byte ZoneAt(int localX, int localY)
	{
		EnsureCell(localX, localY);
		return _zones[localY * WidthCells + localX];
	}

	public MapTerrainZone TerrainZoneAt(int localX, int localY)
	{
		return (MapTerrainZone)ZoneAt(localX, localY);
	}

	public string? SpawnRegionKeyAt(int localX, int localY)
	{
		EnsureCell(localX, localY);
		int num = _spawnRegionIds[localY * WidthCells + localX];
		if (num > 0)
		{
			return SpawnRegionKeys[num];
		}
		return null;
	}

	public bool IsWalkableCell(int localX, int localY)
	{
		if (ContainsLocalCell(localX, localY))
		{
			return !L1jTileRules.IsSolid(_directions[localY * WidthCells + localX]);
		}
		return false;
	}

	public bool IsLegalCell(int localX, int localY)
	{
		if (_illegal != null)
		{
			if (ContainsLocalCell(localX, localY))
			{
				return _illegal[localY * WidthCells + localX] == 0;
			}
			return false;
		}
		return IsWalkableCell(localX, localY);
	}

	public bool IsSafeCell(int localX, int localY)
	{
		if (IsWalkableCell(localX, localY))
		{
			return TerrainZoneAt(localX, localY) == MapTerrainZone.Safe;
		}
		return false;
	}

	public bool IsHuntingCell(int localX, int localY)
	{
		if (IsWalkableCell(localX, localY))
		{
			return TerrainZoneAt(localX, localY) == MapTerrainZone.Hunting;
		}
		return false;
	}

	public (int X, int Y) FindDefaultSpawnLocalCell()
	{
		if (Landmarks != null && Landmarks.Count > 0)
		{
			foreach (var lm in Landmarks)
			{
				if (ContainsLocalCell(lm.LocalX, lm.LocalY) && IsWalkableCell(lm.LocalX, lm.LocalY))
				{
					return (lm.LocalX, lm.LocalY);
				}
			}
		}

		double centerPixelX = (double)FullNativeWidth * 0.5;
		double centerPixelY = (double)FullNativeHeight * 0.5;
		int bestCellX = WidthCells / 2;
		int bestCellY = HeightCells / 2;
		double bestDistSq = double.MaxValue;
		bool foundValid = false;

		for (int cy = 0; cy < HeightCells; cy++)
		{
			for (int cx = 0; cx < WidthCells; cx++)
			{
				if (IsWalkableCell(cx, cy))
				{
					var (px, py) = NativePixelCenter(cx, cy);
					if (px >= 24.0 && px <= (double)FullNativeWidth - 24.0 &&
					    py >= 24.0 && py <= (double)FullNativeHeight - 24.0)
					{
						double dx = px - centerPixelX;
						double dy = py - centerPixelY;
						double distSq = dx * dx + dy * dy;
						if (distSq < bestDistSq)
						{
							bestDistSq = distSq;
							bestCellX = cx;
							bestCellY = cy;
							foundValid = true;
						}
					}
				}
			}
		}

		if (foundValid)
		{
			return (bestCellX, bestCellY);
		}

		for (int cy = 0; cy < HeightCells; cy++)
		{
			for (int cx = 0; cx < WidthCells; cx++)
			{
				if (IsWalkableCell(cx, cy))
				{
					return (cx, cy);
				}
			}
		}

		return (Math.Clamp(WidthCells / 2, 0, Math.Max(0, WidthCells - 1)),
		        Math.Clamp(HeightCells / 2, 0, Math.Max(0, HeightCells - 1)));
	}

	public bool CanMove(int localX, int localY, int deltaX, int deltaY)
	{
		EnsureCell(localX, localY);
		int num = L1jTileRules.HeadingFor(deltaX, deltaY);
		if (num < 0)
		{
			return false;
		}
		return L1jTileRules.IsPassable(_directions, WidthCells, HeightCells, localX, localY, num);
	}

	public bool CanArrowPass(int localX, int localY, int deltaX, int deltaY, Func<int, int, bool>? doorAt = null)
	{
		EnsureCell(localX, localY);
		int num = L1jTileRules.HeadingFor(deltaX, deltaY);
		if (num < 0)
		{
			return false;
		}
		return L1jTileRules.IsArrowPassable(_directions, WidthCells, HeightCells, localX, localY, num, doorAt);
	}

	public byte TileAt(int localX, int localY)
	{
		EnsureCell(localX, localY);
		return _directions[localY * WidthCells + localX];
	}

	public (double X, double Y) NativePixelCenter(int localX, int localY)
	{
		EnsureCell(localX, localY);
		int unshiftedX = localX + CellOriginX;
		int unshiftedY = localY + CellOriginY;
		double num = (double)(ChunkColumns - 1) * 1536.0 / 2.0;
		return (X: (double)(unshiftedX + unshiftedY) * 24.0 + 24.0, Y: num + 756.0 + (double)(unshiftedY - unshiftedX) * 12.0 + 12.0);
	}

	public (double X, double Y) DisplayPixelCenter(int localX, int localY)
	{
		var (num, num2) = NativePixelCenter(localX, localY);
		return (X: num * DisplayScale, Y: num2 * DisplayScale);
	}

	public bool TryLocalCellAtDisplayPixel(double displayPixelX, double displayPixelY, out int localX, out int localY)
	{
		localX = 0;
		localY = 0;
		if (!TryUnboundedLocalCellAtDisplayPixel(displayPixelX, displayPixelY, out localX, out localY))
		{
			return false;
		}
		return ContainsLocalCell(localX, localY);
	}

	public bool TryUnboundedLocalCellAtDisplayPixel(double displayPixelX, double displayPixelY, out int localX, out int localY)
	{
		localX = 0;
		localY = 0;
		if (!double.IsFinite(displayPixelX) || !double.IsFinite(displayPixelY) || DisplayScale <= 0.0)
		{
			return false;
		}
		double num = displayPixelX / DisplayScale;
		double num2 = displayPixelY / DisplayScale;
		double num3 = (double)(ChunkColumns - 1) * 1536.0 / 2.0;
		double num4 = (num - 24.0) / 24.0;
		double num5 = (num2 - num3 - 768.0) / 12.0;
		int unshiftedX = (int)Math.Round((num4 - num5) * 0.5, MidpointRounding.AwayFromZero);
		int unshiftedY = (int)Math.Round((num4 + num5) * 0.5, MidpointRounding.AwayFromZero);
		localX = unshiftedX - CellOriginX;
		localY = unshiftedY - CellOriginY;
		return true;
	}

	public MapPage PageAtNativePixel(double pixelX, double pixelY)
	{
		if (pixelX < 0.0 || pixelY < 0.0 || pixelX >= (double)FullNativeWidth || pixelY >= (double)FullNativeHeight)
		{
			throw new ArgumentOutOfRangeException("pixelX", $"Pixel ({pixelX}, {pixelY}) is outside the map.");
		}
		int num = (int)(pixelX / (double)PageWidth);
		int num2 = (int)(pixelY / (double)PageHeight);
		int index = num2 * PageColumns + num;
		MapPage result = Pages[index];
		if (result.X != num || result.Y != num2)
		{
			throw new InvalidDataException("Map pages are not stored in row-major order.");
		}
		return result;
	}

	public MapPage PageForCell(int localX, int localY)
	{
		var (pixelX, pixelY) = NativePixelCenter(localX, localY);
		return PageAtNativePixel(pixelX, pixelY);
	}

	private void EnsureCell(int x, int y)
	{
		if (!ContainsLocalCell(x, y))
		{
			throw new ArgumentOutOfRangeException("x", $"Cell ({x}, {y}) is outside the map.");
		}
	}

	private static IReadOnlyList<MapLandmark> LoadLandmarks(string path)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(DataFileSystem.ReadAllText(path));
		List<MapLandmark> list = new List<MapLandmark>();
		foreach (JsonElement item in RequireArray(jsonDocument.RootElement, "requirements").EnumerateArray())
		{
			var (gameX, gameY) = ReadPair(item, "anchorGame");
			var (localX, localY) = ReadPair(item, "anchorLocal");
			var (nativePixelX, nativePixelY) = ReadPair(item, "anchorNativePixel");
			list.Add(new MapLandmark(RequireString(item, "id"), gameX, gameY, localX, localY, nativePixelX, nativePixelY, RequireInt(item, "clearWidthCells"), RequireString(item, "status"), RequireString(item, "rule")));
		}
		return new ReadOnlyCollection<MapLandmark>(list);
	}

	private static byte[]? TryReadOptionalGrid(string directory, string relativePath, int width, int height)
	{
		try
		{
			return ReadGridFile(directory, relativePath, width, height);
		}
		catch (InvalidDataException)
		{
			throw;
		}
		catch
		{
			return null;
		}
	}

	private static byte[] ReadGridFile(string directory, string relativePath, int width, int height)
	{
		byte[] array = DataFileSystem.ReadAllBytes(DataFileSystem.Combine(directory, relativePath));
		int num = checked(width * height);
		if (array.Length != num)
		{
			throw new InvalidDataException($"Grid '{relativePath}' contains {array.Length} bytes; expected {num}.");
		}
		return array;
	}

	private static (byte[] Ids, IReadOnlyList<string> Keys) ReadSpawnRegions(JsonElement root, string directory, int width, int height)
	{
		int num = checked(width * height);
		if (!root.TryGetProperty("spawnRegions", out var value))
		{
			return (Ids: new byte[num], Keys: Array.AsReadOnly(new string[1] { string.Empty }));
		}
		if (value.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("'spawnRegions' must be an object.");
		}
		JsonElement jsonElement = RequireArray(value, "keys");
		List<string> keys = new List<string>(jsonElement.GetArrayLength());
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (JsonElement item in jsonElement.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.String)
			{
				throw new InvalidDataException("Every spawn region key must be a string.");
			}
			string text = item.GetString() ?? string.Empty;
			if (!hashSet.Add(text))
			{
				throw new InvalidDataException("Duplicate spawn region key '" + text + "'.");
			}
			keys.Add(text);
		}
		int count = keys.Count;
		bool flag = ((count > 256 || count == 0) ? true : false);
		if (flag || keys[0].Length != 0)
		{
			throw new InvalidDataException("Spawn region keys must start with an empty fallback key and contain at most 256 entries.");
		}
		byte[]? array = TryReadOptionalGrid(directory, RequireString(value, "file"), width, height);
		if (array == null)
		{
			return (Ids: new byte[num], Keys: Array.AsReadOnly(new string[1] { string.Empty }));
		}
		if (array.Any((byte id) => id >= keys.Count))
		{
			throw new InvalidDataException("Spawn region grid contains an unknown region id.");
		}
		return (Ids: array, Keys: new ReadOnlyCollection<string>(keys));
	}

	private static JsonElement RequireObject(JsonElement parent, string property)
	{
		JsonElement result = RequireProperty(parent, property);
		if (result.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("'" + property + "' must be an object.");
		}
		return result;
	}

	private static JsonElement RequireArray(JsonElement parent, string property)
	{
		JsonElement result = RequireProperty(parent, property);
		if (result.ValueKind != JsonValueKind.Array)
		{
			throw new InvalidDataException("'" + property + "' must be an array.");
		}
		return result;
	}

	private static string RequireString(JsonElement parent, string property)
	{
		JsonElement jsonElement = RequireProperty(parent, property);
		if (jsonElement.ValueKind != JsonValueKind.String)
		{
			throw new InvalidDataException("'" + property + "' must be a string.");
		}
		return jsonElement.GetString();
	}

	private static int RequireInt(JsonElement parent, string property)
	{
		if (!RequireProperty(parent, property).TryGetInt32(out var value))
		{
			throw new InvalidDataException("'" + property + "' must be an integer.");
		}
		return value;
	}

	private static double RequireDouble(JsonElement parent, string property)
	{
		if (!RequireProperty(parent, property).TryGetDouble(out var value))
		{
			throw new InvalidDataException("'" + property + "' must be numeric.");
		}
		return value;
	}

	private static (int X, int Y) ReadPair(JsonElement parent, string property)
	{
		JsonElement jsonElement = RequireArray(parent, property);
		if (jsonElement.GetArrayLength() != 2)
		{
			throw new InvalidDataException("'" + property + "' must contain exactly two integers.");
		}
		return (X: jsonElement[0].GetInt32(), Y: jsonElement[1].GetInt32());
	}

	private static JsonElement RequireProperty(JsonElement parent, string property)
	{
		if (!parent.TryGetProperty(property, out var value))
		{
			throw new InvalidDataException("Missing required property '" + property + "'.");
		}
		return value;
	}
}
