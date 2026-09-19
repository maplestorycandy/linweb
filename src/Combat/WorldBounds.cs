using System;

namespace IdleLineage.Combat;

public readonly record struct WorldBounds(double MinX, double MinY, double MaxX, double MaxY)
{
	public bool IsValid
	{
		get
		{
			if (double.IsFinite(MinX) && double.IsFinite(MinY) && double.IsFinite(MaxX) && double.IsFinite(MaxY) && MaxX >= MinX)
			{
				return MaxY >= MinY;
			}
			return false;
		}
	}

	public WorldPoint Clamp(WorldPoint point)
	{
		return new WorldPoint(Math.Clamp(point.X, MinX, MaxX), Math.Clamp(point.Y, MinY, MaxY));
	}
}
