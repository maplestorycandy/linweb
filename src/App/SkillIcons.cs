using System;
using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

internal static class SkillIcons
{
	private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	public static Texture2D? For(string skillId)
	{
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return null;
		}
		if (Cache.TryGetValue(skillId, out Texture2D value))
		{
			return value;
		}
		string path = "res://assets/icons/skills/" + SkillInfo.Name(skillId) + ".png";
		Texture2D texture2D = (ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null);
		Cache[skillId] = texture2D;
		return texture2D;
	}
}
