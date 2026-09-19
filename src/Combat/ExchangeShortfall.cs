using System;

namespace IdleLineage.Combat;

public sealed record ExchangeShortfall(string ItemKey, long Required, long Held)
{
	public long Short
	{
		get
		{
			if (Required != long.MaxValue)
			{
				return Math.Max(0L, Required - Held);
			}
			return long.MaxValue;
		}
	}
}
