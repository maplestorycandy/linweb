using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public sealed class ArpgActor
{
	public Vector2 Pos;

	public Vector2? MoveTarget;

	public double Hp;

	public double MaxHp;

	public double Mp;

	public double MaxMp;

	public bool IsPlayer;
	public bool IsRemote;

	public bool Dead;

	public int MinimumWorldDepth = int.MinValue;

	public float Radius = 22f;

	public float WalkHold;

	public float MoveSpeed = 150f;

	public float WalkRefSpeed = (float)IsometricMovementRules.BaseMoveSpeed;

	public float WalkAnimRef;

	public float AttackRange = 60f;

	public double AttackInterval = 1.0;

	public double AtkCd;

	public double Damage = 20.0;

	public int Level = 1;

	public string Disp = "";

	public double DeadTimer;

	public bool Ranged;

	public bool RangedArrow;

	public Color BoltColor = Color.FromHtml("#ce93d8".AsSpan());

	private readonly AnimatedSprite2D? _body;

	private readonly AnimatedSprite2D? _shadow;

	private readonly AnimatedSprite2D[] _clothes;

	private readonly AnimatedSprite2D? _effect;

	private readonly Label _name;
	private readonly Label _title;
	private readonly Label _status;
	private readonly Node2D? _hpBarNode;
	private readonly ColorRect? _hpBarFill;
	private readonly Node2D? _mpBarNode;
	private readonly ColorRect? _mpBarFill;

	private string _prefix;

	private readonly float _scale;

	private readonly float _contentH;

	private string _loop = "";

	private bool _busy;

	private bool _abnormalLocked;

	private L1jPoisonVisual _poisonVisual;

	private Vector2 _walkMicro;

	private Vector2 _walkVisual;

	private int _walkStrideVariant;

	private bool _walkStrideStepActive;

	private bool _walkStrideStarted;

	private float _walkStrideLastProgress;

	private static readonly Vector2[] GridStepPixels = new Vector2[8]
	{
		new Vector2(-24f, -12f),
		new Vector2(0f, -24f),
		new Vector2(24f, -12f),
		new Vector2(48f, 0f),
		new Vector2(24f, 12f),
		new Vector2(0f, 24f),
		new Vector2(-24f, 12f),
		new Vector2(-48f, 0f)
	};


	private float _visibilityAlpha = 1f;

	private bool _combatNameOnly;

	private double _combatNameRemaining;

	private AtlasBridge _atlas;

	private string _group = "";

	private string _atlasBase = "";

	private bool _eightDir;

	private bool _mobDir;

	private int _curDir = 2;

	private float _busyWatchdog;

	private const int MobInitDir = 5;

	private readonly SpriteFrames?[] _dirFrames = new SpriteFrames[8];

	private static readonly string[] Sfx8 = new string[8] { "2", "F", "", "d3", "d4", "d5", "d6", "d7" };

	private static readonly string[] Sfx3 = new string[8] { "2", "F", "", "", "", "", "2", "2" };

	private bool _threeDirMorph;

	private bool _supportsWeaponPrefixes;

	private string[] _attackVariants = new string[1] { "attack" };

	private static readonly Color CText = Color.FromHtml("#c9d1de".AsSpan());

	private static readonly Color CStatus = Color.FromHtml("#e6b566".AsSpan());

	private SpriteFrames? _walkPresenceFrames;

	private string _walkPresencePrefix = "";

	private int _walkPresenceDir = -1;

	private bool _walkPresenceMorph;

	private bool _walkPresenceValue;

	private SpriteFrames? _walkTableFrames;

	private string _walkTablePrefix = "";

	private int _walkTableDir = -1;

	private bool _walkTableMorph;

	private string _walkTableAnim = "";

	private float[]? _walkTableCumulative;

	public const float CorpseSeconds = 10f;

	private bool _occluded;

	private float _occludeAlpha = 1f;

	private bool _everSynced;

	private const float OccludeFadeOutSeconds = 0.25f;

	private const float OccludeFadeInSeconds = 0.15f;

	public double LastOneShotSeconds { get; private set; }

	public string VisualKey { get; set; } = "";

	public bool FixedFacing { get; set; }

	public bool HasMobDir => _mobDir;

	public bool HasEightDir => _eightDir;

	private ArpgActor(AnimatedSprite2D? body, AnimatedSprite2D? shadow, AnimatedSprite2D[] clothes, AnimatedSprite2D? effect, Label name, Label title, Label status, Node2D? hpBarNode, ColorRect? hpBarFill, Node2D? mpBarNode, ColorRect? mpBarFill, string prefix, float scale, float contentH)
	{
		_body = body;
		_shadow = shadow;
		_clothes = clothes;
		_effect = effect;
		_name = name;
		_title = title;
		_status = status;
		_hpBarNode = hpBarNode;
		_hpBarFill = hpBarFill;
		_mpBarNode = mpBarNode;
		_mpBarFill = mpBarFill;
		_prefix = prefix;
		_scale = scale;
		_contentH = contentH;
	}

	public bool ContainsVisibleBodyPoint(Vector2 worldPoint, float worldPadding = 6f)
	{
		if (ContainsVisibleFramePoint(_body, worldPoint, worldPadding))
		{
			return true;
		}
		AnimatedSprite2D[] clothes = _clothes;
		for (int i = 0; i < clothes.Length; i++)
		{
			if (ContainsVisibleFramePoint(clothes[i], worldPoint, worldPadding))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ContainsVisibleFramePoint(AnimatedSprite2D? sprite, Vector2 worldPoint, float worldPadding)
	{
		if (sprite == null || !sprite.Visible || sprite.SpriteFrames == null)
		{
			return false;
		}
		SpriteFrames spriteFrames = sprite.SpriteFrames;
		StringName animation = sprite.Animation;
		if (!spriteFrames.HasAnimation(animation))
		{
			return false;
		}
		int frameCount = spriteFrames.GetFrameCount(animation);
		if (frameCount <= 0 || sprite.Frame < 0 || sprite.Frame >= frameCount)
		{
			return false;
		}
		Texture2D frameTexture = spriteFrames.GetFrameTexture(animation, sprite.Frame);
		if (frameTexture == null)
		{
			return false;
		}
		Rect2 rect;
		if (frameTexture is AtlasTexture { Region: var region, Margin: var margin })
		{
			float num = margin.Position.X;
			if (sprite.FlipH)
			{
				num = (float)frameTexture.GetWidth() - num - region.Size.X;
			}
			rect = new Rect2(sprite.Offset + new Vector2(num, margin.Position.Y), region.Size);
		}
		else
		{
			rect = new Rect2(sprite.Offset, new Vector2(frameTexture.GetWidth(), frameTexture.GetHeight()));
		}
		float num2 = Mathf.Max(0.001f, Mathf.Min(Mathf.Abs(sprite.Scale.X), Mathf.Abs(sprite.Scale.Y)));
		float num3 = Mathf.Max(0f, worldPadding) / num2;
		return rect.Grow(num3).HasPoint(sprite.ToLocal(worldPoint));
	}

	private static AnimatedSprite2D? MakeClothesLayer(AtlasBridge atlas, string group, string atlasName, string idleAction, string suffix, float scale, Node2D arena, Vector2 bodyOffset)
	{
		string text = null;
		foreach (string item in atlas.ActionNames(group, atlasName))
		{
			if (item.EndsWith(suffix, StringComparison.Ordinal))
			{
				text = item;
				if (item == idleAction + suffix)
				{
					break;
				}
			}
		}
		if (text == null)
		{
			return null;
		}
		AnimatedSprite2D animatedSprite2D = atlas.MakeSprite(group, atlasName, idleAction + suffix) ?? atlas.MakeSprite(group, atlasName, text);
		if (animatedSprite2D == null)
		{
			return null;
		}
		animatedSprite2D.SetMeta("idle", "");
		animatedSprite2D.Offset = bodyOffset;
		animatedSprite2D.Scale = new Vector2(scale, scale);
		if (MonsterClothesBlend(atlasName, suffix) == "add")
		{
			animatedSprite2D.Modulate = Colors.White;
			animatedSprite2D.Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}
		else
		{
			animatedSprite2D.Modulate = Colors.White;
		}
		arena.AddChild(animatedSprite2D, forceReadableName: false, Node.InternalMode.Disabled);
		return animatedSprite2D;
	}

	private static string MonsterClothesBlend(string atlasName, string suffix)
	{
		int result;
		int num = ((!(suffix == "_w")) ? ((int.TryParse(suffix.AsSpan(2), out result) && result >= 2) ? (result - 1) : 0) : 0);
		if (GameDataProvider.Shared.Table("L1J_MOB_SPRITES") is JsonObject jsonObject && jsonObject["byAtlas"]?[atlasName]?["renderedClothes"] is JsonArray jsonArray && num < jsonArray.Count && jsonArray[num]?["blend"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return "mix";
	}

	private static string ClothesSuffix(int index)
	{
		if (index != 0)
		{
			return $"_w{index + 1}";
		}
		return "_w";
	}

	private static bool IsAdditiveBody(string group, string atlasName)
	{
		if (group != "anim" || !(GameDataProvider.Shared.Table("L1J_MOB_SPRITES") is JsonObject jsonObject) || !(jsonObject["byAtlas"]?[atlasName]?["attr"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return false;
		}
		return (value & 8) != 0;
	}

	public static ArpgActor Create(AtlasBridge atlas, Node2D arena, Control ui, string group, string atlasName, string prefix, bool isPlayer, float scale, bool threeDirMorph = false)
	{
		bool flag = atlas.HasAction(group, atlasName, "d0/idle");
		int num = ((!flag || atlas.HasAction(group, atlasName, $"d{5}/idle")) ? 5 : 0);
		string text = (flag ? $"d{num}/idle" : (isPlayer ? Resolve(prefix, "idle") : "idle"));
		AnimatedSprite2D animatedSprite2D = (atlas.HasAction(group, atlasName, text + "_s") ? atlas.MakeSprite(group, atlasName, text + "_s") : null);
		AnimatedSprite2D animatedSprite2D2 = atlas.MakeSprite(group, atlasName, text);
		if (flag && animatedSprite2D2 != null && animatedSprite2D != null)
		{
			animatedSprite2D.Offset = animatedSprite2D2.Offset;
		}
		float contentH = 96f;
		AnimatedSprite2D[] array = new AnimatedSprite2D[2] { animatedSprite2D2, animatedSprite2D };
		foreach (AnimatedSprite2D animatedSprite2D3 in array)
		{
			if (animatedSprite2D3 != null)
			{
				animatedSprite2D3.SetMeta("idle", "");
				animatedSprite2D3.Scale = new Vector2(scale, scale);
				arena.AddChild(animatedSprite2D3, forceReadableName: false, Node.InternalMode.Disabled);
				if (animatedSprite2D3 == animatedSprite2D2)
				{
					contentH = (float)animatedSprite2D3.GetMeta("h", 96f).AsDouble();
				}
			}
		}
		if (animatedSprite2D != null)
		{
			animatedSprite2D.Modulate = new Color(1f, 1f, 1f, 0.4f);
		}
		if (animatedSprite2D2 != null && IsAdditiveBody(group, atlasName))
		{
			animatedSprite2D2.Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}
		if (animatedSprite2D2 != null)
		{
			animatedSprite2D2.AnimationFinished += delegate
			{
			};
		}
		List<AnimatedSprite2D> list = new List<AnimatedSprite2D>();
		if ((!isPlayer || flag || threeDirMorph) && animatedSprite2D2 != null)
		{
			int num2 = 0;
			while (true)
			{
				string suffix = ClothesSuffix(num2);
				AnimatedSprite2D animatedSprite2D4 = MakeClothesLayer(atlas, group, atlasName, text, suffix, scale, arena, animatedSprite2D2.Offset);
				if (animatedSprite2D4 == null)
				{
					break;
				}
				list.Add(animatedSprite2D4);
				num2++;
			}
		}
		AnimatedSprite2D effect = null;
		if ((!isPlayer || flag || threeDirMorph) && animatedSprite2D2 != null)
		{
			string text2 = null;
			foreach (string item in atlas.ActionNames(group, atlasName))
			{
				if (item.EndsWith("_effect", StringComparison.Ordinal))
				{
					text2 = item;
					break;
				}
			}
			if (text2 != null && (effect = atlas.MakeSprite(group, atlasName, text2)) != null)
			{
				effect.SetMeta("idle", "");
				effect.Offset = animatedSprite2D2.Offset;
				effect.Scale = new Vector2(scale, scale);
				effect.Visible = false;
				effect.Stop();
				arena.AddChild(effect, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		Label label = new Label
		{
			Size = new Vector2(140f, 16f),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ClassicWorldText.Apply(label, CText, 12);
		ui.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		Label titleLabel = new Label
		{
			Size = new Vector2(180f, 16f),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false
		};
		ClassicWorldText.Apply(titleLabel, Color.FromHtml("#b8dce5".AsSpan()), 11);
		ui.AddChild(titleLabel, forceReadableName: false, Node.InternalMode.Disabled);
		Label label2 = new Label
		{
			Size = new Vector2(160f, 14f),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false
		};
		ClassicWorldText.Apply(label2, CStatus, 11);
		ui.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		Node2D hpBarNode = new Node2D();
		ColorRect hpBarBorder = new ColorRect
		{
			Size = new Vector2(44f, 6f),
			Color = Color.FromHtml("#0a0e17"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ColorRect hpBarBg = new ColorRect
		{
			Position = new Vector2(1f, 1f),
			Size = new Vector2(42f, 4f),
			Color = Color.FromHtml("#1e293b"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ColorRect hpBarFill = new ColorRect
		{
			Position = new Vector2(1f, 1f),
			Size = new Vector2(42f, 4f),
			Color = Color.FromHtml("#ef4444"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hpBarNode.AddChild(hpBarBorder);
		hpBarNode.AddChild(hpBarBg);
		hpBarNode.AddChild(hpBarFill);

		for (int i = 1; i <= 3; i++)
		{
			float x = 1f + Mathf.Round(42f * (i * 0.25f));
			ColorRect div = new ColorRect
			{
				Position = new Vector2(x, 1f),
				Size = new Vector2(1f, 4f),
				Color = Color.FromHtml("#0a0e17"),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			hpBarNode.AddChild(div);
		}

		Node2D mpBarNode = new Node2D { Position = new Vector2(0f, 6f) };
		ColorRect mpBarBorder = new ColorRect
		{
			Size = new Vector2(44f, 6f),
			Color = Color.FromHtml("#0a0e17"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ColorRect mpBarBg = new ColorRect
		{
			Position = new Vector2(1f, 1f),
			Size = new Vector2(42f, 4f),
			Color = Color.FromHtml("#1e293b"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ColorRect mpBarFill = new ColorRect
		{
			Position = new Vector2(1f, 1f),
			Size = new Vector2(42f, 4f),
			Color = Color.FromHtml("#00b0ff"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		mpBarNode.AddChild(mpBarBorder);
		mpBarNode.AddChild(mpBarBg);
		mpBarNode.AddChild(mpBarFill);

		for (int i = 1; i <= 3; i++)
		{
			float x = 1f + Mathf.Round(42f * (i * 0.25f));
			ColorRect div = new ColorRect
			{
				Position = new Vector2(x, 1f),
				Size = new Vector2(1f, 4f),
				Color = Color.FromHtml("#0a0e17"),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			mpBarNode.AddChild(div);
		}
		hpBarNode.AddChild(mpBarNode);

		arena.AddChild(hpBarNode, forceReadableName: false, Node.InternalMode.Disabled);

		ArpgActor a = new ArpgActor(animatedSprite2D2, animatedSprite2D, list.ToArray(), effect, label, titleLabel, label2, hpBarNode, hpBarFill, mpBarNode, mpBarFill, prefix, scale, contentH)
		{
			IsPlayer = isPlayer
		};
		if (animatedSprite2D2 != null)
		{
			animatedSprite2D2.AnimationFinished += delegate
			{
				if (a.Dead)
				{
					if (animatedSprite2D2.SpriteFrames != null && animatedSprite2D2.SpriteFrames.HasAnimation(animatedSprite2D2.Animation))
					{
						int lastFrame = animatedSprite2D2.SpriteFrames.GetFrameCount(animatedSprite2D2.Animation) - 1;
						if (lastFrame >= 0)
						{
							animatedSprite2D2.Pause();
							animatedSprite2D2.Frame = lastFrame;
						}
					}
					return;
				}
				if (!a._abnormalLocked)
				{
					a._busy = false;
				}
			};
			animatedSprite2D2.AnimationLooped += delegate
			{
				if (a.Dead)
				{
					if (animatedSprite2D2.SpriteFrames != null && animatedSprite2D2.SpriteFrames.HasAnimation(animatedSprite2D2.Animation))
					{
						int lastFrame = animatedSprite2D2.SpriteFrames.GetFrameCount(animatedSprite2D2.Animation) - 1;
						if (lastFrame >= 0)
						{
							animatedSprite2D2.Pause();
							animatedSprite2D2.Frame = lastFrame;
						}
					}
				}
			};
		}
		if (effect != null)
		{
			effect.AnimationFinished += delegate
			{
				effect.Visible = false;
			};
		}
		a._atlas = atlas;
		a._group = group;
		a._atlasBase = atlasName;
		a._eightDir = isPlayer && animatedSprite2D2 != null && !flag;
		a._threeDirMorph = threeDirMorph;
		a._supportsWeaponPrefixes = isPlayer && !flag && !threeDirMorph;
		a._mobDir = flag;
		if (flag)
		{
			a._curDir = num;
		}
		else if (isPlayer && animatedSprite2D2 != null)
		{
			a._dirFrames[2] = animatedSprite2D2.SpriteFrames;
			a._curDir = 2;
		}
		if (!isPlayer || flag)
		{
			List<string> list2 = new List<string>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			foreach (string item2 in atlas.ActionNames(group, atlasName))
			{
				int num3 = item2.IndexOf('/');
				string text3 = ((num3 >= 0) ? item2.Substring(num3 + 1) : item2);
				hashSet.Add(text3);
				if (IsAttackBase(text3) && !list2.Contains(text3))
				{
					list2.Add(text3);
				}
			}
			list2.Sort(StringComparer.Ordinal);
			if (list2.Count > 0)
			{
				a._attackVariants = list2.ToArray();
			}
			else
			{
				string[] array2 = new string[2] { "skill", "skill2" };
				foreach (string text4 in array2)
				{
					if (hashSet.Contains(text4))
					{
						a._attackVariants = new string[1] { text4 };
						break;
					}
				}
			}
		}
		return a;
	}

	private static string Resolve(string prefix, string action)
	{
		if (prefix == "")
		{
			return action;
		}
		switch (action)
		{
		case "skill":
		case "death":
		case "get":
			return action;
		default:
			return prefix + "_" + action;
		}
	}

	private string ResolveAction(string action)
	{
		if (!_mobDir)
		{
			return Resolve(_prefix, action);
		}
		string text = $"d{_curDir}/{action}";
		if (_curDir != 0)
		{
			SpriteFrames spriteFrames = _body?.SpriteFrames;
			if (spriteFrames != null && !spriteFrames.HasAnimation(text))
			{
				return "d0/" + action;
			}
		}
		return text;
	}

	private bool HasResolvedAction(string action)
	{
		return (_body?.SpriteFrames)?.HasAnimation(ResolveAction(action)) ?? false;
	}

	private bool HasWalkAction()
	{
		SpriteFrames spriteFrames = _body?.SpriteFrames;
		if (spriteFrames == null)
		{
			return false;
		}
		if (_walkPresenceFrames != spriteFrames || _walkPresencePrefix != _prefix || _walkPresenceDir != _curDir || _walkPresenceMorph != _threeDirMorph)
		{
			_walkPresenceFrames = spriteFrames;
			_walkPresencePrefix = _prefix;
			_walkPresenceDir = _curDir;
			_walkPresenceMorph = _threeDirMorph;
			_walkPresenceValue = spriteFrames.HasAnimation(ResolveAction("walk"));
		}
		return _walkPresenceValue;
	}

	private string CurrentWalkAction()
	{
		if (_walkStrideVariant != 1 || !HasResolvedAction("walk2"))
		{
			return "walk";
		}
		return "walk2";
	}

	private void BeginStrideStepIfNeeded(bool stepping, float progress)
	{
		bool num = stepping && (!_walkStrideStepActive || progress + 0.01f < _walkStrideLastProgress);
		_walkStrideStepActive = stepping;
		_walkStrideLastProgress = progress;
		if (num && HasResolvedAction("walk2"))
		{
			if (_walkStrideStarted)
			{
				_walkStrideVariant ^= 1;
			}
			else
			{
				_walkStrideStarted = true;
			}
			string action = CurrentWalkAction();
			PlayOn(_body, ResolveAction(action));
			PlayOn(_shadow, ResolveAction(action) + "_s");
			PlayClothes(action);
			_walkTableFrames = null;
		}
	}

	public void SetWeaponPrefix(string desired, string fallback)
	{
		if (_supportsWeaponPrefixes && !Dead)
		{
			string text = (SupportsWeaponPrefix(desired) ? desired : (SupportsWeaponPrefix(fallback) ? fallback : (SupportsWeaponPrefix("unarmed") ? "unarmed" : _prefix)));
			if (!(text == _prefix))
			{
				_prefix = text;
				_loop = "";
			}
		}
	}

	private bool SupportsWeaponPrefix(string prefix)
	{
		if (string.IsNullOrWhiteSpace(prefix))
		{
			return false;
		}
		SpriteFrames spriteFrames = _body?.SpriteFrames;
		if (spriteFrames != null && spriteFrames.HasAnimation(Resolve(prefix, "idle")) && spriteFrames.HasAnimation(Resolve(prefix, "walk")))
		{
			return spriteFrames.HasAnimation(Resolve(prefix, "attack"));
		}
		return false;
	}

	public void DriveLoop(bool moving)
	{
		if (!_busy && !Dead)
		{
			string text = (moving ? "walk" : "idle");
			if (text == "walk" && !HasWalkAction())
			{
				text = "idle";
			}
			float speedScale = ((text == "walk") ? 0f : 1f);
			if (_body != null)
			{
				_body.SpeedScale = speedScale;
			}
			if (_shadow != null)
			{
				_shadow.SpeedScale = speedScale;
			}
			AnimatedSprite2D[] clothes = _clothes;
			for (int i = 0; i < clothes.Length; i++)
			{
				clothes[i].SpeedScale = speedScale;
			}
			if (!(text == _loop))
			{
				_loop = text;
				string action = ((text == "walk") ? CurrentWalkAction() : text);
				PlayOn(_body, ResolveAction(action));
				PlayOn(_shadow, ResolveAction(action) + "_s");
				PlayClothes(action);
			}
		}
	}

	private bool TryGetWalkFrameTable(SpriteFrames frames, out string anim, out float[] cumulative)
	{
		if (_walkTableFrames != frames || _walkTablePrefix != _prefix || _walkTableDir != _curDir || _walkTableMorph != _threeDirMorph)
		{
			_walkTableFrames = frames;
			_walkTablePrefix = _prefix;
			_walkTableDir = _curDir;
			_walkTableMorph = _threeDirMorph;
			_walkTableAnim = ResolveAction(CurrentWalkAction());
			_walkTableCumulative = null;
			if (_walkTableAnim.Contains("walk") && frames.HasAnimation(_walkTableAnim))
			{
				int frameCount = frames.GetFrameCount(_walkTableAnim);
				if (frameCount > 0)
				{
					float[] array = new float[frameCount + 1];
					for (int i = 0; i < frameCount; i++)
					{
						array[i + 1] = array[i] + frames.GetFrameDuration(_walkTableAnim, i);
					}
					_walkTableCumulative = array;
				}
			}
		}
		anim = _walkTableAnim;
		cumulative = _walkTableCumulative ?? Array.Empty<float>();
		return _walkTableCumulative != null;
	}

	public void SyncWalkFrame(bool stepping, float progress)
	{
		_walkMicro = Vector2.Zero;
		_walkVisual = Vector2.Zero;
		if (_busy || Dead || _loop != "walk")
		{
			return;
		}
		BeginStrideStepIfNeeded(stepping, progress);
		SpriteFrames spriteFrames = _body?.SpriteFrames;
		if (spriteFrames == null || !TryGetWalkFrameTable(spriteFrames, out string anim, out float[] cumulative))
		{
			return;
		}
		int num = cumulative.Length - 1;
		int num2 = 0;
		if (stepping)
		{
			float num3 = progress * cumulative[num];
			num2 = num - 1;
			for (int i = 0; i < num; i++)
			{
				if (num3 < cumulative[i + 1])
				{
					num2 = i;
					break;
				}
			}
		}
		if (stepping && num >= 2)
		{
			Vector2[] array = _atlas.FrameAnchorOffsets(_group, CurrentAtlasName(), anim);
			if (array != null && array.Length >= num)
			{
				Vector2 vector2 = array[num2] * _body.Scale;
				if (IsRemote)
				{
					_walkMicro = -vector2;
					_walkVisual = Vector2.Zero;
				}
				else
				{
					Vector2 vector = GridStepPixels[Mathf.Clamp(_curDir, 0, 7)] * Mathf.Clamp(progress, 0f, 1f);
					_walkMicro = vector - vector2;
					_walkVisual = vector;
				}
			}
		}
		SetFrameOn(_body, num2);
		SetFrameOn(_shadow, num2);
		AnimatedSprite2D[] clothes = _clothes;
		for (int j = 0; j < clothes.Length; j++)
		{
			SetFrameOn(clothes[j], num2);
		}
	}

	private string CurrentAtlasName()
	{
		if (!_eightDir)
		{
			return _atlasBase;
		}
		return _atlasBase + (_threeDirMorph ? Sfx3 : Sfx8)[_curDir];
	}

	private static void SetFrameOn(AnimatedSprite2D? s, int frame)
	{
		SpriteFrames spriteFrames = s?.SpriteFrames;
		if (spriteFrames == null)
		{
			return;
		}
		int frameCount = spriteFrames.GetFrameCount(s.Animation);
		if (frameCount > 0)
		{
			int num = Mathf.Min(frameCount - 1, frame);
			if (s.Frame != num)
			{
				s.Frame = num;
			}
		}
	}

	private static bool IsAttackBase(string b)
	{
		if (!b.StartsWith("attack", StringComparison.Ordinal))
		{
			return false;
		}
		for (int i = 6; i < b.Length; i++)
		{
			if (!char.IsDigit(b[i]))
			{
				return false;
			}
		}
		return true;
	}

	public string PickAttackAction(Random rng, bool rangedAttacker, bool rangedShot)
	{
		if (_attackVariants.Length <= 1)
		{
			return _attackVariants[0];
		}
		if (rangedAttacker)
		{
			if (!rangedShot)
			{
				return _attackVariants[0];
			}
			return _attackVariants[^1];
		}
		return _attackVariants[rng.Next(_attackVariants.Length)];
	}

	public void PlayAttack(Random rng, bool rangedAttacker = false, bool rangedShot = false, double cycleSeconds = 0.0, double speedRatio = 1.0)
	{
		PlayOneShot(PickAttackAction(rng, rangedAttacker, rangedShot), cycleSeconds, speedRatio);
	}

	public void PlayCast(double cycleSeconds, params string[] prefer)
	{
		if (Dead || _abnormalLocked || _body?.SpriteFrames == null)
		{
			return;
		}
		foreach (string action in prefer)
		{
			if (_body.SpriteFrames.HasAnimation(ResolveAction(action)))
			{
				PlayOneShot(action, cycleSeconds);
				return;
			}
		}
		for (int num = _attackVariants.Length - 1; num >= 1; num--)
		{
			if (_body.SpriteFrames.HasAnimation(ResolveAction(_attackVariants[num])))
			{
				PlayOneShot(_attackVariants[num], cycleSeconds);
				return;
			}
		}
		PlayOneShot(_attackVariants[0], cycleSeconds);
	}

	private static double AnimationSeconds(SpriteFrames frames, string anim)
	{
		double animationSpeed = frames.GetAnimationSpeed(anim);
		if (animationSpeed <= 0.0)
		{
			return 0.0;
		}
		double num = 0.0;
		int frameCount = frames.GetFrameCount(anim);
		for (int i = 0; i < frameCount; i++)
		{
			num += (double)frames.GetFrameDuration(anim, i);
		}
		return num / animationSpeed;
	}

	public void PlayOneShot(string action, double cycleSeconds = 0.0, double speedRatio = 1.0)
	{
		LastOneShotSeconds = 0.0;
		if (Dead || _abnormalLocked || _body == null)
		{
			return;
		}
		string text = ResolveAction(action);
		if (_body.SpriteFrames == null || !_body.SpriteFrames.HasAnimation(text))
		{
			return;
		}
		double num = AnimationSeconds(_body.SpriteFrames, text);
		float num2 = (float)ActionFrameRules.OneShotSpeedScale(num, cycleSeconds, speedRatio);
		LastOneShotSeconds = ((num2 > 0f) ? (num / (double)num2) : num);
		_busy = true;
		_loop = "";
		SetOneShotSpeed(num2);
		PlayOn(_body, text);
		PlayOn(_shadow, ResolveAction(action) + "_s");
		PlayClothes(action);
		if (_effect == null)
		{
			return;
		}
		string text2 = text + "_effect";
		if (ClientPreferences.EffectsEnabled)
		{
			SpriteFrames spriteFrames = _effect.SpriteFrames;
			if (spriteFrames != null && spriteFrames.HasAnimation(text2))
			{
				_effect.Visible = !_occluded || _occludeAlpha > 0f;
				_effect.SpeedScale = num2;
				_effect.Position = Pos;
				PlayOn(_effect, text2);
				return;
			}
		}
		_effect.Visible = false;
	}

	public void InterruptOneShot()
	{
		if (_busy && !Dead && !_abnormalLocked)
		{
			_busy = false;
			_loop = "";
			if (_effect != null)
			{
				_effect.Visible = false;
			}
		}
	}

	public void SetAbnormalVisual(L1jAbnormalVisualState state)
	{
		_poisonVisual = state.Poison;
		if (Dead)
		{
			return;
		}
		if (state.FreezesAnimation)
		{
			if (!_abnormalLocked)
			{
				_abnormalLocked = true;
				_busy = true;
				_loop = "";
				_walkMicro = Vector2.Zero;
				_walkVisual = Vector2.Zero;
				if (_effect != null)
				{
					_effect.Visible = false;
				}
			}
			FreezeAnimationFrame();
		}
		else if (_abnormalLocked)
		{
			_abnormalLocked = false;
			_busy = false;
			_loop = "";
			ResetSpeed();
		}
	}

	private void FreezeAnimationFrame()
	{
		if (_body != null)
		{
			_body.SpeedScale = 0f;
		}
		if (_shadow != null)
		{
			_shadow.SpeedScale = 0f;
		}
		AnimatedSprite2D[] clothes = _clothes;
		for (int i = 0; i < clothes.Length; i++)
		{
			clothes[i].SpeedScale = 0f;
		}
		if (_effect != null)
		{
			_effect.SpeedScale = 0f;
		}
	}

	public void Die(bool isMob = false)
	{
		Dead = true;
		_abnormalLocked = false;
		_busy = true;
		_walkMicro = Vector2.Zero;
		_walkVisual = Vector2.Zero;
		float animSec = (float)AnimSeconds("death");
		if (animSec <= 0.01f) animSec = 0.8f;
		DeadTimer = isMob ? (animSec + 1.2f) : Mathf.Max(10f, animSec + 0.2f);
		if (_combatNameOnly)
		{
			_name.Visible = false;
		}
		_status.Text = "";
		_status.Visible = false;
		if (_effect != null)
		{
			_effect.Visible = false;
		}
		ResetSpeed();
		string deathAction = ResolveAction("death");
		_body?.SpriteFrames?.SetAnimationLoop(deathAction, false);
		_shadow?.SpriteFrames?.SetAnimationLoop(deathAction + "_s", false);
		PlayOn(_body, deathAction);
		PlayOn(_shadow, deathAction + "_s");
		PlayClothes("death");
		if (!HasResolvedAction("death"))
		{
		}
	}

	public void Revive()
	{
		Dead = false;
		_abnormalLocked = false;
		_busy = false;
		_loop = "";
		DeadTimer = 0.0;
		if (_combatNameOnly)
		{
			_name.Visible = true;
		}
		if (_body != null)
		{
			Color mod = _body.Modulate;
			mod.A = 1.0f;
			_body.Modulate = mod;
			_body.SpeedScale = 1.0f;
		}
		if (_shadow != null)
		{
			Color mod = _shadow.Modulate;
			mod.A = 0.4f;
			_shadow.Modulate = mod;
			_shadow.SpeedScale = 1.0f;
		}
		AnimatedSprite2D[] clothes = _clothes;
		for (int i = 0; i < clothes.Length; i++)
		{
			clothes[i].SpeedScale = 1.0f;
		}
		ResetSpeed();
		InterruptOneShot();
		_loop = "idle";
		PlayOn(_body, ResolveAction("idle"));
		PlayOn(_shadow, ResolveAction("idle") + "_s");
		PlayClothes("idle");
	}

	private void ResetSpeed(float speed = 1f)
	{
		SetOneShotSpeed(speed);
	}

	private void SetOneShotSpeed(float speed)
	{
		if (_body != null)
		{
			_body.SpeedScale = speed;
		}
		if (_shadow != null)
		{
			_shadow.SpeedScale = speed;
		}
		AnimatedSprite2D[] clothes = _clothes;
		for (int i = 0; i < clothes.Length; i++)
		{
			clothes[i].SpeedScale = speed;
		}
		if (_effect != null)
		{
			_effect.SpeedScale = speed;
		}
	}

	private float AnimSeconds(string action)
	{
		SpriteFrames spriteFrames = _body?.SpriteFrames;
		if (spriteFrames == null)
		{
			return 0f;
		}
		string text = ResolveAction(action);
		if (!spriteFrames.HasAnimation(text))
		{
			return 0f;
		}
		double animationSpeed = spriteFrames.GetAnimationSpeed(text);
		if (animationSpeed <= 0.0)
		{
			return 0f;
		}
		double num = 0.0;
		for (int i = 0; i < spriteFrames.GetFrameCount(text); i++)
		{
			num += (double)spriteFrames.GetFrameDuration(text, i);
		}
		return (float)(num / animationSpeed);
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

	private void PlayClothes(string action)
	{
		string text = ResolveAction(action);
		for (int i = 0; i < _clothes.Length; i++)
		{
			PlayOn(_clothes[i], text + ClothesSuffix(i));
		}
	}

	public void Face(float dx, float dy)
	{
		if (Dead || (FixedFacing && !_mobDir && !_eightDir) || (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f))
		{
			return;
		}
		if (_eightDir)
		{
			int num = Vec2Dir(dx, dy);
			if (num != _curDir)
			{
				SwapDir(num);
			}
		}
		else if (_mobDir)
		{
			int num2 = Vec2Dir(dx, dy);
			if (num2 != _curDir)
			{
				SwapMobDir(num2);
			}
		}
	}

	public void FaceDirection(int direction)
	{
		if (Dead || (FixedFacing && !_mobDir && !_eightDir))
		{
			return;
		}
		int num = (direction % 8 + 8) % 8;
		if (_eightDir)
		{
			if (num != _curDir)
			{
				SwapDir(num);
			}
		}
		else if (_mobDir && num != _curDir)
		{
			SwapMobDir(num);
		}
	}

	private void SwapMobDir(int newDir)
	{
		if (Dead || newDir == _curDir)
		{
			return;
		}
		int oldDir = _curDir;
		_curDir = newDir;
		_loop = "";
		SwitchMobAnimationDir(_body, oldDir, newDir);
		SwitchMobAnimationDir(_shadow, oldDir, newDir);
		if (_clothes != null)
		{
			for (int i = 0; i < _clothes.Length; i++)
			{
				SwitchMobAnimationDir(_clothes[i], oldDir, newDir);
			}
		}
	}

	private static void SwitchMobAnimationDir(AnimatedSprite2D? s, int oldDir, int newDir)
	{
		if (s == null || s.SpriteFrames == null)
		{
			return;
		}
		string curAnim = s.Animation.ToString();
		if (string.IsNullOrEmpty(curAnim))
		{
			return;
		}
		int slashIdx = curAnim.IndexOf('/');
		string baseAction = slashIdx >= 0 ? curAnim.Substring(slashIdx + 1) : curAnim;
		string newAnim = $"d{newDir}/{baseAction}";
		if (!s.SpriteFrames.HasAnimation(newAnim))
		{
			newAnim = $"d0/{baseAction}";
		}
		if (s.SpriteFrames.HasAnimation(newAnim) && newAnim != curAnim)
		{
			int curFrame = s.Frame;
			float progress = s.GetPlayingSpeed() > 0 ? s.FrameProgress : 0f;
			bool isPlaying = s.IsPlaying();
			s.Animation = newAnim;
			s.Frame = Mathf.Min(curFrame, Mathf.Max(0, s.SpriteFrames.GetFrameCount(newAnim) - 1));
			s.FrameProgress = progress;
			if (isPlaying)
			{
				s.Play();
			}
		}
		s.SetMeta("idle", $"d{newDir}/idle");
	}

	private void SwapDir(int dir)
	{
		string text = (_threeDirMorph ? Sfx3 : Sfx8)[dir];
		SpriteFrames[] dirFrames = _dirFrames;
		int num = dir;
		SpriteFrames spriteFrames = dirFrames[num] ?? (dirFrames[num] = _atlas.BuildFrames(_group, _atlasBase + text));
		if (spriteFrames != null)
		{
			_curDir = dir;
			SwapFrames(_body, spriteFrames);
			SwapFrames(_shadow, spriteFrames);
			AnimatedSprite2D[] clothes = _clothes;
			for (num = 0; num < clothes.Length; num++)
			{
				SwapFrames(clothes[num], spriteFrames);
			}
			SwapFrames(_effect, spriteFrames);
		}
	}

	private static void SwapFrames(AnimatedSprite2D? s, SpriteFrames f)
	{
		if (s == null)
		{
			return;
		}
		StringName animation = s.Animation;
		int frame = s.Frame;
		bool flag = s.IsPlaying();
		s.SpriteFrames = f;
		if (f.HasAnimation(animation))
		{
			s.Animation = animation;
			s.Frame = Mathf.Min(frame, Mathf.Max(0, f.GetFrameCount(animation) - 1));
			if (flag)
			{
				s.Play();
			}
		}
	}

	internal static int Vec2Dir(float dx, float dy)
	{
		switch (Mathf.RoundToInt(Mathf.Atan2(dy, dx) * 4f / (float)Math.PI))
		{
		case 0:
			return 3;
		case 1:
			return 4;
		case 2:
			return 5;
		case 3:
			return 6;
		case -4:
		case 4:
			return 7;
		case -3:
			return 0;
		case -2:
			return 1;
		case -1:
			return 2;
		default:
			return 6;
		}
	}


	public void SetOccluded(bool occluded)
	{
		if (_occluded != occluded)
		{
			_occluded = occluded;
			if (!_everSynced)
			{
				_occludeAlpha = (occluded ? 0f : 1f);
			}
			if (!occluded || !_everSynced)
			{
				SetOccludedGroupVisible(!occluded || _occludeAlpha > 0f);
			}
		}
	}

	public bool Visible { get; private set; } = true;

	public void SetVisible(bool visible)
	{
		Visible = visible;
		SetOccludedGroupVisible(visible);
		if (_hpBarNode != null)
		{
			_hpBarNode.Visible = visible && !Dead;
		}
	}

	private void SetOccludedGroupVisible(bool visible)
	{
		CanvasItem[] array = new CanvasItem[6] { _body, _shadow, _effect, _name, _title, _status };
		foreach (CanvasItem canvasItem in array)
		{
			if (canvasItem != null)
			{
				if (canvasItem == _title)
				{
					canvasItem.Visible = visible && _title.Text.Length > 0;
				}
				else
				{
					canvasItem.Visible = visible;
				}
			}
		}
		AnimatedSprite2D[] clothes = _clothes;
		for (int i = 0; i < clothes.Length; i++)
		{
			clothes[i].Visible = visible;
		}
	}

	public void Sync(double deadFade, float dt = 0f)
	{
		if (!Visible)
		{
			SetOccludedGroupVisible(false);
			if (_hpBarNode != null) _hpBarNode.Visible = false;
			return;
		}
		_everSynced = true;
		if (_busy && !_abnormalLocked && !Dead)
		{
			_busyWatchdog += dt;
			if (_busyWatchdog > Math.Max(0.5f, (float)LastOneShotSeconds + 0.15f))
			{
				_busy = false;
				_busyWatchdog = 0f;
				_loop = "";
			}
		}
		else
		{
			_busyWatchdog = 0f;
		}
		float num = (_occluded ? 0f : 1f);
		if (Math.Abs(_occludeAlpha - num) > 0.0001f)
		{
			float num2 = (_occluded ? 0.25f : 0.15f);
			_occludeAlpha = Mathf.MoveToward(_occludeAlpha, num, dt / num2);
			if (_occluded && _occludeAlpha <= 0f)
			{
				SetOccludedGroupVisible(visible: false);
			}
		}
		if (_combatNameOnly)
		{
			_combatNameRemaining = Math.Max(0.0, _combatNameRemaining - (double)dt);
			_name.Visible = !Dead && _combatNameRemaining > 0.0 && !_occluded;
		}
		Color modulate = _name.Modulate;
		modulate.A = _occludeAlpha;
		_name.Modulate = modulate;
		Color modulateTitle = _title.Modulate;
		modulateTitle.A = _occludeAlpha;
		_title.Modulate = modulateTitle;
		Color modulate2 = _status.Modulate;
		modulate2.A = _occludeAlpha;
		_status.Modulate = modulate2;
		Vector2 position = Pos + _walkMicro;
		Vector2 vector = Pos + _walkVisual;
		int num3 = Math.Max(Depth.Of(Pos.Y), MinimumWorldDepth);
		if (_body != null)
		{
			_body.Position = position;
			_body.ZIndex = num3;
		}
		if (_shadow != null)
		{
			_shadow.Position = position;
			_shadow.ZIndex = num3 - 1;
		}
		AnimatedSprite2D[] clothes = _clothes;
		foreach (AnimatedSprite2D obj in clothes)
		{
			obj.Position = position;
			obj.ZIndex = num3 + 1;
		}
		if (_effect != null)
		{
			_effect.Position = position;
			_effect.ZIndex = num3 + 2;
		}
		float num4 = vector.Y - _contentH * _scale - 8f;
		bool showTitle = _title.Visible && _title.Text.Length > 0;
		float titleShift = showTitle ? 14f : 0f;
		if (_hpBarNode != null && _hpBarFill != null)
		{
			bool hasMp = MaxMp > 0;
			_hpBarNode.Position = new Vector2(vector.X - 22f, num4 - (hasMp ? 32f : 26f) - titleShift);
			_hpBarNode.ZIndex = num3 + 10;
			bool showHp = !Dead && MaxHp > 0;
			_hpBarNode.Visible = showHp;
			if (showHp)
			{
				float hpPct = Mathf.Clamp((float)(Hp / Math.Max(1.0, MaxHp)), 0f, 1f);
				_hpBarFill.Size = new Vector2(42f * hpPct, 4f);

				if (_mpBarNode != null && _mpBarFill != null)
				{
					_mpBarNode.Visible = hasMp;
					if (hasMp)
					{
						float mpPct = Mathf.Clamp((float)(Mp / Math.Max(1.0, MaxMp)), 0f, 1f);
						_mpBarFill.Size = new Vector2(42f * mpPct, 4f);
					}
				}
			}
		}
		_name.Position = new Vector2(vector.X - 70f, num4 - 18f - titleShift);
		if (showTitle)
		{
			_title.Position = new Vector2(vector.X - 90f, num4 - 18f);
		}
		if (_status.Visible)
		{
			_status.Position = new Vector2(vector.X - 80f, num4 - 32f - titleShift);
		}
		if (Dead && _body != null)
		{
			if (_body.SpriteFrames != null && _body.SpriteFrames.HasAnimation(_body.Animation))
			{
				int lastFrame = _body.SpriteFrames.GetFrameCount(_body.Animation) - 1;
				if (lastFrame >= 0 && _body.Frame >= lastFrame)
				{
					_body.Pause();
					_body.Frame = lastFrame;
					if (_shadow != null)
					{
						_shadow.Pause();
						_shadow.Frame = Math.Min(_shadow.Frame, lastFrame);
					}
				}
			}
			Color modulate3 = _body.Modulate;
			modulate3.A = (float)Mathf.Clamp(deadFade, 0.0, 1.0) * _occludeAlpha;
			_body.Modulate = modulate3;
			if (_shadow != null)
			{
				Color modulate4 = _shadow.Modulate;
				modulate4.A = modulate3.A * 0.4f;
				_shadow.Modulate = modulate4;
			}
			clothes = _clothes;
			foreach (AnimatedSprite2D obj2 in clothes)
			{
				Color modulate5 = obj2.Modulate;
				modulate5.A = modulate3.A;
				obj2.Modulate = modulate5;
			}
		}
		else if (_body != null)
		{
			float num5 = _visibilityAlpha * _occludeAlpha;
			Color color = _poisonVisual switch
			{
				L1jPoisonVisual.Green => new Color(0.48f, 1f, 0.48f, num5), 
				L1jPoisonVisual.Gray => new Color(0.62f, 0.62f, 0.62f, num5), 
				_ => new Color(1f, 1f, 1f, num5), 
			};
			_body.Modulate = color;
			if (_shadow != null)
			{
				Color modulate6 = color;
				modulate6.A = 0.4f * num5;
				_shadow.Modulate = modulate6;
			}
			clothes = _clothes;
			foreach (AnimatedSprite2D obj3 in clothes)
			{
				Color modulate7 = color;
				modulate7.A = num5;
				obj3.Modulate = modulate7;
			}
			if (_effect != null)
			{
				Color modulate8 = _effect.Modulate;
				modulate8.A = num5;
				_effect.Modulate = modulate8;
			}
		}
	}

	public void SetName(string disp, int lv)
	{
		Disp = disp;
		Level = lv;
		_name.Text = $"{disp} Lv{lv}";
	}

	public void SetNameWithoutLevel(string disp, int lv)
	{
		Disp = disp;
		Level = lv;
		_name.Text = disp;
	}

	public void HideName()
	{
		_name.Visible = false;
		_title.Visible = false;
	}

	public void ShowName()
	{
		_name.Visible = true;
		_title.Visible = _title.Text.Length > 0 && !_occluded;
	}

	public void SetTitle(string? title)
	{
		string trimmed = (title ?? "").Trim();
		if (trimmed.Length == 0)
		{
			_title.Text = "";
			_title.Visible = false;
		}
		else
		{
			string formatted = (trimmed.StartsWith("[") || trimmed.StartsWith("【") || trimmed.StartsWith("<")) ? trimmed : ("[" + trimmed + "]");
			_title.Text = formatted;
			_title.Visible = _name.Visible && !_occluded;
		}
	}

	public void SetTitleColor(Color color)
	{
		_title.AddThemeColorOverride("font_color", color);
	}

	public void SetCombatNameOnly()
	{
		_combatNameOnly = true;
		_combatNameRemaining = 0.0;
		_name.Visible = false;
		_title.Visible = false;
	}

	public void RevealCombatName(double seconds = 5.0)
	{
		if (_combatNameOnly && !Dead)
		{
			_combatNameRemaining = Math.Max(_combatNameRemaining, seconds);
			_name.Visible = true;
			_title.Visible = _title.Text.Length > 0 && !_occluded;
		}
	}

	public void SetNameColor(Color color)
	{
		_name.AddThemeColorOverride("font_color", color);
	}

	public void SetInvisible(bool invisible)
	{
		_visibilityAlpha = (invisible ? 0.46f : 1f);
	}

	public void SetStatus(string text)
	{
		if (!(_status.Text == text))
		{
			_status.Text = text;
			_status.Visible = text.Length > 0 && !_occluded;
		}
	}

	public void Free()
	{
		Node?[] array = new Node?[7] { _body, _shadow, _effect, _name, _title, _status, _hpBarNode };
		foreach (Node node in array)
		{
			if (node != null && GodotObject.IsInstanceValid(node))
			{
				node.QueueFree();
			}
		}
		AnimatedSprite2D[] clothes = _clothes;
		foreach (AnimatedSprite2D animatedSprite2D in clothes)
		{
			if (GodotObject.IsInstanceValid(animatedSprite2D))
			{
				animatedSprite2D.QueueFree();
			}
		}
	}
}
