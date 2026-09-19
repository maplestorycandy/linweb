using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public sealed record L1jGetbackArea(int X1, int Y1, int X2, int Y2, int MapId, IReadOnlyList<string> MapKeys)
{
	public bool IsSpecified
	{
		get
		{
			if (X1 != 0 && Y1 != 0 && X2 != 0)
			{
				return Y2 != 0;
			}
			return false;
		}
	}

	public bool Contains(string mapKey, int gameX, int gameY)
	{
		if (MapKeys.Contains<string>(mapKey, StringComparer.Ordinal))
		{
			if (IsSpecified)
			{
				if (X1 <= gameX && gameX <= X2 && Y1 <= gameY)
				{
					return gameY <= Y2;
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
