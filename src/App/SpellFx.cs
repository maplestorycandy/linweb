using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed class SpellFx
{
	private sealed class Config
	{
		public string Dir = "";

		public string? Prefix;

		public string? DirPrefix;

		public int Dirs;

		public int N;

		public double Fps = 14.0;

		public bool Screen;

		public bool Proj;

		public double Ax = 0.5;

		public double Ay = 0.9;

		public double? W;

		public double? H;

		public bool Ground;

		public double ProjScale = 1.0;

		public string? ShadowPrefix;

		public string? ShadowDirPrefix;

		public string[] Layers = Array.Empty<string>();

		public Dictionary<string, string>? ByElePrefix;

		public bool OverHead;

		public int Gfx;

		public Config? Impact;
	}

	private sealed class Active
	{
		public Node2D Node;

		public Sprite2D Main;

		public Sprite2D? Shadow;

		public Sprite2D[] Extra = Array.Empty<Sprite2D>();

		public Texture2D?[] Frames = Array.Empty<Texture2D>();

		public Texture2D?[]? ShadowFrames;

		public Texture2D?[][] ExtraFrames = Array.Empty<Texture2D[]>();

		public double Fps;

		public int N;

		public double Elapsed;

		public int Frame;

		public bool Proj;

		public Vector2 From;

		public Vector2 To;

		public double Dur;

		public (string, Combatant) Key;

		public int[] Order = Array.Empty<int>();

		public double[] Ends = Array.Empty<double>();

		public double Delay;

		public int ResidueFrame = -1;

		public double ResidueSeconds;

		public int ResidueSound = -1;

		public bool InResidue;

		public string? SkillId;
	}

	private sealed class Timeline
	{
		public int[] Order = Array.Empty<int>();

		public double[] Ends = Array.Empty<double>();

		public double ReleaseSeconds;

		public int ResidueFrame = -1;

		public double ResidueSeconds;

		public int ResidueSound = -1;
	}

	private const float RefH = 112f;

	private const int ActiveCap = 60;

	private const double ResidueMinTicks = 20.0;

	private const int ResidueCap = 24;

	private static Dictionary<string, Config>? _configs;

	private static Dictionary<string, Config>? _selfConfigs;

	private static float _mScaleK = 0.009776786f;

	private static float _wK = 0.9375f;

	private static readonly Dictionary<string, Texture2D?[]> _frameCache = new Dictionary<string, Texture2D[]>();

	private readonly Node2D _arena;

	private readonly List<Active> _active = new List<Active>();

	private readonly HashSet<(string, Combatant)> _keys = new HashSet<(string, Combatant)>();

	private static Dictionary<int, Timeline>? _timelines;

	public SpellFx(Node2D arena)
	{
		_arena = arena;
	}

	private static Dictionary<string, Config> Configs()
	{
		if (_configs != null)
		{
			return _configs;
		}
		Dictionary<string, Config> dictionary = new Dictionary<string, Config>(StringComparer.Ordinal);
		GameData shared = GameDataProvider.Shared;
		if (shared.Table("SPELL_FX_REF_MSCALE_K") is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var value))
		{
			_mScaleK = (float)value;
		}
		if (shared.Table("SPELL_FX_REF_W_K") is JsonValue jsonValue2 && jsonValue2.TryGetValue<double>(out var value2))
		{
			_wK = (float)value2;
		}
		if (shared.Table("SPELL_FX") is JsonObject jsonObject)
		{
			bool value4 = default(bool);
			foreach (KeyValuePair<string, JsonNode> item in jsonObject)
			{
				item.Deconstruct(out var key, out var value3);
				string text = key;
				if (!(value3 is JsonObject jsonObject2))
				{
					continue;
				}
				Config config = new Config
				{
					Dir = (Str(jsonObject2, "dir") ?? text),
					Prefix = Str(jsonObject2, "prefix"),
					DirPrefix = Str(jsonObject2, "dirPrefix"),
					Dirs = (int)Num(jsonObject2, "dirs", 0.0),
					N = (int)Num(jsonObject2, "n", 1.0),
					Fps = Num(jsonObject2, "fps", 14.0),
					Screen = (Str(jsonObject2, "blend") == "screen"),
					Proj = (jsonObject2["proj"] is JsonValue jsonValue3 && jsonValue3.TryGetValue<bool>(out value4) && value4),
					W = OptNum(jsonObject2, "w"),
					H = OptNum(jsonObject2, "h"),
					Ground = (jsonObject2["targetVc"] != null),
					ProjScale = Num(jsonObject2, "projScale", 1.0),
					ShadowPrefix = Str(jsonObject2, "shadowPrefix"),
					ShadowDirPrefix = Str(jsonObject2, "shadowDirPrefix")
				};
				config.Gfx = GfxOf(config);
				config.Ax = Num(jsonObject2, "ax", 0.5);
				config.Ay = Num(jsonObject2, "ay", config.Proj ? 0.5 : 0.9);
				if (jsonObject2["layers"] is JsonArray jsonArray)
				{
					List<string> list = new List<string>();
					foreach (JsonNode item2 in jsonArray)
					{
						if (item2 is JsonValue jsonValue4 && jsonValue4.TryGetValue<string>(out string value5) && value5 != null)
						{
							list.Add(value5);
						}
					}
					config.Layers = list.ToArray();
				}
				if (jsonObject2["byEle"] is JsonObject jsonObject3)
				{
					config.ByElePrefix = new Dictionary<string, string>(StringComparer.Ordinal);
					foreach (KeyValuePair<string, JsonNode> item3 in jsonObject3)
					{
						item3.Deconstruct(out key, out value3);
						string key2 = key;
						if (value3 is JsonObject row)
						{
							string text2 = Str(row, "prefix");
							if (text2 != null)
							{
								config.ByElePrefix[key2] = text2;
							}
						}
					}
				}
				if (jsonObject2["impact"] is JsonObject row2 && (Str(row2, "prefix") != null || Str(row2, "dirPrefix") != null))
				{
					config.Impact = new Config
					{
						Dir = (Str(row2, "dir") ?? config.Dir),
						Prefix = Str(row2, "prefix"),
						DirPrefix = Str(row2, "dirPrefix"),
						Dirs = (int)Num(row2, "dirs", 0.0),
						ShadowPrefix = Str(row2, "shadowPrefix"),
						ShadowDirPrefix = Str(row2, "shadowDirPrefix"),
						N = (int)Num(row2, "n", 1.0),
						Fps = Num(row2, "fps", config.Fps),
						Screen = config.Screen,
						Ground = config.Ground,
						W = OptNum(row2, "w"),
						H = OptNum(row2, "h"),
						Ax = Num(row2, "ax", 0.5),
						Ay = Num(row2, "ay", 0.9)
					};
					config.Impact.Gfx = GfxOf(config.Impact);
				}
				dictionary[text] = config;
			}
		}
		_configs = dictionary;
		return dictionary;
	}

	private static Dictionary<string, Config> SelfConfigs()
	{
		if (_selfConfigs != null)
		{
			return _selfConfigs;
		}
		Dictionary<string, Config> dictionary = new Dictionary<string, Config>(StringComparer.Ordinal);
		if (GameDataProvider.Shared.Table("SELF_FX") is JsonObject jsonObject)
		{
			bool value = default(bool);
			foreach (var (text2, jsonNode2) in jsonObject)
			{
				if (jsonNode2 is JsonObject jsonObject2)
				{
					Config config = new Config
					{
						Dir = (Str(jsonObject2, "dir") ?? text2),
						Prefix = Str(jsonObject2, "prefix"),
						N = (int)Num(jsonObject2, "n", 1.0),
						Fps = Num(jsonObject2, "fps", 14.0),
						Screen = (Str(jsonObject2, "blend") == "screen"),
						H = OptNum(jsonObject2, "h"),
						OverHead = (jsonObject2["overHead"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value)
					};
					config.Gfx = GfxOf(config);
					dictionary[text2] = config;
				}
			}
		}
		_selfConfigs = dictionary;
		return dictionary;
	}

	public void PlaySelf(string? skillName, Combatant anchor)
	{
		if (skillName == null || !SelfConfigs().TryGetValue(skillName, out Config value) || Configs().ContainsKey(skillName) || _active.Count >= 60)
		{
			return;
		}
		(string, Combatant) tuple = ("self:" + skillName, anchor);
		if (_keys.Contains(tuple))
		{
			return;
		}
		string prefix = value.Prefix;
		if (prefix == null)
		{
			return;
		}
		Texture2D[] array = LoadFrames(value.Dir, prefix, value.N);
		if (array.Length == 0)
		{
			return;
		}
		Texture2D texture2D = array[0];
		if (texture2D == null)
		{
			return;
		}
		float num = texture2D.GetWidth();
		float num2 = texture2D.GetHeight();
		float num3 = 112f * (float)(value.H ?? 1.8);
		float num4 = num3 * num / num2;
		Vector2 position = EngineAdapter.ToVec(anchor.Pos) + new Vector2(0f, value.OverHead ? (-84f - num3 * 0.5f) : (-34f));
		Node2D node2D = new Node2D
		{
			Position = position,
			ZIndex = Depth.Of(position.Y + 34f, 5)
		};
		Sprite2D sprite2D = new Sprite2D
		{
			Texture = texture2D,
			Centered = true,
			Scale = new Vector2(num4 / num, num3 / num2)
		};
		if (value.Screen)
		{
			sprite2D.Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}
		node2D.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
		_arena.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
		_keys.Add(tuple);
		Timeline timeline = TimelineFor(value, array.Length);
		if (timeline != null)
		{
			Texture2D texture2D2 = array[timeline.Order[0]];
			if (texture2D2 != null)
			{
				sprite2D.Texture = texture2D2;
			}
		}
		_active.Add(new Active
		{
			Node = node2D,
			Main = sprite2D,
			Frames = array,
			Fps = value.Fps,
			N = value.N,
			Proj = false,
			Order = (timeline?.Order ?? Array.Empty<int>()),
			Ends = (timeline?.Ends ?? Array.Empty<double>()),
			Frame = ((timeline != null) ? timeline.Order[0] : 0),
			Key = tuple
		});
	}

	private static Dictionary<int, Timeline> Timelines()
	{
		if (_timelines != null)
		{
			return _timelines;
		}
		Dictionary<int, Timeline> dictionary = new Dictionary<int, Timeline>();
		string? diskPath = GodotDataFiles.TryResolveDiskPath("res://data/fx-sequence.json");
		string? jsonStr = diskPath != null && System.IO.File.Exists(diskPath)
			? System.IO.File.ReadAllText(diskPath)
			: (FileAccess.FileExists("res://data/fx-sequence.json") ? FileAccess.GetFileAsString("res://data/fx-sequence.json") : null);
		if (!string.IsNullOrEmpty(jsonStr) && JsonNode.Parse(jsonStr) is JsonObject jsonObject && jsonObject["timelines"] is JsonObject jsonObject2)
		{
			foreach (var (s, jsonNode2) in jsonObject2)
			{
				if (jsonNode2 is JsonObject jsonObject3 && int.TryParse(s, out var result) && jsonObject3["order"] is JsonArray jsonArray && jsonObject3["ticks"] is JsonArray jsonArray2 && jsonArray.Count != 0 && jsonArray.Count == jsonArray2.Count)
				{
					int[] array = new int[jsonArray.Count];
					double[] array2 = new double[jsonArray.Count];
					double num = 0.0;
					for (int i = 0; i < jsonArray.Count; i++)
					{
						array[i] = ((jsonArray[i] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value)) ? value : 0);
						double value2;
						double val = ((jsonArray2[i] is JsonValue jsonValue2 && jsonValue2.TryGetValue<double>(out value2)) ? value2 : 1.0);
						num = (array2[i] = num + Math.Max(1.0, val) / 25.0);
					}
					int num2 = ReadInt(jsonObject3, "release", -1);
					double releaseSeconds = ((num2 > 0 && num2 < array2.Length) ? array2[num2 - 1] : 0.0);
					double num3 = ReadInt(jsonObject3, "terminalTicks", 0);
					bool flag = num3 >= 20.0;
					dictionary[result] = new Timeline
					{
						Order = array,
						Ends = array2,
						ReleaseSeconds = releaseSeconds,
						ResidueFrame = (flag ? ReadInt(jsonObject3, "terminalFrame", -1) : (-1)),
						ResidueSeconds = (flag ? (num3 / 25.0) : 0.0),
						ResidueSound = (flag ? ReadInt(jsonObject3, "terminalSound", -1) : (-1))
					};
				}
			}
		}
		_timelines = dictionary;
		return dictionary;
	}

	private static int GfxOf(Config cfg)
	{
		string text = cfg.DirPrefix ?? cfg.Prefix;
		if (text == null)
		{
			return 0;
		}
		int num = text.IndexOf('-');
		if (num <= 0 || !int.TryParse(text.Substring(0, num), out var result))
		{
			return 0;
		}
		return result;
	}

	private static Timeline? TimelineFor(Config cfg, int frameCount)
	{
		if (cfg.Gfx == 0 || !Timelines().TryGetValue(cfg.Gfx, out Timeline value))
		{
			return null;
		}
		int[] order = value.Order;
		foreach (int num in order)
		{
			if (num < 0 || num >= frameCount)
			{
				return null;
			}
		}
		return value;
	}

	private static int ReadInt(JsonObject row, string key, int fallback)
	{
		if (!(row[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return fallback;
		}
		return value;
	}

	private static string? Str(JsonObject row, string key)
	{
		if (!(row[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}

	private static double Num(JsonObject row, string key, double fallback)
	{
		if (!(row[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return fallback;
		}
		return value;
	}

	private static double? OptNum(JsonObject row, string key)
	{
		if (!(row[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return null;
		}
		return value;
	}

	private static Texture2D?[] LoadFrames(string dir, string prefix, int n)
	{
		string key = dir + "/" + prefix;
		if (_frameCache.TryGetValue(key, out Texture2D[] value))
		{
			return value;
		}
		Texture2D[] array = new Texture2D[n];
		for (int i = 0; i < n; i++)
		{
			array[i] = ProjectileArt.LoadPng($"res://assets/fx/{dir}/{prefix}_{i}.png");
		}
		_frameCache[key] = array;
		return array;
	}

	public void Play(string? skillName, Combatant caster, Combatant target, string? skillId = null)
	{
		if (skillName == null)
		{
			return;
		}
		Dictionary<string, Config> dictionary = Configs();
		string text = skillName;
		if (skillId != null && dictionary.TryGetValue(skillId, out var value))
		{
			text = skillId;
		}
		else if (skillName != null && dictionary.TryGetValue(skillName, out value))
		{
			text = skillName;
		}
		else
		{
			string candidate = skillName ?? skillId ?? "";
			int dashIdx = candidate.IndexOf('-');
			if (dashIdx >= 0) candidate = candidate.Substring(dashIdx + 1).Trim();
			int colonIdx = candidate.IndexOf(':');
			if (colonIdx >= 0) candidate = candidate.Substring(colonIdx + 1).Trim();

			if (!string.IsNullOrEmpty(candidate) && dictionary.TryGetValue(candidate, out value))
			{
				text = candidate;
			}
			else
			{
				string? fallbackKey = null;
				if (candidate.Contains("沙蟲") || candidate.Contains("毒氣風暴") || candidate.Contains("sandworm")) fallbackKey = "沙蟲-毒氣風暴";
				else if (candidate.Contains("集體毒霧")) fallbackKey = "沙蟲-集體毒霧";
				else if (candidate.Contains("死神之怒") || candidate.Contains("13395")) fallbackKey = "死神之怒";
				else if (candidate.Contains("死亡收割") || candidate.Contains("13396")) fallbackKey = "死亡收割";
				else if (candidate.Contains("雷霆審判") || candidate.Contains("13397")) fallbackKey = "雷霆審判";
				else if (candidate.Contains("萬雷天引") || candidate.Contains("13398")) fallbackKey = "萬雷天引";
				else if (candidate.Contains("獄火煉獄") || candidate.Contains("13400")) fallbackKey = "獄火煉獄";
				else if (candidate.Contains("惡魔滅絕") || candidate.Contains("13401")) fallbackKey = "惡魔滅絕";
				else if (candidate.Contains("毒")) fallbackKey = "毒咒";
				else if (candidate.Contains("雷") || candidate.Contains("電")) fallbackKey = "雷霆風暴";
				else if (candidate.Contains("火") || candidate.Contains("炎") || candidate.Contains("爆")) fallbackKey = "火風暴";
				else if (candidate.Contains("冰") || candidate.Contains("凍") || candidate.Contains("雪")) fallbackKey = "冰雪暴";
				else if (candidate.Contains("風") || candidate.Contains("嵐") || candidate.Contains("刃")) fallbackKey = "風刃";
				else if (candidate.Contains("地") || candidate.Contains("石") || candidate.Contains("震")) fallbackKey = "地裂術";
				else if (candidate.Contains("光") || candidate.Contains("箭")) fallbackKey = "光箭";
				else if (candidate.Contains("暗") || candidate.Contains("黑") || candidate.Contains("影")) fallbackKey = "黑闇之影";
				else if (candidate.Contains("暈") || candidate.Contains("擊")) fallbackKey = "衝擊之暈";

				if (fallbackKey != null && dictionary.TryGetValue(fallbackKey, out var fbVal))
				{
					value = fbVal;
					text = fallbackKey;
				}
				else if (dictionary.TryGetValue("毒咒", out var defVal))
				{
					value = defVal;
					text = "毒咒";
				}
				else
				{
					return;
				}
			}
		}
		if (LiveCount() >= 60)
		{
			return;
		}
		(string, Combatant) tuple = (text, target);
		if (_keys.Contains(tuple))
		{
			return;
		}
		string value2 = value.Prefix;
		if (value.ByElePrefix != null && !value.ByElePrefix.TryGetValue(target.Element ?? "", out value2))
		{
			return;
		}
		Vector2 vector = EngineAdapter.ToVec(caster.Pos) + new Vector2(0f, -42f);
		Vector2 vector2 = EngineAdapter.ToVec(target.Pos) + new Vector2(0f, value.Ground ? (-4) : (-34));
		bool flip = false;
		int num = 0;
		string shadowPrefix = value.ShadowPrefix;
		if (value.DirPrefix != null && value.Dirs > 0)
		{
			Vector2 vector3 = vector2 - vector;
			num = ArpgActor.Vec2Dir(vector3.X, vector3.Y);
			int num2 = num;
			if (value.Dirs < 8)
			{
				(num2, flip) = num switch
				{
					1 => (1, false), 
					2 => (2, false), 
					3 => (3, false), 
					4 => (3, false), 
					5 => (1, false), 
					6 => (3, true), 
					7 => (3, true), 
					_ => (2, true), 
				};
			}
			value2 = value.DirPrefix + num2;
			if (value.ShadowDirPrefix != null)
			{
				shadowPrefix = value.ShadowDirPrefix + num2;
			}
		}
		if (value2 == null)
		{
			return;
		}
		Timeline timeline = Spawn(value, value2, shadowPrefix, flip, vector, vector2, tuple, 0.0, skillId);
		if (timeline == null)
		{
			return;
		}
		Config impact = value.Impact;
		if (impact == null)
		{
			return;
		}
		string text2 = impact.Prefix;
		string shadowPrefix2 = impact.ShadowPrefix;
		bool flip2 = false;
		if (impact.DirPrefix != null && impact.Dirs > 0)
		{
			int num3 = num;
			if (impact.Dirs < 8)
			{
				(num3, flip2) = num switch
				{
					1 => (1, false), 
					2 => (2, false), 
					3 => (3, false), 
					4 => (3, false), 
					5 => (1, false), 
					6 => (3, true), 
					7 => (3, true), 
					_ => (2, true), 
				};
			}
			text2 = impact.DirPrefix + num3;
			if (impact.ShadowDirPrefix != null)
			{
				shadowPrefix2 = impact.ShadowDirPrefix + num3;
			}
		}
		if (text2 != null)
		{
			double delaySeconds = (value.Proj ? Mathf.Clamp((double)vector.DistanceTo(vector2) / 1600.0, 0.14, 0.38) : timeline.ReleaseSeconds);
			Spawn(impact, text2, shadowPrefix2, flip2, vector, vector2, (text + "|impact", target), delaySeconds, skillId);
		}
	}

	private Timeline? Spawn(Config cfg, string prefix, string? shadowPrefix, bool flip, Vector2 from, Vector2 to, (string, Combatant) key, double delaySeconds, string? skillId)
	{
		if (_keys.Contains(key))
		{
			return null;
		}
		Texture2D[] array = LoadFrames(cfg.Dir, prefix, cfg.N);
		Node2D node;
		Vector2 sprOffset;
		Vector2 scaleVec;
		CanvasItemMaterial add;
		if (array.Length != 0)
		{
			Texture2D texture2D = array[0];
			if (texture2D != null)
			{
				float num = texture2D.GetWidth();
				float num2 = texture2D.GetHeight();
				float num4;
				float num5;
				if (cfg.Proj)
				{
					float num3 = 112f * _mScaleK * (float)cfg.ProjScale;
					num4 = num * num3;
					num5 = num2 * num3;
				}
				else
				{
					double? w = cfg.W;
					if (w.HasValue)
					{
						double valueOrDefault = w.GetValueOrDefault();
						num4 = 112f * _wK * (float)valueOrDefault;
						num5 = num4 * num2 / num;
					}
					else
					{
						num5 = 112f * (float)(cfg.H ?? 1.8);
						num4 = num5 * num / num2;
					}
				}
				Vector2 position = (cfg.Proj ? from : to);
				node = new Node2D
				{
					Position = position,
					ZIndex = Depth.Of(to.Y, 5)
				};
				sprOffset = new Vector2((float)(0.5 - cfg.Ax) * num4, (float)(0.5 - cfg.Ay) * num5);
				scaleVec = new Vector2(num4 / num, num5 / num2);
				add = (cfg.Screen ? new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				} : null);
				Texture2D[] array2 = null;
				Sprite2D sprite2D = null;
				if (shadowPrefix != null)
				{
					array2 = LoadFrames(cfg.Dir, shadowPrefix, cfg.N);
					Texture2D texture2D2 = array2[0];
					if (texture2D2 != null)
					{
						sprite2D = Make(texture2D2, blend: false);
					}
				}
				Sprite2D sprite2D2 = Make(texture2D, blend: true);
				List<Sprite2D> list = new List<Sprite2D>();
				List<Texture2D[]> list2 = new List<Texture2D[]>();
				string[] layers = cfg.Layers;
				foreach (string prefix2 in layers)
				{
					Texture2D[] array3 = LoadFrames(cfg.Dir, prefix2, cfg.N);
					Texture2D texture2D3 = array3[0];
					if (texture2D3 != null)
					{
						list.Add(Make(texture2D3, blend: true));
						list2.Add(array3);
					}
				}
				_arena.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
				if (delaySeconds > 0.0)
				{
					node.Visible = false;
				}
				double dur = 0.0;
				if (cfg.Proj)
				{
					dur = Mathf.Clamp((double)from.DistanceTo(to) / 1600.0, 0.14, 0.38);
				}
				_keys.Add(key);
				Timeline timeline = TimelineFor(cfg, array.Length);
				if (timeline != null)
				{
					int num6 = timeline.Order[0];
					Texture2D texture2D4 = array[num6];
					if (texture2D4 != null)
					{
						sprite2D2.Texture = texture2D4;
					}
					if (sprite2D != null)
					{
						Texture2D texture2D5 = ((array2 != null) ? array2[num6] : null);
						if (texture2D5 != null)
						{
							sprite2D.Texture = texture2D5;
						}
					}
					for (int j = 0; j < list.Count; j++)
					{
						Texture2D texture2D6 = list2[j][num6];
						if (texture2D6 != null)
						{
							list[j].Texture = texture2D6;
						}
					}
				}
				int num7 = timeline?.ResidueFrame ?? (-1);
				if (num7 >= array.Length || (num7 >= 0 && array[num7] == null))
				{
					num7 = -1;
				}
				_active.Add(new Active
				{
					Node = node,
					Main = sprite2D2,
					Shadow = sprite2D,
					Extra = list.ToArray(),
					Frames = array,
					ShadowFrames = array2,
					ExtraFrames = list2.ToArray(),
					Fps = cfg.Fps,
					N = cfg.N,
					Proj = cfg.Proj,
					From = from,
					To = to,
					Dur = dur,
					Order = (timeline?.Order ?? Array.Empty<int>()),
					Ends = (timeline?.Ends ?? Array.Empty<double>()),
					Frame = ((timeline != null) ? timeline.Order[0] : 0),
					Key = key,
					Delay = delaySeconds,
					ResidueFrame = (cfg.Proj ? (-1) : num7),
					ResidueSeconds = (timeline?.ResidueSeconds ?? 0.0),
					ResidueSound = (cfg.Proj ? (-1) : (timeline?.ResidueSound ?? (-1))),
					SkillId = skillId
				});
				return timeline;
			}
		}
		return null;
		Sprite2D Make(Texture2D tex, bool blend)
		{
			Sprite2D sprite2D3 = new Sprite2D
			{
				Texture = tex,
				Centered = true,
				Position = sprOffset,
				Scale = scaleVec,
				FlipH = flip
			};
			if (blend && add != null)
			{
				sprite2D3.Material = add;
			}
			node.AddChild(sprite2D3, forceReadableName: false, Node.InternalMode.Disabled);
			return sprite2D3;
		}
	}

	private int LiveCount()
	{
		int num = 0;
		foreach (Active item in _active)
		{
			if (!item.InResidue)
			{
				num++;
			}
		}
		return num;
	}

	public void Process(double dt)
	{
		for (int num = _active.Count - 1; num >= 0; num--)
		{
			Active active = _active[num];
			active.Elapsed += dt;
			double num2 = active.Elapsed - active.Delay;
			if (!(num2 < 0.0))
			{
				if (!active.Node.Visible)
				{
					active.Node.Visible = true;
				}
				bool flag = active.Order.Length != 0;
				double num3 = (flag ? active.Ends[^1] : 0.0);
				int num5;
				bool flag2;
				if (active.Proj)
				{
					float num4 = (float)Math.Min(1.0, num2 / active.Dur);
					Vector2 position = active.From.Lerp(active.To, num4);
					active.Node.Position = position;
					active.Node.ZIndex = Depth.Of(position.Y, 5);
					num5 = (flag ? active.Order[IndexAt(active.Ends, num2 % num3)] : ((int)(num2 * active.Fps) % active.N));
					flag2 = num4 >= 1f;
				}
				else if (active.InResidue)
				{
					num5 = active.ResidueFrame;
					flag2 = num2 >= num3 + active.ResidueSeconds;
				}
				else if (flag)
				{
					if (num2 < num3)
					{
						num5 = active.Order[IndexAt(active.Ends, num2)];
						flag2 = false;
					}
					else if (active.ResidueFrame >= 0)
					{
						EnterResidue(active);
						num5 = active.ResidueFrame;
						flag2 = false;
					}
					else
					{
						num5 = active.Frame;
						flag2 = true;
					}
				}
				else
				{
					num5 = (int)(num2 * active.Fps);
					flag2 = num5 >= active.N;
				}
				if (flag2)
				{
					if (GodotObject.IsInstanceValid(active.Node))
					{
						active.Node.QueueFree();
					}
					_keys.Remove(active.Key);
					_active.RemoveAt(num);
				}
				else if (num5 != active.Frame)
				{
					active.Frame = num5;
					Texture2D texture2D = active.Frames[num5];
					if (texture2D != null)
					{
						active.Main.Texture = texture2D;
					}
					if (active.Shadow != null)
					{
						Texture2D?[]? shadowFrames = active.ShadowFrames;
						Texture2D texture2D2 = ((shadowFrames != null) ? shadowFrames[num5] : null);
						if (texture2D2 != null)
						{
							active.Shadow.Texture = texture2D2;
						}
					}
					for (int i = 0; i < active.Extra.Length; i++)
					{
						Texture2D texture2D3 = active.ExtraFrames[i][num5];
						if (texture2D3 != null)
						{
							active.Extra[i].Texture = texture2D3;
						}
					}
				}
			}
		}
	}

	private void EnterResidue(Active a)
	{
		a.InResidue = true;
		_keys.Remove(a.Key);
		if (a.ResidueSound >= 0 && a.SkillId != null)
		{
			GameAudio.Instance?.PlaySkillImpact(a.SkillId);
		}
		int num = 0;
		for (int i = 0; i < _active.Count; i++)
		{
			if (_active[i].InResidue)
			{
				num++;
			}
		}
		while (num > 24)
		{
			for (int j = 0; j < _active.Count; j++)
			{
				if (_active[j].InResidue && _active[j] != a)
				{
					if (GodotObject.IsInstanceValid(_active[j].Node))
					{
						_active[j].Node.QueueFree();
					}
					_keys.Remove(_active[j].Key);
					_active.RemoveAt(j);
					num--;
					break;
				}
			}
		}
	}

	private static int IndexAt(double[] ends, double elapsed)
	{
		for (int i = 0; i < ends.Length; i++)
		{
			if (elapsed < ends[i])
			{
				return i;
			}
		}
		return ends.Length - 1;
	}
}
