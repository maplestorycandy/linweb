using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class IsometricMovementRules
{
	public const double TileWidth = 48.0;

	public const double TileHeight = 24.0;

	public const double HalfTileWidth = 24.0;

	public const double HalfTileHeight = 12.0;

	public const int BaseFramesPerStep = 38;

	public const int WalkFramesPerStep = 16;

	private static readonly IsometricStep[] Steps = new IsometricStep[8]
	{
		new IsometricStep(0, -24.0, -12.0),
		new IsometricStep(1, 0.0, -24.0),
		new IsometricStep(2, 24.0, -12.0),
		new IsometricStep(3, 48.0, 0.0),
		new IsometricStep(4, 24.0, 12.0),
		new IsometricStep(5, 0.0, 24.0),
		new IsometricStep(6, -24.0, 12.0),
		new IsometricStep(7, -48.0, 0.0)
	};

	public static double BaseMoveSpeed => Math.Sqrt(720.0) / (19.0 / 30.0);

	public static IReadOnlyList<IsometricStep> Directions => Steps;

	public static int FramesForSpeed(double effectiveMoveSpeed)
	{
		if (!double.IsFinite(effectiveMoveSpeed) || effectiveMoveSpeed <= 0.0)
		{
			return int.MaxValue;
		}
		double num = effectiveMoveSpeed / BaseMoveSpeed;
		return Math.Max(1, (int)Math.Round(38.0 / num, MidpointRounding.AwayFromZero));
	}

	public static IsometricGridPoint GridPointAt(WorldPoint point)
	{
		return GridPointAt(point, default(WorldPoint));
	}

	public static IsometricGridPoint GridPointAt(WorldPoint point, WorldPoint latticeOrigin)
	{
		ValidatePoint(point, "point");
		double num = point.X - latticeOrigin.X;
		double num2 = point.Y - latticeOrigin.Y;
		double value = num / 48.0 + num2 / 24.0;
		double value2 = num / 48.0 - num2 / 24.0;
		return new IsometricGridPoint(RoundGridAxis(value), RoundGridAxis(value2));
	}

	public static WorldPoint WorldPointAt(IsometricGridPoint point)
	{
		return WorldPointAt(point, default(WorldPoint));
	}

	public static WorldPoint WorldPointAt(IsometricGridPoint point, WorldPoint latticeOrigin)
	{
		return new WorldPoint(latticeOrigin.X + 24.0 * (double)(point.AxisA + point.AxisB), latticeOrigin.Y + 12.0 * (double)(point.AxisA - point.AxisB));
	}

	public static WorldPoint Snap(WorldPoint point)
	{
		return WorldPointAt(GridPointAt(point));
	}

	public static WorldPoint Snap(WorldPoint point, WorldPoint latticeOrigin)
	{
		return WorldPointAt(GridPointAt(point, latticeOrigin), latticeOrigin);
	}

	public static WorldPoint Lerp(WorldPoint from, WorldPoint to, int completedFrames, int totalFrames)
	{
		if (totalFrames <= 0)
		{
			throw new ArgumentOutOfRangeException("totalFrames");
		}
		double num = Math.Clamp((double)completedFrames / (double)totalFrames, 0.0, 1.0);
		return new WorldPoint(from.X + (to.X - from.X) * num, from.Y + (to.Y - from.Y) * num);
	}

	private static int RoundGridAxis(double value)
	{
		if (!double.IsFinite(value) || value < -2147483648.0 || value > 2147483647.0)
		{
			throw new ArgumentOutOfRangeException("value", "Isometric grid coordinates must fit in Int32.");
		}
		return (int)Math.Round(value, MidpointRounding.AwayFromZero);
	}

	private static void ValidatePoint(WorldPoint point, string name)
	{
		if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
		{
			throw new ArgumentOutOfRangeException(name, "World points must be finite.");
		}
	}
}
