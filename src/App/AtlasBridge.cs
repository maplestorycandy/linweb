using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Godot;
using Godot.Collections;

namespace IdleLineage.App;

public sealed class AtlasBridge
{
	public readonly record struct Bounds(float Cx, float Top, float Bottom)
	{
		public float Height => Bottom - Top;
	}

	private readonly Node _atlas;

	private readonly System.Collections.Generic.Dictionary<string, string[]> _nameCache = new System.Collections.Generic.Dictionary<string, string[]>();

	private readonly System.Collections.Generic.Dictionary<string, SpriteFrames> _framesCache = new System.Collections.Generic.Dictionary<string, SpriteFrames>();

	private readonly HashSet<string> _missingAtlases = new HashSet<string>(StringComparer.Ordinal);

	private string _mapEpoch = "";

	private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float[]>>? _animTicks;

	private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, Vector2[]>>? _walkShifts;

	private AtlasBridge(Node atlas)
	{
		_atlas = atlas;
	}

	public static AtlasBridge Resolve(Node owner)
	{
		Node nodeOrNull = owner.GetNodeOrNull("/root/AtlasLibrary");
		if (nodeOrNull != null)
		{
			return new AtlasBridge(nodeOrNull);
		}
		Script script = GD.Load<Script>("res://scripts/atlas_library.gd");
		Node node = new Node
		{
			Name = "AtlasLibraryLocal"
		};
		node.SetScript(script);
		owner.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		return new AtlasBridge(node);
	}

	private static string? ResolveDiskPath(string relativePath)
	{
		try
		{
			string p1 = ProjectSettings.GlobalizePath("res://" + relativePath.TrimStart('/', '\\'));
			if (File.Exists(p1)) return p1;
		}
		catch { }

		try
		{
			string exeDir = OS.GetExecutablePath().GetBaseDir();
			string p2 = Path.Combine(exeDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(p2)) return p2;
		}
		catch { }

		try
		{
			string baseDir = AppContext.BaseDirectory;
			string p3 = Path.Combine(baseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(p3)) return p3;
		}
		catch { }

		try
		{
			string p4 = Path.GetFullPath(relativePath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(p4)) return p4;
		}
		catch { }

		return null;
	}

