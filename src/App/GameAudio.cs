using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed partial class GameAudio : Node
{
	private const string MapPath = "res://data/audio-map.json";

	private const string SfxDir = "res://assets/sfx/";

	private const string BgmDir = "res://assets/bgm/";

	private const string CfgPath = "user://audio.cfg";

	private const string UiHookMeta = "idle_lineage_audio_hooked";

	private const int SfxVoices = 12;

	private const double FadeSeconds = 1.0;

	private const double ThrottleMobHurt = 90.0;

	private const double ThrottleMobAttack = 90.0;

	private const double ThrottleKill = 80.0;

	private const double ThrottleSkill = 90.0;

	private static readonly string[] SfxExtensions = new string[3] { ".ogg", ".wav", ".mp3" };

	public bool SfxOn = true;

	public bool BgmOn = true;

	public float SfxVolume = 0.5f;

	public float BgmVolume = 0.35f;

	private readonly Dictionary<string, AudioStream?> _streams = new Dictionary<string, AudioStream>();

	private readonly List<AudioStreamPlayer> _voices = new List<AudioStreamPlayer>();

	private int _voice;

	private readonly Dictionary<string, ulong> _lastPlay = new Dictionary<string, ulong>();

	private JsonObject? _sfxMap;

	private JsonObject? _bgmMap;

	private string[]? _mobAttackKeysByLength;

	private readonly Dictionary<string, int> _mobAttackCache = new Dictionary<string, int>();

	private AudioStreamPlayer[] _bgm = Array.Empty<AudioStreamPlayer>();

	private AudioStreamPlayer? _sting;

	private int _bgmActive = -1;

	private string _bgmScene = "";

	private double _fade;

	private readonly Dictionary<int, AudioStreamPlayer> _environmentLoops = new Dictionary<int, AudioStreamPlayer>();

	private readonly Dictionary<string, double> _environmentCountdown = new Dictionary<string, double>();

	private readonly RandomNumberGenerator _environmentRandom = new RandomNumberGenerator();

	private ClientEnvironmentSoundRule? _environmentRule;

	private int _environmentRuleOrder = -1;

	public static GameAudio? Instance { get; private set; }

	public string BgmScene => _bgmScene;

	public string LastAmbientScene { get; private set; } = "";

	public static GameAudio Attach(Node host)
	{
		if (Instance != null && GodotObject.IsInstanceValid(Instance))
		{
			return Instance;
		}
		GameAudio gameAudio = new GameAudio();
		host.AddChild(gameAudio, forceReadableName: false, InternalMode.Disabled);
		return gameAudio;
	}

	public override void _Ready()
	{
		Instance = this;
		_environmentRandom.Randomize();
		LoadConfig();
		LoadMap();
		for (int i = 0; i < 12; i++)
		{
			AudioStreamPlayer audioStreamPlayer = new AudioStreamPlayer
			{
				Bus = "Master"
			};
			AddChild(audioStreamPlayer, forceReadableName: false, InternalMode.Disabled);
			_voices.Add(audioStreamPlayer);
		}
		_bgm = new AudioStreamPlayer[2]
		{
			NewBgmPlayer(),
			NewBgmPlayer()
		};
		GetTree().NodeAdded += HookUiNode;
		HookUiTree(GetTree().Root);
	}

	public override void _ExitTree()
	{
		if (GetTree() != null)
		{
			GetTree().NodeAdded -= HookUiNode;
		}
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void HookUiTree(Node node)
	{
		HookUiNode(node);
		foreach (Node child in node.GetChildren())
		{
			HookUiTree(child);
		}
	}

	private void HookUiNode(Node node)
	{
		if (node is BaseButton baseButton && !baseButton.HasMeta("idle_lineage_audio_hooked"))
		{
			baseButton.SetMeta("idle_lineage_audio_hooked", true);
			baseButton.Pressed += delegate
			{
				PlayUi("button", 20.0, 0.42f);
			};
		}
	}

	private AudioStreamPlayer NewBgmPlayer()
	{
		AudioStreamPlayer p = new AudioStreamPlayer
		{
			Bus = "Master",
			VolumeDb = -80f
		};
		AddChild(p, forceReadableName: false, InternalMode.Disabled);
		p.Finished += delegate
		{
			if (p.Stream != null)
			{
				p.Play();
			}
		};
		return p;
	}

	private void LoadMap()
	{
		using FileAccess fileAccess = FileAccess.Open("res://data/audio-map.json", FileAccess.ModeFlags.Read);
		if (fileAccess == null)
		{
			GD.PushWarning("[audio] 找不到 res://data/audio-map.json——全程靜音");
		}
		else
		{
			if (!(JsonNode.Parse(fileAccess.GetAsText()) is JsonObject jsonObject))
			{
				return;
			}
			_sfxMap = jsonObject["sfx"] as JsonObject;
			_bgmMap = jsonObject["bgm"] as JsonObject;
			if (!(_sfxMap?["mobAttack"] is JsonObject jsonObject2))
			{
				return;
			}
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, JsonNode> item in jsonObject2)
			{
				list.Add(item.Key);
			}
			list.Sort((string a, string b) => (b.Length == a.Length) ? string.CompareOrdinal(a, b) : (b.Length - a.Length));
			_mobAttackKeysByLength = list.ToArray();
		}
	}

	private void LoadConfig()
	{
		using FileAccess fileAccess = FileAccess.Open("user://audio.cfg", FileAccess.ModeFlags.Read);
		if (fileAccess != null && JsonNode.Parse(fileAccess.GetAsText()) is JsonObject o)
		{
			SfxOn = Bool(o, "sfxOn", dflt: true);
			BgmOn = Bool(o, "bgmOn", dflt: true);
			SfxVolume = (float)Num(o, "sfxVol", 0.5);
			BgmVolume = (float)Num(o, "bgmVol", 0.35);
		}
	}

	public void SaveConfig()
	{
		using FileAccess fileAccess = FileAccess.Open("user://audio.cfg", FileAccess.ModeFlags.Write);
		fileAccess?.StoreString($"{{\"sfxOn\":{(SfxOn ? "true" : "false")},\"bgmOn\":{(BgmOn ? "true" : "false")},\"sfxVol\":{SfxVolume:0.###},\"bgmVol\":{BgmVolume:0.###}}}");
	}

	public void PlayEvent(string key)
	{
		if (_sfxMap?["events"]?[key] is JsonObject o)
		{
			string text = Str(o, "file");
			if (text.Length != 0 && Throttle("ev:" + key, Num(o, "throttleMs", 0.0)))
			{
				Emit(text, (float)Num(o, "vol", 0.5));
			}
		}
	}

	public void PlayUi(string key, double throttleMs = 0.0, float volume = 0.5f)
	{
		int num = Lookup("ui", key);
		if (num >= 0 && Throttle("ui:" + key, throttleMs))
		{
			Emit(num.ToString(), volume);
		}
	}

	public void PlayMobAttack(string mobKey, string mobName)
	{
		int num = Lookup("mobAttackByKey", mobKey);
		if (num < 0)
		{
			num = MobAttackId(mobName);
		}
		int num2 = Lookup("mobAttackSwing", mobName);
		if ((num >= 0 || num2 >= 0) && Throttle("mobAtk", 90.0))
		{
			if (num >= 0)
			{
				Emit(num.ToString(), 0.45f);
			}
			if (num2 >= 0)
			{
				Emit(num2.ToString(), 0.45f);
			}
		}
	}

	public void PlayMobHurt(string mobKey, string mobName)
	{
		int num = Lookup("mobHurtByKey", mobKey);
		if (num < 0)
		{
			num = Lookup("mobHurt", mobName);
		}
		if (num >= 0 && Throttle("mobHurt", 90.0))
		{
			Emit(num.ToString(), 0.5f);
		}
	}

	public void PlayMobKill(string mobKey, string mobName)
	{
		int num = Lookup("mobKillByKey", mobKey);
		if (num < 0)
		{
			num = Lookup("mobKill", mobName);
		}
		if (num < 0)
		{
			PlayEvent("kill");
		}
		else if (Throttle("kill", 80.0))
		{
			Emit(num.ToString(), 0.6f);
		}
	}

	public void PlayMobSkill(string mobName)
	{
		int num = Lookup("mobSkill", mobName);
		if (num >= 0 && Throttle("mobSkill", 90.0))
		{
			Emit(num.ToString(), 0.55f);
		}
	}

	public bool PlaySkillCast(string skillId, string? targetElement = null)
	{
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		int num = -1;
		string text = NormalizeElement(targetElement);
		if (text.Length > 0)
		{
			num = Lookup("skillVariant", skillId + ":" + text);
		}
		if (num < 0)
		{
			num = Lookup("skillCast", skillId);
		}
		if (num < 0 || !Throttle("skill:" + skillId, 90.0))
		{
			return false;
		}
		Emit(num.ToString(), 0.55f);
		return true;
	}

	public bool PlaySkillImpact(string skillId)
	{
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		int num = Lookup("skillImpact", skillId);
		if (num < 0 || !Throttle("skillHit:" + skillId, 90.0))
		{
			return false;
		}
		Emit(num.ToString(), 0.55f);
		return true;
	}

	public void PlayPartyHurt(Combatant target)
	{
		string text;
		switch (target.ClassId)
		{
		case "royal":
			text = "royal";
			break;
		case "mage":
		case "illusion":
			text = "mage";
			break;
		case "elf":
		case "dark":
			text = "elf";
			break;
		default:
			text = "knight";
			break;
		}
		string text2 = text;
		ClassDef classDef = ClassCatalog.Find(target.ClassId);
		bool flag = classDef != null && string.Equals(target.Avatar, classDef.FemaleAvatar, StringComparison.Ordinal);
		string file = "hurt_" + text2 + "_" + (flag ? "f" : "m");
		if (Throttle("partyHurt", 90.0))
		{
			Emit(file, 0.5f);
		}
	}

	public void PlayWeaponAttack(WeaponFamily? family)
	{
		int num = Lookup("weaponAttack", WeaponKey(family));
		if (num >= 0 && Throttle("wpnAtk", 60.0))
		{
			Emit(num.ToString(), 0.45f);
		}
	}

	public void PlayEquipment(string itemKey, string classId)
	{
		int? num = ClientAudioCatalog.ResolveEquipmentSound(GameDataProvider.Shared, itemKey, classId);
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			if (Throttle("equip", 40.0))
			{
				EmitClient(valueOrDefault, 0.55f);
			}
		}
	}

	private static string WeaponKey(WeaponFamily? f)
	{
		switch (f)
		{
		case WeaponFamily.OneHandSword:
			return "sword1";
		case WeaponFamily.TwoHandSword:
			return "sword2";
		case WeaponFamily.OneHandBlunt:
			return "blunt1";
		case WeaponFamily.TwoHandBlunt:
			return "blunt2";
		case WeaponFamily.OneHandSpear:
		case WeaponFamily.TwoHandSpear:
			return "spear";
		case WeaponFamily.Dagger:
			return "dagger";
		case WeaponFamily.Claw:
			return "claw";
		case WeaponFamily.DualBlades:
		case WeaponFamily.DualAxes:
			return "dual";
		case WeaponFamily.ChainSword:
			return "chainsword";
		case WeaponFamily.Bow:
			return "bow";
		case WeaponFamily.Crossbow:
			return "xbow";
		case WeaponFamily.Wand:
			return "wand";
		case WeaponFamily.Kiringku:
			return "qigu";
		default:
			return "unarmed";
		}
	}

	private int MobAttackId(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return -1;
		}
		if (_mobAttackCache.TryGetValue(name, out var value))
		{
			return value;
		}
		int num = Lookup("mobAttack", name);
		if (num < 0 && _sfxMap?["mobAttackAlias"] is JsonObject o)
		{
			string text = Str(o, name);
			if (text.Length > 0)
			{
				num = Lookup("mobAttack", text);
			}
		}
		if (num < 0 && _mobAttackKeysByLength != null)
		{
			string[] mobAttackKeysByLength = _mobAttackKeysByLength;
			foreach (string text2 in mobAttackKeysByLength)
			{
				if (text2.Length >= 2 && !string.Equals(text2, name, StringComparison.Ordinal) && name.Contains(text2))
				{
					num = Lookup("mobAttack", text2);
					break;
				}
			}
		}
		_mobAttackCache[name] = num;
		return num;
	}

	private int Lookup(string table, string key)
	{
		if (!(_sfxMap?[table]?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return -1;
		}
		return (int)value;
	}

	private static string NormalizeElement(string? element)
	{
		switch (element?.Trim().ToLowerInvariant())
		{
		case "earth":
		case "地":
		case "地屬性":
			return "earth";
		case "water":
		case "水":
		case "水屬性":
			return "water";
		case "火":
		case "火屬性":
		case "fire":
			return "fire";
		case "風":
		case "風屬性":
		case "wind":
			return "wind";
		default:
			return "";
		}
	}

	private bool Throttle(string key, double ms)
	{
		if (!SfxOn)
		{
			return false;
		}
		ulong ticksMsec = Time.GetTicksMsec();
		if (ms > 0.0 && _lastPlay.TryGetValue(key, out var value) && ticksMsec - value < (ulong)ms)
		{
			return false;
		}
		_lastPlay[key] = ticksMsec;
		return true;
	}

	private void Emit(string file, float vol)
	{
		if (SfxOn)
		{
			AudioStream audioStream = SfxStream(file);
			if (audioStream != null)
			{
				_voice = (_voice + 1) % _voices.Count;
				AudioStreamPlayer audioStreamPlayer = _voices[_voice];
				audioStreamPlayer.Stream = audioStream;
				audioStreamPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(vol * SfxVolume, 0.0001f, 1f));
				audioStreamPlayer.Play();
			}
		}
	}

	private void EmitClient(int soundId, float vol)
	{
		if (SfxOn)
		{
			AudioStream audioStream = ClientSfxStream(soundId);
			if (audioStream != null)
			{
				_voice = (_voice + 1) % _voices.Count;
				AudioStreamPlayer audioStreamPlayer = _voices[_voice];
				audioStreamPlayer.Stream = audioStream;
				audioStreamPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(vol * SfxVolume, 0.0001f, 1f));
				audioStreamPlayer.Play();
			}
		}
	}

	private AudioStream? ClientSfxStream(int soundId)
	{
		return Stream("res://assets/sfx/" + soundId + ".wav") ?? SfxStream(soundId.ToString());
	}

	private AudioStream? SfxStream(string file)
	{
		string[] sfxExtensions = SfxExtensions;
		foreach (string text in sfxExtensions)
		{
			AudioStream audioStream = Stream("res://assets/sfx/" + file + text);
			if (audioStream != null)
			{
				return audioStream;
			}
		}
		return null;
	}

	private AudioStream? Stream(string path)
	{
		if (_streams.TryGetValue(path, out AudioStream value))
		{
			return value;
		}
		value = (ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path, null, ResourceLoader.CacheMode.Reuse) : null);
		_streams[path] = value;
		return value;
	}

	public void SetEnvironment(string mapKey, int gameX, int gameY, bool night, string weather = "")
	{
		ClientEnvironmentSoundRule clientEnvironmentSoundRule = ClientAudioCatalog.ResolveEnvironment(GameDataProvider.Shared, mapKey, gameX, gameY, night, weather);
		int num = clientEnvironmentSoundRule?.SourceOrder ?? (-1);
		if (num == _environmentRuleOrder)
		{
			return;
		}
		ClearEnvironment();
		if ((object)clientEnvironmentSoundRule == null)
		{
			return;
		}
		_environmentRule = clientEnvironmentSoundRule;
		_environmentRuleOrder = num;
		foreach (int soundId in clientEnvironmentSoundRule.MainSounds)
		{
			AudioStream audioStream = ClientSfxStream(soundId);
			if (audioStream == null)
			{
				continue;
			}
			AudioStreamPlayer player = new AudioStreamPlayer
			{
				Bus = "Master",
				Stream = audioStream
			};
			AddChild(player, forceReadableName: false, InternalMode.Disabled);
			player.Finished += delegate
			{
				if (SfxOn && _environmentLoops.ContainsKey(soundId))
				{
					player.Play();
				}
			};
			_environmentLoops[soundId] = player;
			if (SfxOn)
			{
				player.Play();
			}
		}
		foreach (string group in clientEnvironmentSoundRule.Groups)
		{
			ClientEnvironmentSoundGroup clientEnvironmentSoundGroup = ClientAudioCatalog.EnvironmentGroup(GameDataProvider.Shared, group);
			if ((object)clientEnvironmentSoundGroup != null && clientEnvironmentSoundGroup.Sounds.Count > 0)
			{
				_environmentCountdown[group] = clientEnvironmentSoundGroup.IntervalSeconds;
			}
		}
	}

	public void ClearEnvironment()
	{
		foreach (AudioStreamPlayer value in _environmentLoops.Values)
		{
			value.Stop();
			value.QueueFree();
		}
		_environmentLoops.Clear();
		_environmentCountdown.Clear();
		_environmentRule = null;
		_environmentRuleOrder = -1;
	}

	private void AdvanceEnvironment(double delta)
	{
		float linear = Mathf.Clamp(0.35f * SfxVolume, 0.0001f, 1f);
		foreach (AudioStreamPlayer value in _environmentLoops.Values)
		{
			value.VolumeDb = Mathf.LinearToDb(linear);
			if (SfxOn)
			{
				if (!value.Playing)
				{
					value.Play();
				}
			}
			else if (value.Playing)
			{
				value.Stop();
			}
		}
		if (!SfxOn || (object)_environmentRule == null)
		{
			return;
		}
		foreach (string item in new List<string>(_environmentCountdown.Keys))
		{
			double num = _environmentCountdown[item] - delta;
			if (num > 0.0)
			{
				_environmentCountdown[item] = num;
				continue;
			}
			ClientEnvironmentSoundGroup clientEnvironmentSoundGroup = ClientAudioCatalog.EnvironmentGroup(GameDataProvider.Shared, item);
			if ((object)clientEnvironmentSoundGroup == null || clientEnvironmentSoundGroup.Sounds.Count == 0)
			{
				_environmentCountdown.Remove(item);
				continue;
			}
			int index = _environmentRandom.RandiRange(0, clientEnvironmentSoundGroup.Sounds.Count - 1);
			EmitClient(clientEnvironmentSoundGroup.Sounds[index], 0.35f);
			_environmentCountdown[item] = Math.Max(1.0, clientEnvironmentSoundGroup.IntervalSeconds);
		}
	}

	public void PlayScene(string scene)
	{
		if (scene == _bgmScene)
		{
			return;
		}
		string value;
		string text = ((_bgmMap?["tracks"]?[scene] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? value : scene);
		AudioStream audioStream = Stream("res://assets/bgm/" + text + ".mp3");
		if (audioStream != null)
		{
			_bgmScene = scene;
			if ((!(scene == "battle") && !(scene == "boss")) || 1 == 0)
			{
				LastAmbientScene = scene;
			}
			int num = ((_bgmActive == 0) ? 1 : 0);
			_bgm[num].Stream = audioStream;
			_bgm[num].VolumeDb = -80f;
			_bgm[num].Play();
			_bgmActive = num;
			_fade = 1.0;
		}
	}

	public void PlaySting(string? track)
	{
		if (!BgmOn || string.IsNullOrEmpty(track))
		{
			return;
		}
		AudioStream audioStream = Stream("res://assets/bgm/" + track + ".mp3");
		if (audioStream != null)
		{
			if (_sting == null)
			{
				_sting = CreateChild<AudioStreamPlayer>();
			}
			_sting.Stream = audioStream;
			_sting.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(BgmVolume, 0.0001f, 1f));
			_sting.Play();
		}
	}

	private T CreateChild<T>() where T : AudioStreamPlayer, new()
	{
		T val = new T
		{
			Bus = "Master"
		};
		AddChild(val, forceReadableName: false, InternalMode.Disabled);
		return val;
	}

	public string? HuntTrack(string mapKey)
	{
		if (!(_bgmMap?["hunt"]?[mapKey] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}

	public string TownScene(string townKey)
	{
		if (_bgmMap?["tracks"]?[townKey] == null)
		{
			return "town";
		}
		return townKey;
	}

	public string CreateScene(string classId)
	{
		if (_bgmMap?["create"]?[classId] == null)
		{
			return "create";
		}
		return "create_" + classId;
	}

	public override void _Process(double delta)
	{
		AdvanceEnvironment(delta);
		if (_bgm.Length < 2)
		{
			return;
		}
		float num = (BgmOn ? Mathf.Clamp(BgmVolume, 0.0001f, 1f) : 0.0001f);
		if (_sting != null && _sting.Playing)
		{
			_sting.VolumeDb = Mathf.LinearToDb(num);
		}
		if (_fade > 0.0)
		{
			_fade = Mathf.Max(0.0, _fade - delta);
			float num2 = (float)(1.0 - _fade / 1.0);
			SetBgmVol(_bgmActive, num * num2);
			SetBgmVol(1 - _bgmActive, num * (1f - num2));
			if (_fade <= 0.0 && _bgmActive >= 0)
			{
				_bgm[1 - _bgmActive].Stop();
			}
		}
		else if (_bgmActive >= 0)
		{
			SetBgmVol(_bgmActive, num);
		}
	}

	private void SetBgmVol(int i, float linear)
	{
		if (i >= 0 && i < _bgm.Length)
		{
			_bgm[i].VolumeDb = Mathf.LinearToDb(Mathf.Clamp(linear, 0.0001f, 1f));
		}
	}

	private static string Str(JsonObject o, string k)
	{
		if (!(o[k] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value;
	}

	private static double Num(JsonObject o, string k, double dflt)
	{
		if (!(o[k] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return dflt;
		}
		return value;
	}

	private static bool Bool(JsonObject o, string k, bool dflt)
	{
		if (!(o[k] is JsonValue jsonValue) || !jsonValue.TryGetValue<bool>(out var value))
		{
			return dflt;
		}
		return value;
	}
}
