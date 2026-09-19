using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class CastleWarBookSave
{
	public int Version { get; set; } = 1;

	public List<CastleWarCastleSave> Castles { get; set; } = new List<CastleWarCastleSave>();

	public CastleWarAttemptSave? Active { get; set; }
}