	private static string? ResolveDiskDirPath(string relativePath)
	{
		try
		{
			string p1 = ProjectSettings.GlobalizePath("res://" + relativePath.TrimStart('/', '\\'));
			if (Directory.Exists(p1)) return p1;
		}
		catch { }

		try
		{
			string exeDir = OS.GetExecutablePath().GetBaseDir();
			string p2 = Path.Combine(exeDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
			if (Directory.Exists(p2)) return p2;
		}
		catch { }

		try
		{
			string p3 = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
			if (Directory.Exists(p3)) return p3;
		}
		catch { }

		return null;
	}

	public bool HasAtlas(string group, string name)
	{
		string key = $"{group}/{name}";
		if (_missingAtlases.Contains(key))
		{
			return false;
		}
		if (_atlas.Call("has_atlas", group, name).AsBool())
		{
			return true;
		}
		bool found = ResolveDiskPath($"assets/atlas/{group}/{name}.json") != null;
		if (!found)
		{
			_missingAtlases.Add(key);
		}
		return found;
	}

	public string[] AtlasNames(string group)
	{
		if (_nameCache.TryGetValue(group, out string[] value))
		{
			return value;
		}
		HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
		using DirAccess dirAccess = DirAccess.Open("res://assets/atlas/" + group);
		if (dirAccess != null)
		{
			string[] files = dirAccess.GetFiles();
			foreach (string text in files)
			{
				if (text.EndsWith(".json", StringComparison.Ordinal))
				{
					set.Add(text.Substring(0, text.Length - 5));
				}
			}
		}
		string? diskDir = ResolveDiskDirPath($"assets/atlas/{group}");
		if (diskDir != null && Directory.Exists(diskDir))
		{
			foreach (string file in Directory.GetFiles(diskDir, "*.json"))
			{
				set.Add(Path.GetFileNameWithoutExtension(file));
			}
		}
		string[] array = set.ToArray();
		_nameCache[group] = array;
		return array;
	}

	public string ResolveName(string group, string name)
	{
		if (string.IsNullOrEmpty(name) || HasAtlas(group, name))
		{
			return name;
		}
		string text = "";
		string[] array = AtlasNames(group);
		foreach (string text2 in array)
		{
			if (text2.Length > text.Length && text2.Length < name.Length && name.Contains(text2, StringComparison.Ordinal))
			{
				text = text2;
			}
		}
		if (text.Length <= 0)
		{
			return name;
		}
		return text;
	}

	public bool HasAction(string group, string name, string action)
	{
		Dictionary dictionary = Manifest(group, name);
		if (dictionary != null && dictionary.ContainsKey("frames"))
		{
			return dictionary["frames"].AsGodotDictionary().ContainsKey(action);
		}
		return false;
	}

	public Dictionary? Manifest(string group, string name)
	{
		Variant variant = _atlas.Call("get_manifest", group, name);
		if (variant.VariantType != Variant.Type.Nil)
		{
			Dictionary d = variant.AsGodotDictionary();
			if (d != null && d.Count > 0)
			{
				return d;
			}
		}
		string? jsonPath = ResolveDiskPath($"assets/atlas/{group}/{name}.json");
		if (jsonPath != null && File.Exists(jsonPath))
		{
			string jsonStr = File.ReadAllText(jsonPath);
			Variant parsed = Json.ParseString(jsonStr);
			if (parsed.VariantType == Variant.Type.Dictionary)
			{
				return parsed.AsGodotDictionary();
			}
		}
		return null;
	}

	public IReadOnlyList<string> ActionNames(string group, string name)
	{
		Dictionary dictionary = Manifest(group, name);
		if (dictionary == null || !dictionary.ContainsKey("frames"))
		{
			return System.Array.Empty<string>();
		}
		Dictionary dictionary2 = dictionary["frames"].AsGodotDictionary();
		List<string> list = new List<string>(dictionary2.Count);
		foreach (Variant key in dictionary2.Keys)
		{
			list.Add(key.AsString());
		}
		return list;
	}

	public SpriteFrames? SpriteFrames(string group, string name, float fps = 8f, bool loop = true)
	{
		return _atlas.Call("get_sprite_frames", group, name, fps, loop).As<SpriteFrames>();
	}

	private static bool HasAnyValidFrames(SpriteFrames? sf)
	{
		if (sf == null) return false;
		string[] anims = sf.GetAnimationNames();
		if (anims == null || anims.Length == 0) return false;
		foreach (string anim in anims)
		{
			if (sf.GetFrameCount(anim) > 0 && sf.GetFrameTexture(anim, 0) != null)
			{
				return true;
			}
		}
		return false;
	}

	private SpriteFrames? BuildFramesFromDisk(string group, string name, float fps = 8f, bool loop = true)
	{
		string? jsonPath = ResolveDiskPath($"assets/atlas/{group}/{name}.json");
		string? pngPath = ResolveDiskPath($"assets/atlas/{group}/{name}.png");
		if (jsonPath == null || pngPath == null || !File.Exists(jsonPath) || !File.Exists(pngPath))
		{
			GD.PushWarning($"[AtlasBridge] 無法從磁碟找到圖集檔案 {group}/{name} (json: {jsonPath ?? "null"}, png: {pngPath ?? "null"})");
			return null;
		}

		Image img = Image.LoadFromFile(pngPath);
		if (img == null || img.IsEmpty())
		{
			GD.PushWarning($"[AtlasBridge] 磁碟圖片載入失敗: {pngPath}");
			return null;
		}

		ImageTexture tex = ImageTexture.CreateFromImage(img);
		if (tex == null)
		{
			GD.PushWarning($"[AtlasBridge] 建立貼圖失敗: {pngPath}");
			return null;
		}

		string jsonStr = File.ReadAllText(jsonPath);
		if (string.IsNullOrEmpty(jsonStr))
		{
			return null;
		}

		JsonNode? parsedNode;
		try
		{
			parsedNode = JsonNode.Parse(jsonStr);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[AtlasBridge] 解析圖集 JSON 失敗 {jsonPath}: {ex.Message}");
			return null;
		}

		if (!(parsedNode is JsonObject root) || !(root["frames"] is JsonObject framesObj))
		{
			return null;
		}

		SpriteFrames sf = new SpriteFrames();
		if (sf.HasAnimation("default"))
		{
			sf.RemoveAnimation("default");
		}

		foreach (KeyValuePair<string, JsonNode> pair in framesObj)
		{
			string action = pair.Key;
			if (!(pair.Value is JsonArray frameArray)) continue;

			sf.AddAnimation(action);
			sf.SetAnimationSpeed(action, fps);
			sf.SetAnimationLoop(action, loop);

			for (int i = 0; i < frameArray.Count; i++)
			{
				if (!(frameArray[i] is JsonObject f)) continue;
				float x = (float)(f["x"]?.GetValue<double>() ?? 0.0);
				float y = (float)(f["y"]?.GetValue<double>() ?? 0.0);
				float w = (float)(f["w"]?.GetValue<double>() ?? 0.0);
				float h = (float)(f["h"]?.GetValue<double>() ?? 0.0);
				float dx = (float)(f["dx"]?.GetValue<double>() ?? 0.0);
				float dy = (float)(f["dy"]?.GetValue<double>() ?? 0.0);
				float cw = (float)(f["cw"]?.GetValue<double>() ?? (double)w);
				float ch = (float)(f["ch"]?.GetValue<double>() ?? (double)h);

				AtlasTexture at = new AtlasTexture
				{
					Atlas = tex,
					Region = new Rect2(x, y, w, h),
					Margin = new Rect2(dx, dy, cw - w, ch - h),
					FilterClip = true
				};
				sf.AddFrame(action, at);
			}
		}

		GD.Print($"[AtlasBridge] 成功由磁碟載入圖集 {group}/{name} (共 {framesObj.Count} 個動作)");
		return sf;
	}

	public SpriteFrames? BuildFrames(string group, string name, float fps = 8f)
	{
		string key = $"{group}/{name}@{fps}";
		if (_framesCache.TryGetValue(key, out SpriteFrames value))
		{
			return value;
		}
		bool flag = !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("IDLE_LINEAGE_ATLAS_DIAG"));
		long num = (flag ? Stopwatch.GetTimestamp() : 0);
		if (!HasAtlas(group, name))
		{
			return null;
		}
		long num2 = (flag ? Stopwatch.GetTimestamp() : 0);
		if (flag)
		{
			_atlas.Call("get_texture", group, name);
		}
		long num3 = (flag ? Stopwatch.GetTimestamp() : 0);
		SpriteFrames? spriteFrames = null;
		string? diskPng = ResolveDiskPath($"assets/atlas/{group}/{name}.png");
		if (diskPng != null && File.Exists(diskPng))
		{
			spriteFrames = BuildFramesFromDisk(group, name, fps);
		}
		if (spriteFrames == null || !HasAnyValidFrames(spriteFrames))
		{
			spriteFrames = SpriteFrames(group, name, fps);
		}
		if (spriteFrames == null || !HasAnyValidFrames(spriteFrames))
		{
			spriteFrames = BuildFramesFromDisk(group, name, fps);
		}
		long num4 = (flag ? Stopwatch.GetTimestamp() : 0);
		if (spriteFrames == null)
		{
			return null;
		}
		System.Collections.Generic.Dictionary<string, float[]> dictionary = TicksFor(group, name);
		string[] animationNames = spriteFrames.GetAnimationNames();
		foreach (string text in animationNames)
		{
			bool flag2 = text.Contains("attack") || text.Contains("hurt") || text.Contains("death") || text.Contains("skill") || text.Contains("breath") || text.Contains("_effect") || text.Contains("get");
			spriteFrames.SetAnimationLoop(text, !flag2);
			int frameCount = spriteFrames.GetFrameCount(text);
			float[] value2 = null;
			if (dictionary != null)
			{
				string input = Regex.Replace(text.ToString(), "^d\\d+/", "");
				input = Regex.Replace(input, "_(s|w(?:\\d+)?|effect)$", "");
				dictionary.TryGetValue(input, out value2);
			}
			if (value2 != null && value2.Length >= frameCount && frameCount > 0)
			{
				spriteFrames.SetAnimationSpeed(text, 25.0);
				for (int j = 0; j < frameCount; j++)
				{
					spriteFrames.SetFrame(text, j, spriteFrames.GetFrameTexture(text, j), Mathf.Max(0.5f, value2[j]));
				}
			}
			else
			{
				spriteFrames.SetAnimationSpeed(text, flag2 ? 12.0 : 8.0);
			}
		}
		_framesCache[key] = spriteFrames;
		if (flag)
		{
			double num5 = 1000.0 / (double)Stopwatch.Frequency;
			long timestamp = Stopwatch.GetTimestamp();
			GD.Print($"[AtlasDiag] {group}/{name} manifest={(double)(num2 - num) * num5:F1}ms png={(double)(num3 - num2) * num5:F1}ms assemble={(double)(num4 - num3) * num5:F1}ms post={(double)(timestamp - num4) * num5:F1}ms TOTAL={(double)(timestamp - num) * num5:F1}ms anims={spriteFrames.GetAnimationNames().Length}");
		}
		return spriteFrames;
	}

