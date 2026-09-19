using System;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class GatePositionRules
{
	public const int MaxSearchRadius = 16;

	public static bool TryFindNearbyLegalCell(MapTopology topology, int originX, int originY, int maxRadius, out int cellX, out int cellY)
	{
		for (int i = 1; i <= maxRadius; i++)
		{
			int num = 0;
			int num2 = 0;
			long num3 = long.MaxValue;
			for (int j = -i; j <= i; j++)
			{
				for (int k = -i; k <= i; k++)
				{
					if (Math.Max(Math.Abs(j), Math.Abs(k)) != i)
					{
						continue;
					}
					int num4 = originX + j;
					int num5 = originY + k;
					if (topology.IsLegalCell(num4, num5))
					{
						long num6 = (long)j * (long)j + (long)k * (long)k;
						if (num6 < num3)
						{
							num3 = num6;
							num = num4;
							num2 = num5;
						}
					}
				}
			}
			if (num3 != long.MaxValue)
			{
				cellX = num;
				cellY = num2;
				return true;
			}
		}
		cellX = 0;
		cellY = 0;
		return false;
	}
}
