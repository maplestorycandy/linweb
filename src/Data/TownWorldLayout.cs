using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleLineage.Data;

public static class TownWorldLayout
{
	private static IReadOnlyList<(int X, int Y)> AllSafeCells(MapTopology topology)
	{
		List<(int, int)> list = new List<(int, int)>();
		for (int i = 0; i < topology.HeightCells; i++)
		{
			for (int j = 0; j < topology.WidthCells; j++)
			{
				if (topology.IsSafeCell(j, i))
				{
					list.Add((j, i));
				}
			}
		}
		return list;
	}

	public static (int X, int Y) SafeZoneCenter(MapTopology topology)
	{
		ArgumentNullException.ThrowIfNull(topology, "topology");
		return SafeZoneCenter(topology, AllSafeCells(topology));
	}

	public static (int X, int Y) SafeZoneCenterNear(MapTopology topology, int centerX, int centerY)
	{
		ArgumentNullException.ThrowIfNull(topology, "topology");
		if (!topology.IsSafeCell(centerX, centerY))
		{
			if (!topology.IsWalkableCell(centerX, centerY))
			{
				throw new InvalidDataException($"Town entry ({centerX},{centerY}) is not walkable.");
			}
			return (X: centerX, Y: centerY);
		}
		return SafeZoneCenter(topology, SafeComponent(topology, centerX, centerY));
	}

	private static (int X, int Y) SafeZoneCenter(MapTopology topology, IReadOnlyList<(int X, int Y)> safeCells)
	{
		long num = 0L;
		long num2 = 0L;
		foreach (var safeCell in safeCells)
		{
			int item = safeCell.X;
			int item2 = safeCell.Y;
			num += item;
			num2 += item2;
		}
		if (safeCells.Count == 0)
		{
			throw new InvalidDataException("Map '" + topology.MapKey + "' has no safe-zone cells.");
		}
		double targetX = (double)num / (double)safeCells.Count;
		double targetY = (double)num2 / (double)safeCells.Count;
		return SelectCell(OpenCells(topology, safeCells, 1), targetX, targetY);
	}

	private static IReadOnlyList<(int X, int Y)> OpenCells(MapTopology topology, IReadOnlyList<(int X, int Y)> cells, int minimumCells)
	{
		List<(int, int)> list = new List<(int, int)>(cells.Count);
		foreach (var cell in cells)
		{
			int item = cell.X;
			int item2 = cell.Y;
			bool flag = true;
			for (int i = -1; i <= 1 && flag; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					if ((j != 0 || i != 0) && !topology.IsWalkableCell(item + j, item2 + i))
					{
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				list.Add((item, item2));
			}
		}
		if (list.Count < Math.Max(1, minimumCells))
		{
			return cells;
		}
		return list;
	}

	private static IReadOnlyList<(int X, int Y)> SafeComponent(MapTopology topology, int startX, int startY)
	{
		HashSet<(int, int)> hashSet = new HashSet<(int, int)> { (startX, startY) };
		Queue<(int X, int Y)> queue = new Queue<(int X, int Y)>();
		queue.Enqueue((startX, startY));
		(int, int)[] array = new(int, int)[8]
		{
			(0, -1),
			(1, -1),
			(1, 0),
			(1, 1),
			(0, 1),
			(-1, 1),
			(-1, 0),
			(-1, -1)
		};
		while (queue.Count > 0)
		{
			(int, int) tuple = queue.Dequeue();
			int item = tuple.Item1;
			int item2 = tuple.Item2;
			(int, int)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(int, int) tuple2 = array2[i];
				int item3 = tuple2.Item1;
				int item4 = tuple2.Item2;
				int num = item + item3;
				int num2 = item2 + item4;
				if (topology.ContainsLocalCell(num, num2) && topology.IsSafeCell(num, num2) && topology.CanMove(item, item2, item3, item4) && hashSet.Add((num, num2)))
				{
					queue.Enqueue((num, num2));
				}
			}
		}
		return (from cell in hashSet
			orderby cell.Item2, cell.Item1
			select cell).ToArray();
	}

	private static (int X, int Y) SelectCell(IReadOnlyList<(int X, int Y)> candidates, double targetX, double targetY)
	{
		(int, int)? tuple = null;
		double num = double.MaxValue;
		foreach (var candidate in candidates)
		{
			int item = candidate.X;
			int item2 = candidate.Y;
			double num2 = (double)item - targetX;
			double num3 = (double)item2 - targetY;
			double num4 = num2 * num2 + num3 * num3;
			if (!(num4 > num) && (num4 != num || !tuple.HasValue || (item2 <= tuple.Value.Item2 && (item2 != tuple.Value.Item2 || item < tuple.Value.Item1))))
			{
				tuple = (item, item2);
				num = num4;
			}
		}
		return tuple ?? throw new InvalidOperationException("This map has no safe-zone cell to stand on.");
	}
}