	public bool HasFrames(string group, string name, float fps = 8f)
	{
		string key = $"{group}/{name}@{fps}";
		return _framesCache.ContainsKey(key);
	}

	public void EvictOnMapChange(string mapKey)
	{
		if (!string.IsNullOrEmpty(mapKey) && !string.Equals(mapKey, _mapEpoch, StringComparison.Ordinal))
		{
			_mapEpoch = mapKey;
			// Only evict non-essential animations if cache has grown very large (>120 entries)
			// Never evict player hero, weapons, skills or fx animations to prevent map transition stutters!
			if (_framesCache.Count > 120)
			{
				List<string> toEvict = new List<string>();
				foreach (var k in _framesCache.Keys)
				{
					if (!k.StartsWith("hero/") && !k.StartsWith("weapon/") && !k.StartsWith("fx/") && !k.StartsWith("spell/"))
					{
						toEvict.Add(k);
					}
				}
				foreach (var k in toEvict)
				{
					_framesCache.Remove(k);
				}
				_atlas.Call("clear_cache");
			}
		}
	}

	private static System.Collections.Generic.Dictionary<string, float[]>? TicksFor(string group, string name)
	{
		if (_animTicks == null)
		{
			_animTicks = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float[]>>();
			string text = (Godot.FileAccess.FileExists("res://data/anim-ticks.json") ? Godot.FileAccess.GetFileAsString("res://data/anim-ticks.json") : "");
			if (text.Length > 0 && JsonNode.Parse(text) is JsonObject jsonObject && jsonObject["ticks"] is JsonObject jsonObject2)
			{
				foreach (KeyValuePair<string, JsonNode> item in jsonObject2)
				{
					item.Deconstruct(out var key, out var value);
					string key2 = key;
					if (!(value is JsonObject jsonObject3))
					{
						continue;
					}
					System.Collections.Generic.Dictionary<string, float[]> dictionary = new System.Collections.Generic.Dictionary<string, float[]>();
					foreach (KeyValuePair<string, JsonNode> item2 in jsonObject3)
					{
						item2.Deconstruct(out key, out value);
						string key3 = key;
						if (value is JsonArray jsonArray)
						{
							float[] array = new float[jsonArray.Count];
							for (int i = 0; i < jsonArray.Count; i++)
							{
								array[i] = (float)(jsonArray[i]?.GetValue<double>() ?? 1.0);
							}
							dictionary[key3] = array;
						}
					}
					_animTicks[key2] = dictionary;
				}
			}
		}
		if (!_animTicks.TryGetValue(group + "/" + name, out System.Collections.Generic.Dictionary<string, float[]> value2))
		{
			return null;
		}
		return value2;
	}

