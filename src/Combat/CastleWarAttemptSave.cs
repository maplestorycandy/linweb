using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class CastleWarAttemptSave
{
	public int CastleId { get; set; }

	public string AttackerIdentity { get; set; } = "";

	public string AttackerDisplayName { get; set; } = "";

	public double RemainingSeconds { get; set; } = 1800.0;

	public HashSet<string> DestroyedObjects { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	public Dictionary<string, double> ObjectHealth { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);
}
