using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record MapPageStreamingDelta(IReadOnlyList<MapPage> ToLoad, IReadOnlyList<MapPage> ToKeep, IReadOnlyList<MapPage> ToUnload)
{
	public int DesiredPageCount => ToLoad.Count + ToKeep.Count;
}
