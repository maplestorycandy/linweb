using System;
using System.Collections.Generic;

namespace IdleLineage.Data;

public static class MobFlinchCatalog
{
	private static readonly HashSet<string> Never = new HashSet<string>(StringComparer.Ordinal) { "l1j_45682", "l1j_45683", "l1j_45684", "l1j_45681", "l1j_81163", "l1j_90519", "l1j_90518" };

	public static IReadOnlyCollection<string> Keys => Never;

	public static bool NeverFlinches(string? mobKey)
	{
		if (!string.IsNullOrEmpty(mobKey))
		{
			return Never.Contains(mobKey);
		}
		return false;
	}
}
