using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed class L1jNpcCombatSpritePlayer
{
	private readonly IGameData _data;

	private readonly int _gfx;

	private readonly L1jGfxSprite _sprite;

	private readonly AnimatedSprite2D _body;

	private readonly AnimatedSprite2D? _shadow;

	private readonly IReadOnlyDictionary<string, AnimatedSprite2D> _layers;

	private string _action = "";

	private int _heading = -1;

	private long _lockedUntilMilliseconds;

	private bool _death;

	public Node2D Root { get; }

	public float ContentHeight { get; private set; }

	public double LastOneShotSeconds { get; private set; }

	public bool DeathFinished
	{
		get
		{
			if (_death)
			{
				return Time.GetTicksMsec() >= (ulong)Math.Max(0L, _lockedUntilMilliseconds);
			}
			return false;
		}
	}

	internal L1jNpcCombatSpritePlayer(Node2D root, IGameData data, int gfx, L1jGfxSprite sprite, int heading)
	{
		Root = root;
		_data = data;
		_gfx = gfx;
		_sprite = sprite;
		int num = sprite.ResolveHeading(heading);
		L1jSpriteAction action = sprite.Actions["idle"];
		int height;
		SpriteFrames frames = Load(action, num, loop: true, out height) ?? throw new InvalidDataException($"NPC gfx {gfx} idle_h{num} 沒有轉出影格。");
		_body = L1jNpcSpriteRenderer.CreateAnimatedSprite(frames, height, sprite.Box);
		L1jNpcSpriteRenderer.ApplyBlend(_body, sprite.Attr);
		Root.AddChild(_body, forceReadableName: false, Node.InternalMode.Disabled);
		float contentHeight2;
		if (!L1jNpcSpriteRenderer.TryMeasureNameplateAnchor(data, gfx, heading, out var contentHeight, out var _))
		{
			L1jSpriteBox box = sprite.Box;
			contentHeight2 = (((object)box != null) ? Math.Max(1, -box.Y0) : height);
		}
		else
		{
			contentHeight2 = contentHeight;
		}
		ContentHeight = contentHeight2;
		int height2;
		if (sprite.Shadow.HasValue)
		{
			SpriteFrames spriteFrames = Load(action, num, loop: true, out height2, shadow: true);
			if (spriteFrames != null)
			{
				_shadow = L1jNpcSpriteRenderer.CreateAnimatedSprite(spriteFrames, height, sprite.Box);
				_shadow.ZIndex = -1;
				Root.AddChild(_shadow, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		Dictionary<string, AnimatedSprite2D> dictionary = new Dictionary<string, AnimatedSprite2D>(StringComparer.Ordinal);
		foreach (L1jSpriteLayer renderedClothe in sprite.RenderedClothes)
		{
			SpriteFrames spriteFrames2 = Load(action, num, loop: true, out height2, shadow: false, renderedClothe.Suffix);
			if (spriteFrames2 != null)
			{
				AnimatedSprite2D animatedSprite2D = L1jNpcSpriteRenderer.CreateAnimatedSprite(spriteFrames2, height, sprite.Box);
				L1jNpcSpriteRenderer.ApplyBlend(animatedSprite2D, renderedClothe.Attr);
				Root.AddChild(animatedSprite2D, forceReadableName: false, Node.InternalMode.Disabled);
				dictionary[renderedClothe.Suffix] = animatedSprite2D;
			}
		}
		_layers = dictionary;
		_action = "idle";
		_heading = num;
	}

	public void DriveLoop(bool walking, int heading)
	{
		if (!_death && Time.GetTicksMsec() >= (ulong)Math.Max(0L, _lockedUntilMilliseconds))
		{
			string actionName = ((walking && _sprite.Actions.ContainsKey("walk")) ? "walk" : "idle");
			SetAction(actionName, heading, loop: true, lockDuration: false);
		}
	}

	public bool PlayAttack(int heading, double cycleSeconds = 0.0, double speedRatio = 1.0)
	{
		return PlayOneShot("attack", heading, death: false, cycleSeconds, speedRatio);
	}

	public bool PlayDamage(int heading)
	{
		return PlayOneShot("damage", heading, death: false);
	}

	public bool PlayDeath(int heading)
	{
		return PlayOneShot("death", heading, death: true);
	}

	private bool PlayOneShot(string action, int heading, bool death, double cycleSeconds = 0.0, double speedRatio = 1.0)
	{
		if (!_sprite.Actions.ContainsKey(action))
		{
			return false;
		}
		_death |= death;
		return SetAction(action, heading, loop: false, lockDuration: true, cycleSeconds, speedRatio);
	}

	private bool SetAction(string actionName, int heading, bool loop, bool lockDuration, double cycleSeconds = 0.0, double speedRatio = 1.0)
	{
		if (!_sprite.Actions.TryGetValue(actionName, out L1jSpriteAction value))
		{
			return false;
		}
		int num = _sprite.ResolveHeading(heading);
		if (_action == actionName && _heading == num && !lockDuration)
		{
			return true;
		}
		int height;
		SpriteFrames spriteFrames = Load(value, num, loop, out height);
		if (spriteFrames == null)
		{
			return false;
		}
		_body.SpriteFrames = spriteFrames;
		L1jNpcSpriteRenderer.ApplyAnchor(_body, height, _sprite.Box);
		_body.Play("default");
		int height2;
		if (_shadow != null)
		{
			SpriteFrames spriteFrames2 = Load(value, num, loop, out height2, shadow: true);
			_shadow.Visible = spriteFrames2 != null;
			if (spriteFrames2 != null)
			{
				_shadow.SpriteFrames = spriteFrames2;
				L1jNpcSpriteRenderer.ApplyAnchor(_shadow, height, _sprite.Box);
				_shadow.Play("default");
			}
		}
		foreach (L1jSpriteLayer renderedClothe in _sprite.RenderedClothes)
		{
			if (_layers.TryGetValue(renderedClothe.Suffix, out AnimatedSprite2D value2))
			{
				SpriteFrames spriteFrames3 = Load(value, num, loop, out height2, shadow: false, renderedClothe.Suffix);
				value2.Visible = spriteFrames3 != null;
				if (spriteFrames3 != null)
				{
					value2.SpriteFrames = spriteFrames3;
					L1jNpcSpriteRenderer.ApplyAnchor(value2, height, _sprite.Box);
					value2.Play("default");
				}
			}
		}
		_action = actionName;
		_heading = num;
		if (lockDuration)
		{
			int frameCount = spriteFrames.GetFrameCount("default");
			IReadOnlyList<double> ticks = value.Ticks;
			double num2 = ((ticks != null && ticks.Count > 0) ? value.Ticks.Sum() : ((double)(frameCount * L1jNpcSpriteCatalog.TicksDefault(_data)))) / (double)Math.Max(1, L1jNpcSpriteCatalog.TicksPerSecond(_data));
			double num3 = ActionFrameRules.OneShotSpeedScale(num2, cycleSeconds, speedRatio);
			_body.SpeedScale = (float)num3;
			if (_shadow != null)
			{
				_shadow.SpeedScale = (float)num3;
			}
			foreach (AnimatedSprite2D value3 in _layers.Values)
			{
				value3.SpeedScale = (float)num3;
			}
			long num4 = (long)Math.Ceiling(num2 / num3 * 1000.0);
			_lockedUntilMilliseconds = (long)Time.GetTicksMsec() + Math.Max(1L, num4);
			LastOneShotSeconds = Math.Max(0.001, (double)num4 / 1000.0);
		}
		else
		{
			_body.SpeedScale = 1f;
			if (_shadow != null)
			{
				_shadow.SpeedScale = 1f;
			}
			foreach (AnimatedSprite2D value4 in _layers.Values)
			{
				value4.SpeedScale = 1f;
			}
		}
		return true;
	}

	private SpriteFrames? Load(L1jSpriteAction action, int heading, bool loop, out int height, bool shadow = false, string? layerSuffix = null)
	{
		return L1jNpcSpriteRenderer.LoadFrames(_gfx, $"{action.Prefix}{(shadow ? "_s" : (layerSuffix ?? ""))}_h{heading}", action.Ticks, L1jNpcSpriteCatalog.TicksDefault(_data), L1jNpcSpriteCatalog.TicksPerSecond(_data), out height, loop);
	}
}
