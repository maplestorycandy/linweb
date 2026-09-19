using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class WorldCollisionGrid
{
	private const double Epsilon = 1E-06;

	private static readonly (int Column, int Row)[] Neighbors = new(int, int)[8]
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

	private readonly bool[] _blocked;

	private readonly Dictionary<long, int[]> _componentIdsByRadius = new Dictionary<long, int[]>();

	public double OriginX { get; }

	public double OriginY { get; }

	public double CellSize { get; }

	public int Columns { get; }

	public int Rows { get; }

	public double MaxX => OriginX + (double)Columns * CellSize;

	public double MaxY => OriginY + (double)Rows * CellSize;

	public WorldCollisionGrid(double originX, double originY, double cellSize, int columns, int rows, IEnumerable<WorldGridCell>? blockedCells = null)
	{
		if (!double.IsFinite(originX) || !double.IsFinite(originY))
		{
			throw new ArgumentOutOfRangeException("originX", "Grid origin must be finite.");
		}
		if (!double.IsFinite(cellSize) || cellSize <= 0.0)
		{
			throw new ArgumentOutOfRangeException("cellSize", "Cell size must be finite and positive.");
		}
		if (columns <= 0 || rows <= 0)
		{
			throw new ArgumentOutOfRangeException("columns", "Grid dimensions must be positive.");
		}
		if ((long)columns * (long)rows > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("columns", "Grid contains too many cells.");
		}
		OriginX = originX;
		OriginY = originY;
		CellSize = cellSize;
		Columns = columns;
		Rows = rows;
		_blocked = new bool[columns * rows];
		if (blockedCells == null)
		{
			return;
		}
		foreach (WorldGridCell blockedCell in blockedCells)
		{
			if (!IsInside(blockedCell))
			{
				throw new ArgumentOutOfRangeException("blockedCells", $"Blocked cell {blockedCell} is outside the grid.");
			}
			_blocked[Index(blockedCell)] = true;
		}
	}

	public static WorldCollisionGrid FromRows(double originX, double originY, double cellSize, IReadOnlyList<string> rows, char blocked = '#')
	{
		ArgumentNullException.ThrowIfNull(rows, "rows");
		if (rows.Count == 0 || rows[0].Length == 0)
		{
			throw new ArgumentException("Collision rows cannot be empty.", "rows");
		}
		int length = rows[0].Length;
		List<WorldGridCell> list = new List<WorldGridCell>();
		for (int i = 0; i < rows.Count; i++)
		{
			if (rows[i].Length != length)
			{
				throw new ArgumentException("All collision rows must have equal width.", "rows");
			}
			for (int j = 0; j < length; j++)
			{
				if (rows[i][j] == blocked)
				{
					list.Add(new WorldGridCell(j, i));
				}
			}
		}
		return new WorldCollisionGrid(originX, originY, cellSize, length, rows.Count, list);
	}

	public bool IsInside(WorldGridCell cell)
	{
		if (cell.Column >= 0 && cell.Row >= 0 && cell.Column < Columns)
		{
			return cell.Row < Rows;
		}
		return false;
	}

	public bool IsBlocked(WorldGridCell cell)
	{
		if (IsInside(cell))
		{
			return _blocked[Index(cell)];
		}
		return true;
	}

	public WorldGridCell CellAt(WorldPoint point)
	{
		ValidatePoint(point, "point");
		return new WorldGridCell((int)Math.Floor((point.X - OriginX) / CellSize), (int)Math.Floor((point.Y - OriginY) / CellSize));
	}

	public WorldPoint CellCenter(WorldGridCell cell)
	{
		if (!IsInside(cell))
		{
			throw new ArgumentOutOfRangeException("cell");
		}
		return new WorldPoint(OriginX + ((double)cell.Column + 0.5) * CellSize, OriginY + ((double)cell.Row + 0.5) * CellSize);
	}

	public bool CanOccupy(WorldPoint point, double radius = 0.0)
	{
		ValidatePoint(point, "point");
		ValidateRadius(radius);
		if (radius <= 1E-06)
		{
			return !IsBlocked(CellAt(point));
		}
		if (point.X - radius < OriginX - 1E-06 || point.Y - radius < OriginY - 1E-06 || point.X + radius > MaxX + 1E-06 || point.Y + radius > MaxY + 1E-06)
		{
			return false;
		}
		int num = Math.Max(0, (int)Math.Floor((point.X - radius - OriginX) / CellSize));
		int num2 = Math.Min(Columns - 1, (int)Math.Floor((point.X + radius - OriginX) / CellSize));
		int num3 = Math.Max(0, (int)Math.Floor((point.Y - radius - OriginY) / CellSize));
		int num4 = Math.Min(Rows - 1, (int)Math.Floor((point.Y + radius - OriginY) / CellSize));
		double num5 = radius * radius;
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				WorldGridCell cell = new WorldGridCell(j, i);
				if (IsBlocked(cell))
				{
					double num6 = OriginX + (double)j * CellSize;
					double num7 = OriginY + (double)i * CellSize;
					double num8 = Math.Clamp(point.X, num6, num6 + CellSize);
					double num9 = Math.Clamp(point.Y, num7, num7 + CellSize);
					double num10 = point.X - num8;
					double num11 = point.Y - num9;
					if (num10 * num10 + num11 * num11 < num5 - 1E-06)
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	public bool CanTraverseSegment(WorldPoint from, WorldPoint to, double radius = 0.0)
	{
		ValidatePoint(from, "from");
		ValidatePoint(to, "to");
		ValidateRadius(radius);
		double num = from.DistanceTo(to);
		int num2 = Math.Max(1, (int)Math.Ceiling(num / Math.Max(1.0, CellSize / 4.0)));
		for (int i = 0; i <= num2; i++)
		{
			double num3 = (double)i / (double)num2;
			if (!CanOccupy(new WorldPoint(from.X + (to.X - from.X) * num3, from.Y + (to.Y - from.Y) * num3), radius))
			{
				return false;
			}
		}
		return true;
	}

	public bool CanReach(WorldPoint from, WorldPoint to, double radius = 0.0)
	{
		ValidatePoint(from, "from");
		ValidatePoint(to, "to");
		ValidateRadius(radius);
		if (!TryFindNearestWalkable(from, radius, out var walkable) || !TryFindNearestWalkable(to, radius, out var walkable2))
		{
			return false;
		}
		if (CanTraverseSegment(walkable, walkable2, radius))
		{
			return true;
		}
		WorldGridCell worldGridCell = CellAt(walkable);
		WorldGridCell worldGridCell2 = CellAt(walkable2);
		if (CanUseCell(worldGridCell, radius) && CanUseCell(worldGridCell2, radius))
		{
			return AreCellsConnected(worldGridCell, worldGridCell2, radius);
		}
		return FindPath(walkable, walkable2, radius).Count > 0;
	}

	public WorldPoint ResolveMove(WorldPoint from, WorldPoint desired, double radius = 0.0)
	{
		ValidatePoint(from, "from");
		ValidatePoint(desired, "desired");
		ValidateRadius(radius);
		if (!CanOccupy(from, radius))
		{
			return from;
		}
		double num = desired.X - from.X;
		double num2 = desired.Y - from.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		int num4 = Math.Max(1, (int)Math.Ceiling(num3 / Math.Max(1.0, CellSize / 4.0)));
		double num5 = num / (double)num4;
		double num6 = num2 / (double)num4;
		WorldPoint current = from;
		for (int i = 0; i < num4; i++)
		{
			WorldPoint worldPoint = new WorldPoint(current.X + num5, current.Y + num6);
			if (CanOccupy(worldPoint, radius))
			{
				current = worldPoint;
				continue;
			}
			bool num7 = Math.Abs(num5) >= Math.Abs(num6);
			bool flag = false;
			if (num7)
			{
				flag |= TryAxisMove(ref current, num5, 0.0, radius);
				flag |= TryAxisMove(ref current, 0.0, num6, radius);
			}
			else
			{
				flag |= TryAxisMove(ref current, 0.0, num6, radius);
				flag |= TryAxisMove(ref current, num5, 0.0, radius);
			}
			if (!flag)
			{
				break;
			}
		}
		return current;
	}

	public bool TryFindNearestWalkable(WorldPoint point, double radius, out WorldPoint walkable)
	{
		ValidatePoint(point, "point");
		ValidateRadius(radius);
		if (CanOccupy(point, radius))
		{
			walkable = point;
			return true;
		}
		double num = double.PositiveInfinity;
		WorldPoint worldPoint = default(WorldPoint);
		bool result = false;
		for (int i = 0; i < Rows; i++)
		{
			for (int j = 0; j < Columns; j++)
			{
				WorldPoint worldPoint2 = CellCenter(new WorldGridCell(j, i));
				if (CanOccupy(worldPoint2, radius))
				{
					double num2 = point.DistanceSquaredTo(worldPoint2);
					if (!(num2 + 1E-06 >= num))
					{
						num = num2;
						worldPoint = worldPoint2;
						result = true;
					}
				}
			}
		}
		walkable = worldPoint;
		return result;
	}

	public IReadOnlyList<WorldPoint> FindPath(WorldPoint from, WorldPoint to, double radius = 0.0)
	{
		ValidatePoint(from, "from");
		ValidatePoint(to, "to");
		ValidateRadius(radius);
		if (!TryFindNearestWalkable(from, radius, out var walkable) || !TryFindNearestWalkable(to, radius, out var walkable2))
		{
			return Array.Empty<WorldPoint>();
		}
		if (CanTraverseSegment(walkable, walkable2, radius))
		{
			if (walkable == walkable2)
			{
				return new WorldPoint[1] { walkable };
			}
			return new WorldPoint[2] { walkable, walkable2 };
		}
		WorldGridCell worldGridCell = CellAt(walkable);
		WorldGridCell worldGridCell2 = CellAt(walkable2);
		if (CanUseCell(worldGridCell, radius) && CanUseCell(worldGridCell2, radius) && !AreCellsConnected(worldGridCell, worldGridCell2, radius))
		{
			return Array.Empty<WorldPoint>();
		}
		int num = Index(worldGridCell);
		int num2 = Index(worldGridCell2);
		int num3 = Columns * Rows;
		double[] array = Enumerable.Repeat(double.PositiveInfinity, num3).ToArray();
		int[] array2 = Enumerable.Repeat(-1, num3).ToArray();
		bool[] array3 = new bool[num3];
		long num4 = 0L;
		PriorityQueue<int, (double, double, int, int, long)> priorityQueue = new PriorityQueue<int, (double, double, int, int, long)>();
		array[num] = 0.0;
		double num5 = Heuristic(worldGridCell, worldGridCell2);
		priorityQueue.Enqueue(num, (num5, num5, worldGridCell.Row, worldGridCell.Column, num4++));
		int element;
		(double, double, int, int, long) priority;
		while (priorityQueue.TryDequeue(out element, out priority))
		{
			if (array3[element])
			{
				continue;
			}
			if (element == num2)
			{
				return SimplifyPath(ReconstructPath(array2, element, walkable, walkable2), radius);
			}
			array3[element] = true;
			WorldGridCell current = CellFromIndex(element);
			(int, int)[] neighbors = Neighbors;
			for (int i = 0; i < neighbors.Length; i++)
			{
				(int, int) tuple = neighbors[i];
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				WorldGridCell worldGridCell3 = new WorldGridCell(current.Column + item, current.Row + item2);
				if (!CanStep(current, worldGridCell3, item, item2, radius))
				{
					continue;
				}
				bool flag = item != 0 && item2 != 0;
				int num6 = Index(worldGridCell3);
				if (!array3[num6])
				{
					double num7 = array[element] + (flag ? Math.Sqrt(2.0) : 1.0);
					if (!(num7 + 1E-06 >= array[num6]))
					{
						array[num6] = num7;
						array2[num6] = element;
						double num8 = Heuristic(worldGridCell3, worldGridCell2);
						priorityQueue.Enqueue(num6, (num7 + num8, num8, worldGridCell3.Row, worldGridCell3.Column, num4++));
					}
				}
			}
		}
		return Array.Empty<WorldPoint>();
	}

	private bool TryAxisMove(ref WorldPoint current, double dx, double dy, double radius)
	{
		if (Math.Abs(dx) <= 1E-06 && Math.Abs(dy) <= 1E-06)
		{
			return false;
		}
		WorldPoint worldPoint = new WorldPoint(current.X + dx, current.Y + dy);
		if (!CanOccupy(worldPoint, radius))
		{
			return false;
		}
		current = worldPoint;
		return true;
	}

	private bool CanUseCell(WorldGridCell cell, double radius)
	{
		if (IsInside(cell) && !IsBlocked(cell))
		{
			return CanOccupy(CellCenter(cell), radius);
		}
		return false;
	}

	private bool CanStep(WorldGridCell current, WorldGridCell next, int columnOffset, int rowOffset, double radius)
	{
		if (!CanUseCell(next, radius))
		{
			return false;
		}
		if (columnOffset == 0 || rowOffset == 0)
		{
			return true;
		}
		if (CanUseCell(new WorldGridCell(current.Column + columnOffset, current.Row), radius))
		{
			return CanUseCell(new WorldGridCell(current.Column, current.Row + rowOffset), radius);
		}
		return false;
	}

	private bool AreCellsConnected(WorldGridCell from, WorldGridCell to, double radius)
	{
		int[] array = ComponentIds(radius);
		int num = array[Index(from)];
		if (num >= 0)
		{
			return num == array[Index(to)];
		}
		return false;
	}

	private int[] ComponentIds(double radius)
	{
		double num = ((radius <= 1E-06) ? 0.0 : radius);
		long key = BitConverter.DoubleToInt64Bits(num);
		if (_componentIdsByRadius.TryGetValue(key, out int[] value))
		{
			return value;
		}
		int[] array = Enumerable.Repeat(-1, Columns * Rows).ToArray();
		Queue<WorldGridCell> queue = new Queue<WorldGridCell>();
		int num2 = 0;
		for (int i = 0; i < Rows; i++)
		{
			for (int j = 0; j < Columns; j++)
			{
				WorldGridCell worldGridCell = new WorldGridCell(j, i);
				int num3 = Index(worldGridCell);
				if (array[num3] >= 0 || !CanUseCell(worldGridCell, num))
				{
					continue;
				}
				array[num3] = num2;
				queue.Enqueue(worldGridCell);
				WorldGridCell result;
				while (queue.TryDequeue(out result))
				{
					(int, int)[] neighbors = Neighbors;
					for (int k = 0; k < neighbors.Length; k++)
					{
						(int, int) tuple = neighbors[k];
						int item = tuple.Item1;
						int item2 = tuple.Item2;
						WorldGridCell worldGridCell2 = new WorldGridCell(result.Column + item, result.Row + item2);
						if (IsInside(worldGridCell2))
						{
							int num4 = Index(worldGridCell2);
							if (array[num4] < 0 && CanStep(result, worldGridCell2, item, item2, num))
							{
								array[num4] = num2;
								queue.Enqueue(worldGridCell2);
							}
						}
					}
				}
				num2++;
			}
		}
		_componentIdsByRadius[key] = array;
		return array;
	}

	private IReadOnlyList<WorldPoint> ReconstructPath(int[] cameFrom, int currentIndex, WorldPoint start, WorldPoint goal)
	{
		List<WorldPoint> list = new List<WorldPoint>();
		while (currentIndex >= 0)
		{
			list.Add(CellCenter(CellFromIndex(currentIndex)));
			currentIndex = cameFrom[currentIndex];
		}
		list.Reverse();
		list[0] = start;
		if (list[list.Count - 1].DistanceSquaredTo(goal) > 1E-06)
		{
			list.Add(goal);
		}
		else
		{
			list[list.Count - 1] = goal;
		}
		return list;
	}

	private IReadOnlyList<WorldPoint> SimplifyPath(IReadOnlyList<WorldPoint> path, double radius)
	{
		if (path.Count <= 2)
		{
			return path;
		}
		List<WorldPoint> list = new List<WorldPoint> { path[0] };
		int num = 0;
		while (num < path.Count - 1)
		{
			int num2 = path.Count - 1;
			while (num2 > num + 1 && !CanTraverseSegment(path[num], path[num2], radius))
			{
				num2--;
			}
			list.Add(path[num2]);
			num = num2;
		}
		return list;
	}

	private static double Heuristic(WorldGridCell from, WorldGridCell to)
	{
		int val = Math.Abs(to.Column - from.Column);
		int val2 = Math.Abs(to.Row - from.Row);
		int num = Math.Min(val, val2);
		return (double)num * Math.Sqrt(2.0) + (double)Math.Max(val, val2) - (double)num;
	}

	private int Index(WorldGridCell cell)
	{
		return cell.Row * Columns + cell.Column;
	}

	private WorldGridCell CellFromIndex(int index)
	{
		return new WorldGridCell(index % Columns, index / Columns);
	}

	private static void ValidatePoint(WorldPoint point, string name)
	{
		if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
		{
			throw new ArgumentOutOfRangeException(name, "World point must be finite.");
		}
	}

	private static void ValidateRadius(double radius)
	{
		if (!double.IsFinite(radius) || radius < 0.0)
		{
			throw new ArgumentOutOfRangeException("radius", "Radius must be finite and non-negative.");
		}
	}
}
