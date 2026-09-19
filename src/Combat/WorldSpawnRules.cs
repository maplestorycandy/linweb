using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class WorldSpawnRules
{
	private const double Epsilon = 1E-06;

	private const int DefaultRandomAttempts = 24;

	private const double DefaultSeparation = 2.0;

	public static bool TryFindPoint(ICombatRandom random, WorldPoint anchor, double spawnRadius, double minimumDistance, double maximumDistance, WorldBounds? bounds, WorldCollisionGrid? collisionGrid, IReadOnlyList<WorldOccupant> occupied, out WorldPoint point, int randomAttempts = 24, double separation = 2.0, WorldBounds? hiddenFrom = null)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		ArgumentNullException.ThrowIfNull(occupied, "occupied");
		ValidatePoint(anchor, "anchor");
		ValidateNonNegative(spawnRadius, "spawnRadius");
		ValidateNonNegative(minimumDistance, "minimumDistance");
		ValidateNonNegative(maximumDistance, "maximumDistance");
		ValidateNonNegative(separation, "separation");
		if (maximumDistance + 1E-06 < minimumDistance)
		{
			throw new ArgumentOutOfRangeException("maximumDistance", "Maximum spawn distance cannot be smaller than minimum distance.");
		}
		if (randomAttempts < 0)
		{
			throw new ArgumentOutOfRangeException("randomAttempts");
		}
		if (bounds.HasValue && !bounds.GetValueOrDefault().IsValid)
		{
			throw new ArgumentOutOfRangeException("bounds");
		}
		if (hiddenFrom.HasValue && !hiddenFrom.GetValueOrDefault().IsValid)
		{
			throw new ArgumentOutOfRangeException("hiddenFrom");
		}
		foreach (WorldOccupant item in occupied)
		{
			ValidatePoint(item.Position, "occupied");
			ValidateNonNegative(item.Radius, "occupied");
		}
		double num = minimumDistance * minimumDistance;
		double num2 = maximumDistance * maximumDistance;
		for (int i = 0; i < randomAttempts; i++)
		{
			double num3 = random.NextDouble() * (Math.PI * 2.0);
			double num4 = Math.Sqrt(num + random.NextDouble() * Math.Max(0.0, num2 - num));
			WorldPoint worldPoint = new WorldPoint(anchor.X + Math.Cos(num3) * num4, anchor.Y + Math.Sin(num3) * num4);
			if (IsValidCandidate(worldPoint, anchor, spawnRadius, num, num2, bounds, collisionGrid, occupied, separation, hiddenFrom))
			{
				point = worldPoint;
				return true;
			}
		}
		foreach (WorldPoint item2 in FallbackCandidates(anchor, minimumDistance, maximumDistance, collisionGrid))
		{
			if (IsValidCandidate(item2, anchor, spawnRadius, num, num2, bounds, collisionGrid, occupied, separation, hiddenFrom))
			{
				point = item2;
				return true;
			}
		}
		point = default(WorldPoint);
		return false;
	}

	private static bool IsValidCandidate(WorldPoint candidate, WorldPoint anchor, double spawnRadius, double minimumDistanceSquared, double maximumDistanceSquared, WorldBounds? bounds, WorldCollisionGrid? collisionGrid, IReadOnlyList<WorldOccupant> occupied, double separation, WorldBounds? hiddenFrom)
	{
		if (!double.IsFinite(candidate.X) || !double.IsFinite(candidate.Y))
		{
			return false;
		}
		double num = anchor.DistanceSquaredTo(candidate);
		if (num + 1E-06 < minimumDistanceSquared || num > maximumDistanceSquared + 1E-06)
		{
			return false;
		}
		if (hiddenFrom.HasValue)
		{
			WorldBounds valueOrDefault = hiddenFrom.GetValueOrDefault();
			if (candidate.X >= valueOrDefault.MinX - 1E-06 && candidate.X <= valueOrDefault.MaxX + 1E-06 && candidate.Y >= valueOrDefault.MinY - 1E-06 && candidate.Y <= valueOrDefault.MaxY + 1E-06)
			{
				return false;
			}
		}
		if (bounds.HasValue)
		{
			WorldBounds valueOrDefault2 = bounds.GetValueOrDefault();
			if (candidate.X - spawnRadius < valueOrDefault2.MinX - 1E-06 || candidate.Y - spawnRadius < valueOrDefault2.MinY - 1E-06 || candidate.X + spawnRadius > valueOrDefault2.MaxX + 1E-06 || candidate.Y + spawnRadius > valueOrDefault2.MaxY + 1E-06)
			{
				return false;
			}
		}
		if (collisionGrid != null && (!collisionGrid.CanOccupy(candidate, spawnRadius) || collisionGrid.FindPath(anchor, candidate, spawnRadius).Count == 0))
		{
			return false;
		}
		foreach (WorldOccupant item in occupied)
		{
			double num2 = spawnRadius + Math.Max(0.0, item.Radius) + separation;
			if (candidate.DistanceSquaredTo(item.Position) < num2 * num2 - 1E-06)
			{
				return false;
			}
		}
		return true;
	}

	private static IEnumerable<WorldPoint> FallbackCandidates(WorldPoint anchor, double minimumDistance, double maximumDistance, WorldCollisionGrid? collisionGrid)
	{
		double preferred = (minimumDistance + maximumDistance) * 0.5;
		if (collisionGrid != null)
		{
			return (from candidate in (from cell in Enumerable.Range(0, collisionGrid.Rows).SelectMany((int row) => from column in Enumerable.Range(0, collisionGrid.Columns)
						select new WorldGridCell(column, row))
					where !collisionGrid.IsBlocked(cell)
					select cell).Select(collisionGrid.CellCenter).Where(delegate(WorldPoint candidate)
				{
					double num5 = anchor.DistanceTo(candidate);
					return num5 + 1E-06 >= minimumDistance && num5 <= maximumDistance + 1E-06;
				})
				orderby Math.Abs(anchor.DistanceTo(candidate) - preferred), NormalizeAngle(Math.Atan2(candidate.Y - anchor.Y, candidate.X - anchor.X)), candidate.Y, candidate.X
				select candidate).ToArray();
		}
		List<WorldPoint> list = new List<WorldPoint>();
		double[] array = ((minimumDistance != maximumDistance) ? new double[3] { preferred, minimumDistance, maximumDistance } : new double[1] { minimumDistance });
		foreach (double num2 in array)
		{
			for (int num3 = 0; num3 < 32; num3++)
			{
				double num4 = (double)num3 * (Math.PI * 2.0) / 32.0;
				list.Add(new WorldPoint(anchor.X + Math.Cos(num4) * num2, anchor.Y + Math.Sin(num4) * num2));
			}
		}
		return list;
	}

	private static double NormalizeAngle(double angle)
	{
		if (!(angle < 0.0))
		{
			return angle;
		}
		return angle + Math.PI * 2.0;
	}

	private static void ValidatePoint(WorldPoint point, string name)
	{
		if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
		{
			throw new ArgumentOutOfRangeException(name, "World point must be finite.");
		}
	}

	private static void ValidateNonNegative(double value, string name)
	{
		if (!double.IsFinite(value) || value < 0.0)
		{
			throw new ArgumentOutOfRangeException(name, "Value must be finite and non-negative.");
		}
	}
}
