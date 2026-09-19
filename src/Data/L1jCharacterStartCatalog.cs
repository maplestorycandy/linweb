using System;
using System.IO;

namespace IdleLineage.Data;

public static class L1jCharacterStartCatalog
{
	public const int MapId = 2005;

	public const string MapKey = "l1j_map_2005";

	public const string MapName = "隱藏之谷";

	public const int GameX = 32679;

	public const int GameY = 32866;

	public static (double X, double Y) ResolveDisplaySpawn(MapTopology topology)
	{
		ArgumentNullException.ThrowIfNull(topology, "topology");
		if (!string.Equals(topology.MapKey, "l1j_map_2005", StringComparison.Ordinal))
		{
			throw new InvalidDataException($"Character start requires map '{"l1j_map_2005"}', not '{topology.MapKey}'.");
		}
		var (localX, localY) = topology.ToLocalCell(32679, 32866);
		if (!topology.IsSafeCell(localX, localY))
		{
			throw new InvalidDataException($"Character start ({32679},{32866},{2005}) is not a walkable safe cell.");
		}
		return topology.DisplayPixelCenter(localX, localY);
	}
}
