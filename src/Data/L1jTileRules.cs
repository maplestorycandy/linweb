using System;

namespace IdleLineage.Data;

public static class L1jTileRules
{
	public static readonly int[] HeadingDx = new int[8] { 0, 1, 1, 1, 0, -1, -1, -1 };

	public static readonly int[] HeadingDy = new int[8] { -1, -1, 0, 1, 1, 1, 0, -1 };

	public const byte WalkEast = 1;

	public const byte WalkNorth = 2;

	public const byte ArrowEast = 4;

	public const byte ArrowNorth = 8;

	public const byte WalkMask = 3;

	public const byte ArrowMask = 12;

	public const byte ZoneMask = 48;

	public const byte ZoneNormal = 0;

	public const byte ZoneSafety = 16;

	public const byte ZoneCombat = 32;

	public const byte DynamicBlock = 128;

	public const byte FishingTile = 28;

	public static byte AccessTile(ReadOnlySpan<byte> tiles, int width, int height, int x, int y)
	{
		if (x >= 0 && y >= 0 && x < width && y < height)
		{
			return tiles[y * width + x];
		}
		return 0;
	}

	public static bool IsSolid(byte tile)
	{
		return (tile & 3) == 0;
	}

	public static bool IsPassable(ReadOnlySpan<byte> tiles, int width, int height, int x, int y, int heading)
	{
		if (heading < 0 || heading > 7)
		{
			return false;
		}
		byte b = AccessTile(tiles, width, height, x, y);
		int x2 = x + HeadingDx[heading];
		int y2 = y + HeadingDy[heading];
		byte b2 = AccessTile(tiles, width, height, x2, y2);
		if ((b2 & 0x80) != 0)
		{
			return false;
		}
		if ((b2 & 3) == 0)
		{
			return false;
		}
		switch (heading)
		{
		case 0:
			return (b & 2) != 0;
		case 1:
		{
			byte num = AccessTile(tiles, width, height, x, y - 1);
			byte b3 = AccessTile(tiles, width, height, x + 1, y);
			if ((num & 1) == 0)
			{
				return (b3 & 2) != 0;
			}
			return true;
		}
		case 2:
			return (b & 1) != 0;
		case 3:
			return (AccessTile(tiles, width, height, x, y + 1) & 1) != 0;
		case 4:
			return (b2 & 2) != 0;
		case 5:
			if ((b2 & 1) == 0)
			{
				return (b2 & 2) != 0;
			}
			return true;
		case 6:
			return (b2 & 1) != 0;
		default:
			return (AccessTile(tiles, width, height, x - 1, y) & 2) != 0;
		}
	}

	public static bool IsArrowPassable(ReadOnlySpan<byte> tiles, int width, int height, int x, int y, int heading, Func<int, int, bool>? doorAt = null)
	{
		if (heading < 0 || heading > 7)
		{
			return false;
		}
		byte b = AccessTile(tiles, width, height, x, y);
		int num = x + HeadingDx[heading];
		int num2 = y + HeadingDy[heading];
		byte b2 = AccessTile(tiles, width, height, num, num2);
		if (doorAt != null && doorAt(num, num2))
		{
			return false;
		}
		switch (heading)
		{
		case 0:
			return (b & 8) != 0;
		case 1:
		{
			byte num3 = AccessTile(tiles, width, height, x, y - 1);
			byte b3 = AccessTile(tiles, width, height, x + 1, y);
			if ((num3 & 4) == 0)
			{
				return (b3 & 8) != 0;
			}
			return true;
		}
		case 2:
			return (b & 4) != 0;
		case 3:
			return (AccessTile(tiles, width, height, x, y + 1) & 4) != 0;
		case 4:
			return (b2 & 8) != 0;
		case 5:
			if ((b2 & 4) == 0)
			{
				return (b2 & 8) != 0;
			}
			return true;
		case 6:
			return (b2 & 4) != 0;
		default:
			return (AccessTile(tiles, width, height, x - 1, y) & 8) != 0;
		}
	}

	public static bool IsReachable(ReadOnlySpan<byte> tiles, int width, int height, int x, int y)
	{
		if (!IsPassable(tiles, width, height, x, y - 1, 4) && !IsPassable(tiles, width, height, x + 1, y, 6) && !IsPassable(tiles, width, height, x, y + 1, 0) && !IsPassable(tiles, width, height, x - 1, y, 2) && !IsPassable(tiles, width, height, x - 1, y + 1, 1) && !IsPassable(tiles, width, height, x - 1, y - 1, 3) && !IsPassable(tiles, width, height, x + 1, y - 1, 5))
		{
			return IsPassable(tiles, width, height, x + 1, y + 1, 7);
		}
		return true;
	}

	public static byte ZoneOf(byte tile)
	{
		return (byte)(tile & 0x30);
	}

	public static bool IsFishing(byte tile)
	{
		return tile == 28;
	}

	public static int HeadingFor(int deltaX, int deltaY)
	{
		for (int i = 0; i < 8; i++)
		{
			if (HeadingDx[i] == deltaX && HeadingDy[i] == deltaY)
			{
				return i;
			}
		}
		return -1;
	}
}
