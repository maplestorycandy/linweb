using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record CastleWarDefinition(int Id, string Name, string MapKey, int MinCellX, int MinCellY, int MaxCellX, int MaxCellY, int CrownCellX, int CrownCellY, IReadOnlyList<int> RegistrarNpcIds, string DefaultOwnerName)
{
	public bool Contains(string mapKey, int cellX, int cellY)
	{
		if (string.Equals(MapKey, mapKey, StringComparison.Ordinal) && cellX >= MinCellX && cellX <= MaxCellX && cellY >= MinCellY)
		{
			return cellY <= MaxCellY;
		}
		return false;
	}
}
