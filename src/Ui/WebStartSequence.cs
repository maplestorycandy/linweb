using System;
using System.Collections.Generic;
using Godot;
using IdleLineage.App;

namespace IdleLineage.Ui;

public sealed partial class WebStartSequence : Node2D
{
	private const string AssetRoot = "res://assets/start";

	private static readonly Dictionary<string, (int First, int Last)> Ranges = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
	{
		["prince"] = (714, 798),
		["princess"] = (629, 710),
		["m_knight"] = (378, 448),
		["f_knight"] = (315, 374),
		["m_mage"] = (531, 625),
		["f_mage"] = (452, 527),
		["m_elf"] = (245, 311),
		["f_elf"] = (166, 241),
		["m_dark"] = (90, 162),
		["f_dark"] = (25, 86),
		["m_illusionist"] = (968, 1036),
		["f_illusionist"] = (1039, 1125),
		["m_Dknight"] = (841, 905),
		["f_Dknight"] = (908, 965),
		["m_warrior"] = (1992, 2076),
		["f_warrior"] = (1908, 1991),
		["none"] = (1, 24)
	};

	private readonly Sprite2D _sprite = new Sprite2D();

	private Texture2D[] _frames = Array.Empty<Texture2D>();

	private double _stepSeconds = 0.082;

	private double _elapsed;

	private int _frame;

	private bool _animate;

	public void Init(string key, Vector2 containSize, bool animate, double stepSeconds, float extraScale = 1f)
	{
		key = (Ranges.ContainsKey(key) ? key : "prince");
		_frames = LoadFrames(key, animate);
		_animate = animate && _frames.Length > 1;
		_stepSeconds = Math.Max(0.01, stepSeconds);
		AddChild(_sprite, forceReadableName: false, InternalMode.Disabled);
		_sprite.Texture = _frames[0];
		_sprite.Centered = true;
		Vector2 size = _frames[0].GetSize();
		float num = Mathf.Min(containSize.X / Math.Max(1f, size.X), containSize.Y / Math.Max(1f, size.Y));
		num *= Math.Max(0.01f, extraScale);
		_sprite.Scale = Vector2.One * num;
		_sprite.Position = new Vector2(0f, (0f - size.Y) * num * 0.5f);
		SetProcess(_animate);
	}

	public override void _Process(double delta)
	{
		if (_animate && _frames.Length >= 2)
		{
			_elapsed += delta;
			while (_elapsed >= _stepSeconds)
			{
				_elapsed -= _stepSeconds;
				_frame = (_frame + 1) % _frames.Length;
				_sprite.Texture = _frames[_frame];
			}
		}
	}

	public static string KeyFor(ClassDef def, bool male)
	{
		return def.Id switch
		{
			"royal" => male ? "prince" : "princess", 
			"knight" => male ? "m_knight" : "f_knight", 
			"mage" => male ? "m_mage" : "f_mage", 
			"elf" => male ? "m_elf" : "f_elf", 
			"dark" => male ? "m_dark" : "f_dark", 
			"illusion" => male ? "m_illusionist" : "f_illusionist", 
			"dragon" => male ? "m_Dknight" : "f_Dknight", 
			"warrior" => male ? "m_warrior" : "f_warrior", 
			_ => male ? "prince" : "princess", 
		};
	}

	public static string KeyFor(ClassDef? def, string avatar, bool male)
	{
		string text = avatar switch
		{
			"王子" => "prince", 
			"公主" => "princess", 
			"男騎士" => "m_knight", 
			"女騎士" => "f_knight", 
			"男法師" => "m_mage", 
			"女法師" => "f_mage", 
			"男妖精" => "m_elf", 
			"女妖精" => "f_elf", 
			"男黑暗妖精" => "m_dark", 
			"女黑暗妖精" => "f_dark", 
			"男幻術士" => "m_illusionist", 
			"女幻術士" => "f_illusionist", 
			"男龍騎士" => "m_Dknight", 
			"女龍騎士" => "f_Dknight", 
			"男戰士" => "m_warrior", 
			"女戰士" => "f_warrior", 
			_ => "", 
		};
		if (text.Length <= 0)
		{
			if ((object)def != null)
			{
				return KeyFor(def, male);
			}
			if (!male)
			{
				return "princess";
			}
			return "prince";
		}
		return text;
	}

	public static bool IsWarrior(string key)
	{
		if (key == "m_warrior" || key == "f_warrior")
		{
			return true;
		}
		return false;
	}

	private static Texture2D[] LoadFrames(string key, bool animate)
	{
		var (num, num2) = Ranges[key];
		if (!animate)
		{
			string text = $"{"res://assets/start"}/{key}/{num}.png";
			return new Texture2D[1] { GD.Load<Texture2D>(text) ?? throw new InvalidOperationException("Unable to load Web start frame: " + text) };
		}
		Texture2D[] array = new Texture2D[num2 - num + 1];
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = $"{"res://assets/start"}/{key}/{num + i}.png";
			array[i] = GD.Load<Texture2D>(text2) ?? throw new InvalidOperationException("Unable to load Web start frame: " + text2);
		}
		return array;
	}
}