	public Bounds ContentBounds(string group, string name, string action)
	{
		Bounds result = new Bounds(32f, 0f, 64f);
		Dictionary dictionary = Manifest(group, name);
		if (dictionary == null || !dictionary.ContainsKey("frames"))
		{
			return result;
		}
		Dictionary dictionary2 = dictionary["frames"].AsGodotDictionary();
		if (!dictionary2.ContainsKey(action))
		{
			if (dictionary2.ContainsKey("d0/idle"))
			{
				action = "d0/idle";
			}
			else if (dictionary2.Count > 0)
			{
				foreach (Variant k in dictionary2.Keys)
				{
					action = k.AsString();
					break;
				}
			}
			else
			{
				return result;
			}
		}
		float num = 1E+09f;
		float num2 = -1E+09f;
		float num3 = 1E+09f;
		float num4 = -1E+09f;
		foreach (Variant item in dictionary2[action].AsGodotArray())
		{
			Dictionary dictionary3 = item.AsGodotDictionary();
			float num5 = (float)dictionary3["dx"].AsDouble();
			float num6 = (float)dictionary3["dy"].AsDouble();
			float num7 = (float)dictionary3["w"].AsDouble();
			float num8 = (float)dictionary3["h"].AsDouble();
			num = Mathf.Min(num, num5);
			num2 = Mathf.Max(num2, num5 + num7);
			num3 = Mathf.Min(num3, num6);
			num4 = Mathf.Max(num4, num6 + num8);
		}
		if (num2 < num)
		{
			return result;
		}
		return new Bounds((num + num2) * 0.5f, num3, num4);
	}

