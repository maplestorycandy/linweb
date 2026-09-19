using System;

namespace IdleLineage.Combat;

public sealed record NpcActionMaterialAvailability(string Name, long Required, long Held)
{
	public bool Enough => Held >= Required;

	public long Short => Math.Max(0L, Required - Held);
}
