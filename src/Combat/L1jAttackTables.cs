using System;

namespace IdleLineage.Combat;

public static class L1jAttackTables
{
	private static readonly int[] StrHitTable = new int[61]
	{
		-2, -2, -2, -2, -2, -2, -2, -2, -2, -1,
		-1, 0, 0, 1, 1, 2, 2, 3, 3, 4,
		4, 5, 5, 5, 6, 6, 6, 7, 7, 7,
		8, 8, 8, 9, 9, 9, 10, 10, 10, 11,
		11, 11, 12, 12, 12, 13, 13, 13, 14, 14,
		14, 15, 15, 15, 16, 16, 16, 17, 17, 17,
		18
	};

	private static readonly int[] DexHitTable = new int[61]
	{
		-2, -2, -2, -2, -2, -2, -2, -1, -1, 0,
		0, 1, 1, 2, 2, 3, 3, 4, 4, 5,
		6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
		16, 17, 18, 19, 19, 19, 20, 20, 20, 21,
		21, 21, 22, 22, 22, 23, 23, 23, 24, 24,
		24, 25, 25, 25, 26, 26, 26, 27, 27, 27,
		28
	};

	private static readonly int[] StrDmgTable = BuildStrDmg();

	private static readonly int[] DexDmgTable = BuildDexDmg();

	private static int[] BuildStrDmg()
	{
		int[] array = new int[128];
		int num = -6;
		for (int i = 0; i <= 22; i++)
		{
			if (i % 2 == 1)
			{
				num++;
			}
			array[i] = num;
		}
		for (int j = 23; j <= 28; j++)
		{
			if (j % 3 == 2)
			{
				num++;
			}
			array[j] = num;
		}
		for (int k = 29; k <= 32; k++)
		{
			if (k % 2 == 1)
			{
				num++;
			}
			array[k] = num;
		}
		for (int l = 33; l <= 34; l++)
		{
			num = (array[l] = num + 1);
		}
		for (int m = 35; m <= 127; m++)
		{
			if (m % 4 == 1)
			{
				num++;
			}
			array[m] = num;
		}
		return array;
	}

	private static int[] BuildDexDmg()
	{
		int[] array = new int[128];
		array[15] = 1;
		array[16] = 2;
		array[17] = 3;
		array[18] = 4;
		array[19] = 4;
		array[20] = 4;
		array[21] = 5;
		array[22] = 5;
		array[23] = 5;
		int num = 5;
		for (int i = 24; i <= 35; i++)
		{
			if (i % 3 == 1)
			{
				num++;
			}
			array[i] = num;
		}
		for (int j = 36; j <= 127; j++)
		{
			if (j % 4 == 1)
			{
				num++;
			}
			array[j] = num;
		}
		return array;
	}

	public static int StrHit(double strength)
	{
		return StrHitTable[Math.Clamp((int)Math.Floor(strength), 0, 60)];
	}

	public static int DexHit(double dexterity)
	{
		return DexHitTable[Math.Clamp((int)Math.Floor(dexterity), 0, 60)];
	}

	public static int StrDmg(double strength)
	{
		return StrDmgTable[Math.Clamp((int)Math.Floor(strength), 0, 127)];
	}

	public static int DexDmg(double dexterity)
	{
		return DexDmgTable[Math.Clamp((int)Math.Floor(dexterity), 0, 127)];
	}
}
