using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class ExplorationNavigationGrid
{
	private readonly record struct QueuedCell(int Index, int Cost);

	private static readonly (int X, int Y)[] Neighbors = new(int, int)[8]
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

	private int[] _components;
	private int[]? _pathCost;
	private int[]? _pathParent;
	private readonly List<int> _touchedPathIndices = new List<int>(512);

	public MapTopology Topology { get; }

	public ExplorationNavigationGrid(MapTopology topology)
	{
		Topology = topology ?? throw new ArgumentNullException("topology");
		_components = BuildComponents();
	}

	public bool HasArrowLineOfSight(WorldPoint from, WorldPoint to)
	{
		return CanReach(from, to);
	}

	private bool IsOpenCell(int x, int y)
	{
		return Topology.IsLegalCell(x, y);
	}

	private bool CanStepInto(int x, int y, int deltaX, int deltaY)
	{
		return IsOpenCell(x + deltaX, y + deltaY);
	}

	public bool TryCellAt(WorldPoint point, out MapSpawnCell cell)
	{
		if (Topology.TryLocalCellAtDisplayPixel(point.X, point.Y, out var localX, out var localY))
		{
			cell = new MapSpawnCell(localX, localY);
			return true;
		}
		cell = default(MapSpawnCell);
		return false;
	}

	public bool IsWalkable(WorldPoint point)
	{
		if (TryCellAt(point, out var cell))
		{
			return IsOpenCell(cell.X, cell.Y);
		}
		return false;
	}

	public WorldPoint CellCenter(MapSpawnCell cell)
	{
		if (!Topology.IsLegalCell(cell.X, cell.Y))
		{
			throw new ArgumentOutOfRangeException("cell", $"Cell ({cell.X}, {cell.Y}) is not legal.");
		}
		var (x, y) = Topology.DisplayPixelCenter(cell.X, cell.Y);
		return new WorldPoint(x, y);
	}

	public WorldPoint SnapToNearestWalkable(WorldPoint point)
	{
		if (!TryCellAt(point, out var cell))
		{
			if (!Topology.TryUnboundedLocalCellAtDisplayPixel(point.X, point.Y, out var localX, out var localY))
			{
				return point;
			}
			return NearestWalkableFrom(new MapSpawnCell(Math.Clamp(localX, 0, Topology.WidthCells - 1), Math.Clamp(localY, 0, Topology.HeightCells - 1)), point);
		}
		if (IsOpenCell(cell.X, cell.Y))
		{
			return point;
		}
		return NearestWalkableFrom(cell, point);
	}

	private WorldPoint NearestWalkableFrom(MapSpawnCell origin, WorldPoint fallback)
	{
		int num = Math.Max(Topology.WidthCells, Topology.HeightCells);
		for (int i = 0; i <= num; i++)
		{
			MapSpawnCell? mapSpawnCell = null;
			double num2 = double.PositiveInfinity;
			for (int j = -i; j <= i; j++)
			{
				for (int k = -i; k <= i; k++)
				{
					if (Math.Max(Math.Abs(k), Math.Abs(j)) != i)
					{
						continue;
					}
					int x = origin.X + k;
					int y = origin.Y + j;
					if (IsOpenCell(x, y))
					{
						WorldPoint other = CellCenter(new MapSpawnCell(x, y));
						double num3 = fallback.DistanceSquaredTo(other);
						if (num3 < num2)
						{
							num2 = num3;
							mapSpawnCell = new MapSpawnCell(x, y);
						}
					}
				}
			}
			if (mapSpawnCell.HasValue)
			{
				MapSpawnCell valueOrDefault = mapSpawnCell.GetValueOrDefault();
				return CellCenter(valueOrDefault);
			}
		}
		return fallback;
	}

	public bool CanTraverseStep(WorldPoint from, WorldPoint to)
	{
		if (!TryCellAt(from, out var cell) || !TryCellAt(to, out var cell2) || !IsOpenCell(cell.X, cell.Y) || !IsOpenCell(cell2.X, cell2.Y))
		{
			return false;
		}
		int num = cell2.X - cell.X;
		int num2 = cell2.Y - cell.Y;
		if (num != 0 || num2 != 0)
		{
			return CanStepInto(cell.X, cell.Y, num, num2);
		}
		return true;
	}

	public bool CanTraverseSegment(WorldPoint from, WorldPoint to)
	{
		if (!TryCellAt(from, out var cell) || !IsOpenCell(cell.X, cell.Y) || !TryCellAt(to, out var cell2) || !IsOpenCell(cell2.X, cell2.Y))
		{
			return false;
		}
		int num = cell2.X - cell.X;
		int num2 = cell2.Y - cell.Y;
		if (Math.Abs(num) <= 1 && Math.Abs(num2) <= 1)
		{
			if (num != 0 || num2 != 0)
			{
				return CanStepInto(cell.X, cell.Y, num, num2);
			}
			return true;
		}
		double num3 = from.DistanceTo(to);
		int num4 = Math.Max(1, (int)Math.Ceiling(num3 / 8.0));
		for (int i = 1; i <= num4; i++)
		{
			double num5 = (double)i / (double)num4;
			WorldPoint point = new WorldPoint(from.X + (to.X - from.X) * num5, from.Y + (to.Y - from.Y) * num5);
			if (!TryCellAt(point, out var cell3) || !IsOpenCell(cell3.X, cell3.Y))
			{
				return false;
			}
			if (!(cell3 == cell))
			{
				int num6 = cell3.X - cell.X;
				int num7 = cell3.Y - cell.Y;
				if (Math.Abs(num6) > 1 || Math.Abs(num7) > 1 || !CanStepInto(cell.X, cell.Y, num6, num7))
				{
					return false;
				}
				cell = cell3;
			}
		}
		return cell == cell2;
	}

	public bool CanReach(WorldPoint from, WorldPoint to)
	{
		if (!TryCellAt(from, out var cell) || !TryCellAt(to, out var cell2))
		{
			return false;
		}
		int num = ComponentAt(cell);
		if (num > 0)
		{
			return num == ComponentAt(cell2);
		}
		return false;
	}

	public bool AreCellsConnected(MapSpawnCell from, MapSpawnCell to)
	{
		int num = ComponentAt(from);
		if (num > 0)
		{
			return num == ComponentAt(to);
		}
		return false;
	}

	public IReadOnlyList<MapSpawnCell> FindPath(MapSpawnCell start, MapSpawnCell goal)
	{
		if (!IsOpenCell(start.X, start.Y) || !IsOpenCell(goal.X, goal.Y) || ComponentAt(start) != ComponentAt(goal))
		{
			return Array.Empty<MapSpawnCell>();
		}
		if (start == goal)
		{
			return new MapSpawnCell[1] { start };
		}
		int num = checked(Topology.WidthCells * Topology.HeightCells);
		if (_pathCost == null || _pathCost.Length != num || _pathParent == null || _pathParent.Length != num)
		{
			_pathCost = new int[num];
			_pathParent = new int[num];
			Array.Fill(_pathCost, int.MaxValue);
			Array.Fill(_pathParent, -1);
		}
		else
		{
			for (int t = 0; t < _touchedPathIndices.Count; t++)
			{
				int touched = _touchedPathIndices[t];
				_pathCost[touched] = int.MaxValue;
				_pathParent[touched] = -1;
			}
		}
		_touchedPathIndices.Clear();

		int[] array = _pathCost;
		int[] array2 = _pathParent;
		int num2 = Index(start);
		int num3 = Index(goal);
		array[num2] = 0;
		_touchedPathIndices.Add(num2);
		int num4 = 0;
		(double, double) tuple = Topology.DisplayPixelCenter(start.X, start.Y);
		double startX = tuple.Item1;
		double startY = tuple.Item2;
		(double X, double Y) tuple2 = Topology.DisplayPixelCenter(goal.X, goal.Y);
		double item = tuple2.X;
		double item2 = tuple2.Y;
		double lineX = item - startX;
		double lineY = item2 - startY;
		double num5 = Math.Sqrt(lineX * lineX + lineY * lineY);
		if (num5 > 0.0)
		{
			lineX /= num5;
			lineY /= num5;
		}
		PriorityQueue<QueuedCell, (int, int, long, int)> priorityQueue = new PriorityQueue<QueuedCell, (int, int, long, int)>();
		int num6 = Heuristic(start, goal);
		priorityQueue.Enqueue(new QueuedCell(num2, 0), (num6, num6, 0L, num4++));
		QueuedCell element;
		(int, int, long, int) priority;
		while (priorityQueue.TryDequeue(out element, out priority))
		{
			if (element.Cost != array[element.Index])
			{
				continue;
			}
			if (element.Index == num3)
			{
				return Reconstruct(array2, num2, num3);
			}
			MapSpawnCell mapSpawnCell = CellAt(element.Index);
			(int, int)[] neighbors = Neighbors;
			for (int i = 0; i < neighbors.Length; i++)
			{
				var (num7, num8) = neighbors[i];
				if (CanStepInto(mapSpawnCell.X, mapSpawnCell.Y, num7, num8))
				{
					MapSpawnCell mapSpawnCell2 = new MapSpawnCell(mapSpawnCell.X + num7, mapSpawnCell.Y + num8);
					int num9 = Index(mapSpawnCell2);
					int num10 = element.Cost + 1;
					if (num10 < array[num9])
					{
						if (array[num9] == int.MaxValue)
						{
							_touchedPathIndices.Add(num9);
						}
						array[num9] = num10;
						array2[num9] = element.Index;
						int num11 = Heuristic(mapSpawnCell2, goal);
						priorityQueue.Enqueue(new QueuedCell(num9, num10), (num10 + num11, num11, DeviationOf(mapSpawnCell2), num4++));
					}
				}
			}
		}
		return Array.Empty<MapSpawnCell>();
		long DeviationOf(MapSpawnCell cell)
		{
			var (num12, num13) = Topology.DisplayPixelCenter(cell.X, cell.Y);
			return (long)Math.Round(Math.Abs((num12 - startX) * lineY - (num13 - startY) * lineX) * 16.0);
		}
	}

	public IReadOnlyList<WorldPoint> FindWorldPath(WorldPoint from, WorldPoint to)
	{
		if (!TryCellAt(from, out var cell) || !TryCellAt(to, out var cell2))
		{
			return Array.Empty<WorldPoint>();
		}
		return FindPath(cell, cell2).Select(CellCenter).ToArray();
	}

	private int[] BuildComponents()
	{
		int[] array = new int[checked(Topology.WidthCells * Topology.HeightCells)];
		Queue<MapSpawnCell> queue = new Queue<MapSpawnCell>();
		int num = 0;
		for (int i = 0; i < Topology.HeightCells; i++)
		{
			for (int j = 0; j < Topology.WidthCells; j++)
			{
				MapSpawnCell mapSpawnCell = new MapSpawnCell(j, i);
				int num2 = Index(mapSpawnCell);
				if (array[num2] != 0 || !IsOpenCell(j, i))
				{
					continue;
				}
				int num3 = (array[num2] = ++num);
				queue.Enqueue(mapSpawnCell);
				while (queue.Count > 0)
				{
					MapSpawnCell mapSpawnCell2 = queue.Dequeue();
					(int, int)[] neighbors = Neighbors;
					for (int k = 0; k < neighbors.Length; k++)
					{
						var (num4, num5) = neighbors[k];
						if (CanStepInto(mapSpawnCell2.X, mapSpawnCell2.Y, num4, num5))
						{
							MapSpawnCell mapSpawnCell3 = new MapSpawnCell(mapSpawnCell2.X + num4, mapSpawnCell2.Y + num5);
							int num6 = Index(mapSpawnCell3);
							if (array[num6] == 0)
							{
								array[num6] = num3;
								queue.Enqueue(mapSpawnCell3);
							}
						}
					}
				}
			}
		}
		return array;
	}

	private IReadOnlyList<MapSpawnCell> Reconstruct(int[] previous, int startIndex, int goalIndex)
	{
		List<MapSpawnCell> list = new List<MapSpawnCell>();
		for (int num = goalIndex; num >= 0; num = previous[num])
		{
			list.Add(CellAt(num));
			if (num == startIndex)
			{
				break;
			}
		}
		if (list[list.Count - 1] != CellAt(startIndex))
		{
			return Array.Empty<MapSpawnCell>();
		}
		list.Reverse();
		return list;
	}

	private int ComponentAt(MapSpawnCell cell)
	{
		if (!Topology.ContainsLocalCell(cell.X, cell.Y))
		{
			return 0;
		}
		return _components[Index(cell)];
	}

	private int Index(MapSpawnCell cell)
	{
		return cell.Y * Topology.WidthCells + cell.X;
	}

	private MapSpawnCell CellAt(int index)
	{
		return new MapSpawnCell(index % Topology.WidthCells, index / Topology.WidthCells);
	}

	private static int Heuristic(MapSpawnCell left, MapSpawnCell right)
	{
		return Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y));
	}
}
