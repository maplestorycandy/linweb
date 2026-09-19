using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace IdleLineage.App;

internal static class ClassIcons
{
	private const string Root = "res://assets/ui/classicons";

	private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	public static Texture2D? For(string classId)
	{
		if (Cache.TryGetValue(classId, out Texture2D value))
		{
			return value;
		}
		string path = "res://assets/ui/classicons/" + classId + ".png";
		Texture2D texture2D = (ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null);
		Cache[classId] = texture2D;
		return texture2D;
	}

	public static IReadOnlyList<string> RestrictedTo(JsonObject? item)
	{
		string value;
		string text = ((item?["req"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? value.Trim() : "");
		if (text.Length == 0 || string.Equals(text, "all", StringComparison.OrdinalIgnoreCase))
		{
			return Array.Empty<string>();
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		string[] array = text.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			ClassDef classDef = ClassCatalog.Find(array[i].Trim());
			if (classDef != null)
			{
				hashSet.Add(classDef.Id);
			}
		}
		if (hashSet.Count == 0 || hashSet.Count == ClassCatalog.All.Length)
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>(hashSet.Count);
		ClassDef[] all = ClassCatalog.All;
		foreach (ClassDef classDef2 in all)
		{
			if (hashSet.Contains(classDef2.Id))
			{
				list.Add(classDef2.Id);
			}
		}
		return list;
	}

	public static string DisplayName(string classId)
	{
		return ClassCatalog.Find(classId)?.Name ?? classId;
	}
}
