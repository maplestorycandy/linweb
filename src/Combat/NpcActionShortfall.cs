using System;

namespace IdleLineage.Combat;

public sealed record NpcActionShortfall(string Name, long Required, long Held)
{
	public long Short => Math.Max(0L, Required - Held);
}
