using System.Collections.Generic;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class MapObstacles
{
	public readonly record struct CellRect(int Col, int Row, int Cols, int Rows);

	public sealed record Layout(string Name, IReadOnlyList<CellRect> Blocks);

	public const double CellSize = 60.0;

	private static readonly Layout TowerPillars = new Layout("石柱廳", new CellRect[12]
	{
		new CellRect(5, 3, 3, 3),
		new CellRect(13, 3, 3, 3),
		new CellRect(23, 3, 3, 3),
		new CellRect(31, 3, 3, 3),
		new CellRect(5, 10, 3, 3),
		new CellRect(13, 10, 3, 3),
		new CellRect(23, 10, 3, 3),
		new CellRect(31, 10, 3, 3),
		new CellRect(5, 17, 3, 3),
		new CellRect(13, 17, 3, 3),
		new CellRect(23, 17, 3, 3),
		new CellRect(31, 17, 3, 3)
	});

	private static readonly Layout TowerHall = new Layout("列柱大廳", new CellRect[16]
	{
		new CellRect(2, 2, 4, 4),
		new CellRect(34, 2, 4, 4),
		new CellRect(2, 19, 4, 4),
		new CellRect(34, 19, 4, 4),
		new CellRect(8, 5, 2, 2),
		new CellRect(16, 5, 2, 2),
		new CellRect(24, 5, 2, 2),
		new CellRect(32, 12, 2, 2),
		new CellRect(8, 12, 2, 2),
		new CellRect(16, 12, 2, 2),
		new CellRect(24, 12, 2, 2),
		new CellRect(6, 12, 2, 2),
		new CellRect(8, 19, 2, 2),
		new CellRect(16, 19, 2, 2),
		new CellRect(24, 19, 2, 2),
		new CellRect(30, 19, 2, 2)
	});

	private static readonly Layout DungeonCorridor = new Layout("地監迴廊", new CellRect[8]
	{
		new CellRect(10, 0, 1, 9),
		new CellRect(10, 13, 1, 12),
		new CellRect(29, 0, 1, 11),
		new CellRect(29, 15, 1, 10),
		new CellRect(13, 6, 6, 1),
		new CellRect(22, 6, 5, 1),
		new CellRect(13, 18, 2, 1),
		new CellRect(18, 18, 9, 1)
	});

	private static readonly Layout DungeonRooms = new Layout("地監房間", new CellRect[9]
	{
		new CellRect(6, 4, 12, 1),
		new CellRect(21, 4, 13, 1),
		new CellRect(6, 20, 6, 1),
		new CellRect(15, 20, 19, 1),
		new CellRect(6, 5, 1, 6),
		new CellRect(6, 14, 1, 6),
		new CellRect(33, 5, 1, 3),
		new CellRect(33, 11, 1, 9),
		new CellRect(17, 9, 3, 2)
	});

	private static readonly Layout CaveRubble = new Layout("洞窟亂石", new CellRect[9]
	{
		new CellRect(3, 3, 5, 4),
		new CellRect(12, 2, 4, 3),
		new CellRect(24, 4, 6, 5),
		new CellRect(33, 8, 5, 4),
		new CellRect(6, 10, 3, 6),
		new CellRect(14, 15, 7, 4),
		new CellRect(26, 16, 5, 5),
		new CellRect(34, 19, 4, 4),
		new CellRect(2, 20, 4, 3)
	});

	private static readonly Layout CaveNarrows = new Layout("洞窟狹道", new CellRect[7]
	{
		new CellRect(0, 0, 14, 5),
		new CellRect(20, 0, 20, 4),
		new CellRect(0, 8, 8, 5),
		new CellRect(30, 7, 10, 6),
		new CellRect(10, 18, 10, 5),
		new CellRect(26, 19, 9, 6),
		new CellRect(16, 7, 4, 3)
	});

	private static readonly Layout[] TowerPool = new Layout[2] { TowerPillars, TowerHall };

	private static readonly Layout[] DungeonPool = new Layout[2] { DungeonCorridor, DungeonRooms };

	private static readonly Layout[] CavePool = new Layout[2] { CaveRubble, CaveNarrows };

	public static Layout? For(string mapKey, string mapName)
	{
		if (mapKey.StartsWith("town_") || IsSafeName(mapName))
		{
			return null;
		}
		Layout[] array = (Contains(mapName, "地監", "地下", "監獄") ? DungeonPool : (Contains(mapName, "洞穴", "洞窟", "墓穴") ? CavePool : (Contains(mapName, "塔") ? TowerPool : null)));
		if (array != null)
		{
			return array[StableHash(mapKey) % (uint)array.Length];
		}
		return null;
	}

	private static bool IsSafeName(string name)
	{
		return Contains(name, "村莊", "城鎮", "港口");
	}

	private static bool Contains(string text, params string[] keys)
	{
		foreach (string value in keys)
		{
			if (text.Contains(value))
			{
				return true;
			}
		}
		return false;
	}

	private static uint StableHash(string s)
	{
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num ^= c;
			num *= 16777619;
		}
		return num;
	}

	public static WorldCollisionGrid Build(Layout layout, Rect2 field)
	{
		int num = (int)Mathf.Round((double)field.Size.X / 60.0);
		int num2 = (int)Mathf.Round((double)field.Size.Y / 60.0);
		List<WorldGridCell> list = new List<WorldGridCell>();
		foreach (CellRect block in layout.Blocks)
		{
			for (int i = block.Row; i < block.Row + block.Rows; i++)
			{
				for (int j = block.Col; j < block.Col + block.Cols; j++)
				{
					if (j >= 0 && i >= 0 && j < num && i < num2)
					{
						list.Add(new WorldGridCell(j, i));
					}
				}
			}
		}
		return new WorldCollisionGrid(field.Position.X, field.Position.Y, 60.0, num, num2, list);
	}

	public static IEnumerable<Rect2> WorldRects(Layout layout, Rect2 field)
	{
		foreach (CellRect block in layout.Blocks)
		{
			yield return new Rect2(field.Position.X + (float)((double)block.Col * 60.0), field.Position.Y + (float)((double)block.Row * 60.0), (float)((double)block.Cols * 60.0), (float)((double)block.Rows * 60.0));
		}
	}
}
