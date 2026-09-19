using System;
using Godot;
using IdleLineage.App;
using IdleLineage.Combat;

namespace IdleLineage.Nodes;

public sealed class CombatantView
{
	private readonly AnimatedSprite2D? _body;

	private readonly AnimatedSprite2D? _shadow;

	private readonly Label _name;

	private readonly ColorRect _hpBg;

	private readonly ColorRect _hpFg;

	private readonly string _prefix;

	private readonly float _barW;

	private static readonly Color CHp = Color.FromHtml("#b5342f".AsSpan());

	private static readonly Color CText = Color.FromHtml("#c9d1de".AsSpan());

	public Combatant Model { get; }

	private CombatantView(Combatant model, AnimatedSprite2D? body, AnimatedSprite2D? shadow, Label name, ColorRect hpBg, ColorRect hpFg, string prefix, float barW)
	{
		Model = model;
		_body = body;
		_shadow = shadow;
		_name = name;
		_hpBg = hpBg;
		_hpFg = hpFg;
		_prefix = prefix;
		_barW = barW;
	}

	public static CombatantView Create(AtlasBridge atlas, Node2D arena, Control ui, Combatant model, string group, string atlasName, string prefix, Vector2 foot, float scale, bool showBar)
	{
		string text = Resolve(prefix, "idle");
		AnimatedSprite2D animatedSprite2D = atlas.MakeSprite(group, atlasName, text + "_s");
		AnimatedSprite2D animatedSprite2D2 = atlas.MakeSprite(group, atlasName, text);
		float num = 96f;
		AnimatedSprite2D[] array = new AnimatedSprite2D[2] { animatedSprite2D2, animatedSprite2D };
		foreach (AnimatedSprite2D animatedSprite2D3 in array)
		{
			if (animatedSprite2D3 != null)
			{
				animatedSprite2D3.Position = foot;
				animatedSprite2D3.Scale = new Vector2(scale, scale);
				animatedSprite2D3.ZIndex = (int)foot.Y;
				arena.AddChild(animatedSprite2D3, forceReadableName: false, Node.InternalMode.Disabled);
				if (animatedSprite2D3 == animatedSprite2D2)
				{
					num = (float)animatedSprite2D3.GetMeta("h", 96f).AsDouble();
				}
			}
		}
		if (animatedSprite2D != null)
		{
			animatedSprite2D.Modulate = new Color(1f, 1f, 1f, 0.45f);
			animatedSprite2D.ZIndex = (int)foot.Y - 1;
		}
		float num2 = foot.Y - num * scale - 8f;
		float num3 = 76f * scale;
		Label label = new Label
		{
			Text = $"{Disp(model)} Lv{model.Level}",
			Position = new Vector2(foot.X - 70f, num2 - 20f),
			Size = new Vector2(140f, 16f),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", CText);
		ui.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		ColorRect colorRect = new ColorRect
		{
			Color = Color.FromHtml("#0c0f15".AsSpan()),
			Position = new Vector2(foot.X - num3 * 0.5f, num2),
			Size = new Vector2(num3, 5f),
			Visible = showBar
		};
		ui.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		ColorRect colorRect2 = new ColorRect
		{
			Color = CHp,
			Position = colorRect.Position,
			Size = new Vector2(num3, 5f),
			Visible = showBar
		};
		ui.AddChild(colorRect2, forceReadableName: false, Node.InternalMode.Disabled);
		return new CombatantView(model, animatedSprite2D2, animatedSprite2D, label, colorRect, colorRect2, prefix, num3);
	}

	private static string Disp(Combatant c)
	{
		if (c.Disp.Length <= 0)
		{
			return c.Key;
		}
		return c.Disp;
	}

	private static string Resolve(string prefix, string action)
	{
		if (prefix == "")
		{
			return action;
		}
		if (action == "skill" || action == "death")
		{
			return action;
		}
		return prefix + "_" + action;
	}

	public void Play(string action)
	{
		PlayOn(_body, Resolve(_prefix, action));
		PlayOn(_shadow, Resolve(_prefix, action) + "_s");
	}

	private static void PlayOn(AnimatedSprite2D? s, string anim)
	{
		if (s != null && s.SpriteFrames != null && s.SpriteFrames.HasAnimation(anim))
		{
			s.Animation = anim;
			s.Frame = 0;
			s.Play();
		}
	}

	public void SetHpRatio(float r)
	{
		_hpFg.Size = new Vector2(_barW * Mathf.Clamp(r, 0f, 1f), _hpFg.Size.Y);
	}

	public void SetTargeted(bool on)
	{
		_name.Modulate = new Color(1f, 1f, 1f, on ? 1f : 0.62f);
	}

	public void PlayDeathAndFree()
	{
		_hpBg.Visible = false;
		_hpFg.Visible = false;
		if (_body != null)
		{
			_body.SetMeta("idle", "");
			PlayOn(_body, "death");
		}
		AnimatedSprite2D animatedSprite2D = _body ?? _shadow;
		if (animatedSprite2D == null)
		{
			Free();
			return;
		}
		Tween tween = animatedSprite2D.CreateTween();
		tween.TweenInterval(0.7);
		tween.TweenProperty(_body, "modulate:a", 0.0, 0.25);
		tween.TweenCallback(Callable.From(Free));
	}

	public void Free()
	{
		Node[] array = new Node[5] { _body, _shadow, _name, _hpBg, _hpFg };
		foreach (Node node in array)
		{
			if (node != null && GodotObject.IsInstanceValid(node))
			{
				node.QueueFree();
			}
		}
	}
}
