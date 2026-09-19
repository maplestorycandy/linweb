using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class PlayerAvatarGfx
{
	public sealed record Entry(int LogicalGfx, int SourceGfx, int ShadowGfx, int IdleBlock);

	private static readonly Dictionary<string, Entry> ByAvatar = new Dictionary<string, Entry>(StringComparer.Ordinal)
	{
		["王子"] = new Entry(0, 3225, 3226, 8),
		["公主"] = new Entry(1, 3227, 3228, 8),
		["男騎士"] = new Entry(61, 3213, 3214, 8),
		["女騎士"] = new Entry(48, 3215, 3216, 8),
		["男妖精"] = new Entry(138, 3217, 3218, 8),
		["女妖精"] = new Entry(37, 3474, 3475, 8),
		["男法師"] = new Entry(734, 3476, 3477, 8),
		["女法師"] = new Entry(1186, 3221, 3222, 8),
		["男黑暗妖精"] = new Entry(4806, 2786, 2787, 8),
		["女黑暗妖精"] = new Entry(4807, 2796, 2797, 8),
		["男龍騎士"] = new Entry(7139, 6658, 6659, 0),
		["女龍騎士"] = new Entry(7140, 6661, 6662, 0),
		["男幻術士"] = new Entry(7141, 6671, 6672, 0),
		["女幻術士"] = new Entry(7142, 6650, 6651, 0),
		["男戰士"] = new Entry(12490, 12490, 12491, 0),
		["女戰士"] = new Entry(12494, 12494, 12495, 0)
	};

	public static Entry? For(string avatar)
	{
		if (string.IsNullOrEmpty(avatar))
		{
			return null;
		}
		if (ByAvatar.TryGetValue(avatar, out Entry value))
		{
			return value;
		}
		foreach (var (value2, result) in ByAvatar)
		{
			if (avatar.EndsWith(value2, StringComparison.Ordinal) || avatar.StartsWith(value2, StringComparison.Ordinal))
			{
				return result;
			}
		}
		return null;
	}
}
