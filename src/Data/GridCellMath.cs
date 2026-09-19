using System;

namespace IdleLineage.Data;

public static class GridCellMath
{
	public static float Boundary(int index, float total, int count)
	{
		if (count <= 0)
		{
			return 0f;
		}
		return (float)Math.Round((double)Math.Clamp(index, 0, count) * (double)total / (double)count, MidpointRounding.AwayFromZero);
	}

	public static (float Start, float Length) Cell(int index, float total, int count)
	{
		float num = Boundary(index, total, count);
		return (Start: num, Length: Boundary(index + 1, total, count) - num);
	}
}
