using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

public static class Backgrounds
{
	private static readonly Dictionary<string, Texture2D?> _cache = new Dictionary<string, Texture2D>();

	public static Texture2D? Asset(string relativePath)
	{
		string text = relativePath.Replace('\\', '/').TrimStart('/');
		string key = "asset:" + text;
		if (_cache.TryGetValue(key, out Texture2D value))
		{
			return value;
		}
		string text2 = "res://" + text;
		Texture2D texture2D = (ResourceLoader.Exists(text2) ? GD.Load<Texture2D>(text2) : null);
		if (texture2D == null)
		{
			GD.PushWarning("[Backgrounds] 找不到指定圖片 " + text2);
		}
		_cache[key] = texture2D;
		return texture2D;
	}

	public static Texture2D? Area(string name)
	{
		if (_cache.TryGetValue(name, out Texture2D value))
		{
			return value;
		}
		Texture2D texture2D = null;
		string text = "res://assets/area/1920x1080/" + name + ".jpg";
		if (ResourceLoader.Exists(text))
		{
			texture2D = GD.Load<Texture2D>(text);
		}
		if (texture2D == null)
		{
			GD.PushWarning("[Backgrounds] 找不到區域圖 " + text);
		}
		_cache[name] = texture2D;
		return texture2D;
	}
}