	public Vector2[]? FrameAnchorOffsets(string group, string name, string action)
	{
		if (_walkShifts == null)
		{
			_walkShifts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, Vector2[]>>();
			string text = (Godot.FileAccess.FileExists("res://data/walk-shift.json") ? Godot.FileAccess.GetFileAsString("res://data/walk-shift.json") : "");
			if (text.Length > 0 && JsonNode.Parse(text) is JsonObject jsonObject && jsonObject["shifts"] is JsonObject jsonObject2)
			{
				foreach (KeyValuePair<string, JsonNode> item in jsonObject2)
				{
					item.Deconstruct(out var key, out var value);
					string key2 = key;
					if (!(value is JsonObject jsonObject3))
					{
						continue;
					}
					System.Collections.Generic.Dictionary<string, Vector2[]> dictionary = new System.Collections.Generic.Dictionary<string, Vector2[]>();
					foreach (KeyValuePair<string, JsonNode> item2 in jsonObject3)
					{
						item2.Deconstruct(out key, out value);
						string key3 = key;
						if (!(value is JsonArray jsonArray))
						{
							continue;
						}
						Vector2[] array = new Vector2[jsonArray.Count];
						for (int i = 0; i < jsonArray.Count; i++)
						{
							if (jsonArray[i] is JsonArray { Count: 2 } jsonArray2)
							{
								array[i] = new Vector2((float)(jsonArray2[0]?.GetValue<double>() ?? 0.0), (float)(jsonArray2[1]?.GetValue<double>() ?? 0.0));
							}
						}
						dictionary[key3] = array;
					}
					_walkShifts[key2] = dictionary;
				}
			}
		}
		if (!_walkShifts.TryGetValue(name, out System.Collections.Generic.Dictionary<string, Vector2[]> value2) || !value2.TryGetValue(action, out var value3))
		{
			return null;
		}
		return value3;
	}

	public AnimatedSprite2D? MakeSprite(string group, string name, string firstAction, float fps = 8f)
	{
		if (!HasAtlas(group, name))
		{
			GD.PushWarning("[AtlasBridge] 找不到圖集 " + group + "/" + name);
			return null;
		}
		SpriteFrames spriteFrames = BuildFrames(group, name, fps);
		if (spriteFrames == null)
		{
			return null;
		}
		AnimatedSprite2D spr = new AnimatedSprite2D
		{
			SpriteFrames = spriteFrames,
			Centered = false
		};
		Bounds bounds = ContentBounds(group, name, firstAction);
		spr.Offset = new Vector2(0f - bounds.Cx, 0f - bounds.Bottom);
		spr.SetMeta("h", bounds.Height);
		spr.SetMeta("idle", firstAction);
		if (spriteFrames.HasAnimation(firstAction))
		{
			spr.Animation = firstAction;
			spr.Play();
		}
		spr.AnimationFinished += delegate
		{
			string text = spr.GetMeta("idle", "").AsString();
			if (text != "" && spr.SpriteFrames != null && spr.SpriteFrames.HasAnimation(text))
			{
				spr.Animation = text;
				spr.Play();
			}
		};
		return spr;
	}
}
