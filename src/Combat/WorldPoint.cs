using System;

namespace IdleLineage.Combat;

public readonly record struct WorldPoint(double X, double Y)
{
	public static WorldPoint Zero => new WorldPoint(0.0, 0.0);

	public double DistanceSquaredTo(WorldPoint other)
	{
		double num = other.X - X;
		double num2 = other.Y - Y;
		return num * num + num2 * num2;
	}

	public double DistanceTo(WorldPoint other)
	{
		return Math.Sqrt(DistanceSquaredTo(other));
	}
}
