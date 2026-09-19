using System;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class TeleportLandingRules
{
	public const double MinimumJumpDistance = 400.0;

	public const int Attempts = 256;

	public const double EdgeMargin = 120.0;

	private const int DistanceRelaxedAfter = 192;

	public static bool CanStand(WorldCollisionGrid? grid, MapTopology? topology, WorldPoint point, double actorRadius)
	{
		if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
		{
			return false;
		}
		if (grid != null && !grid.CanOccupy(point, Math.Max(0.0, actorRadius)))
		{
			return false;
		}
		if (topology == null)
		{
			return true;
		}
		if (topology.TryLocalCellAtDisplayPixel(point.X, point.Y, out var localX, out var localY))
		{
			return topology.IsLegalCell(localX, localY);
		}
		return false;
	}

	public static WorldPoint RandomPointInMap(WorldBounds field, WorldCollisionGrid? grid, MapTopology? topology, WorldPoint from, double actorRadius, Func<double> nextDouble)
	{
		ArgumentNullException.ThrowIfNull(nextDouble, "nextDouble");
		if (!field.IsValid)
		{
			throw new ArgumentOutOfRangeException("field", "Teleport field must be a valid rectangle.");
		}
		double num = field.MinX + 120.0;
		double num2 = field.MinY + 120.0;
		double num3 = Math.Max(0.0, field.MaxX - field.MinX - 240.0);
		double num4 = Math.Max(0.0, field.MaxY - field.MinY - 240.0);
		for (int i = 0; i < 256; i++)
		{
			WorldPoint worldPoint = new WorldPoint(num + nextDouble() * num3, num2 + nextDouble() * num4);
			if (CanStand(grid, topology, worldPoint, actorRadius) && (i >= 192 || !(worldPoint.DistanceTo(from) < 400.0)))
			{
				return worldPoint;
			}
		}
		return from;
	}
}
