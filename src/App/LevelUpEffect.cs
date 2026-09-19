using System;
using Godot;

namespace IdleLineage.App;

public sealed partial class LevelUpEffect : Node2D
{
	private struct Particle
	{
		public float OffsetX;
		public float Y;
		public float Speed;
		public float Size;
		public float Alpha;
	}

	public float PillarHeight { get; set; } = 0f;
	public float MaxHeight { get; set; } = 145f;
	public float Duration { get; set; } = 1.85f;

	private float _elapsedTime;
	private Particle[] _particles = Array.Empty<Particle>();
	private Label? _label;
	private readonly Random _rng = new Random();

	public override void _Ready()
	{
		// 1. 建立白底發光 LEVEL UP 文字
		_label = new Label
		{
			Text = "LEVEL UP",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Size = new Vector2(160f, 32f),
			Position = new Vector2(-80f, -65f),
			Scale = new Vector2(0.5f, 0.5f),
			PivotOffset = new Vector2(80f, 16f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		// 亮白主字體 + 經典天藍光暈描邊
		_label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
		_label.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.58f, 1.0f, 0.95f));
		_label.AddThemeConstantOverride("outline_size", 6);
		_label.AddThemeFontSizeOverride("font_size", 21);
		_label.Modulate = new Color(1f, 1f, 1f, 0f);
		AddChild(_label);

		// 2. 初始化 18 顆隨光柱向上升騰的魔法微粒
		_particles = new Particle[18];
		for (int i = 0; i < _particles.Length; i++)
		{
			_particles[i] = new Particle
			{
				OffsetX = (float)(_rng.NextDouble() * 46.0 - 23.0),
				Y = (float)(_rng.NextDouble() * 35.0),
				Speed = (float)(_rng.NextDouble() * 100.0 + 80.0),
				Size = (float)(_rng.NextDouble() * 2.0 + 1.5),
				Alpha = (float)(_rng.NextDouble() * 0.6 + 0.4)
			};
		}

		// 3. Tween 驅動光柱向上拔升與文字浮空動畫
		Tween tween = CreateTween();
		tween.SetParallel();

		// 光柱慢慢拔地升起（0.65 秒平滑展開）
		tween.TweenMethod(Callable.From<float>((val) => PillarHeight = val), 0f, MaxHeight, 0.65)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		// 文字彈出放大
		tween.TweenProperty(_label, "modulate:a", 1.0, 0.2);
		tween.TweenProperty(_label, "scale", new Vector2(1.15f, 1.15f), 0.28)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
		// 文字緩慢向上漂浮
		tween.TweenProperty(_label, "position:y", _label.Position.Y - 26f, 1.3);

		// 整體後段漸隱淡出
		tween.Chain().TweenInterval(0.45);
		tween.TweenProperty(this, "modulate:a", 0.0, 0.55);
		tween.Chain().TweenCallback(Callable.From(QueueFree));
	}

	public override void _Process(double delta)
	{
		_elapsedTime += (float)delta;

		// 逐幀更新升騰微粒位置
		for (int i = 0; i < _particles.Length; i++)
		{
			_particles[i].Y += _particles[i].Speed * (float)delta;
			if (_particles[i].Y > PillarHeight + 20f)
			{
				_particles[i].Y = 0f;
				_particles[i].OffsetX = (float)(_rng.NextDouble() * 48.0 - 24.0);
			}
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		float progress = Mathf.Clamp(_elapsedTime / Duration, 0f, 1f);
		float fadeAlpha = Mathf.Clamp((1f - progress) * 1.5f, 0f, 1f);
		float groundY = 6f; // 人物腳底基準線

		// ──────────────────────────
		// A. 腳底藍光魔法陣（同心圓環 + 旋轉芒芒光芒）
		// ──────────────────────────
		float ringRot = _elapsedTime * 2.2f;
		DrawSetTransform(new Vector2(0f, groundY), 0f, new Vector2(1.0f, 0.45f)); // 橢圓透視

		// 外層光暈圓盤
		DrawCircle(Vector2.Zero, 38f, new Color(0.12f, 0.55f, 1.0f, 0.38f * fadeAlpha));
		// 核心同心光環
		DrawArc(Vector2.Zero, 34f, 0f, Mathf.Tau, 32, new Color(0.35f, 0.85f, 1.0f, 0.85f * fadeAlpha), 2.5f, antialiased: true);
		DrawArc(Vector2.Zero, 22f, 0f, Mathf.Tau, 24, new Color(0.75f, 0.95f, 1.0f, 0.95f * fadeAlpha), 2.0f, antialiased: true);

		// 腳底法陣交錯芒星線
		for (int i = 0; i < 4; i++)
		{
			float ang = ringRot + i * (Mathf.Pi / 2f);
			Vector2 p1 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 34f;
			Vector2 p2 = new Vector2(Mathf.Cos(ang + Mathf.Pi), Mathf.Sin(ang + Mathf.Pi)) * 34f;
			DrawLine(p1, p2, new Color(0.7f, 0.95f, 1.0f, 0.65f * fadeAlpha), 1.5f, antialiased: true);
		}

		// 還原 Transform
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

		// ──────────────────────────
		// B. 拔地升起的「藍光陣勢」光柱
		// ──────────────────────────
		if (PillarHeight > 2f)
		{
			float topY = groundY - PillarHeight;

			// 1. 外層漸層半透明藍光柱
			float beamW = 46f;
			Color[] outerCols = new Color[]
			{
				new Color(0.15f, 0.6f, 1.0f, 0.5f * fadeAlpha),
				new Color(0.15f, 0.6f, 1.0f, 0.5f * fadeAlpha),
				new Color(0.2f, 0.7f, 1.0f, 0.05f * fadeAlpha),
				new Color(0.2f, 0.7f, 1.0f, 0.05f * fadeAlpha)
			};
			Vector2[] outerPts = new Vector2[]
			{
				new Vector2(-beamW * 0.5f, groundY),
				new Vector2(beamW * 0.5f, groundY),
				new Vector2(beamW * 0.42f, topY),
				new Vector2(-beamW * 0.42f, topY)
			};
			DrawPolygon(outerPts, outerCols);

			// 2. 內層高亮白藍能量核心
			float innerW = 18f;
			Color[] innerCols = new Color[]
			{
				new Color(0.85f, 0.95f, 1.0f, 0.85f * fadeAlpha),
				new Color(0.85f, 0.95f, 1.0f, 0.85f * fadeAlpha),
				new Color(0.65f, 0.9f, 1.0f, 0.1f * fadeAlpha),
				new Color(0.65f, 0.9f, 1.0f, 0.1f * fadeAlpha)
			};
			Vector2[] innerPts = new Vector2[]
			{
				new Vector2(-innerW * 0.5f, groundY),
				new Vector2(innerW * 0.5f, groundY),
				new Vector2(innerW * 0.35f, topY),
				new Vector2(-innerW * 0.35f, topY)
			};
			DrawPolygon(innerPts, innerCols);

			// 3. 上升的魔法微粒光點
			for (int i = 0; i < _particles.Length; i++)
			{
				float py = groundY - _particles[i].Y;
				if (py >= topY)
				{
					float pa = _particles[i].Alpha * fadeAlpha;
					DrawCircle(new Vector2(_particles[i].OffsetX, py), _particles[i].Size, new Color(0.9f, 0.96f, 1.0f, pa));
				}
			}
		}
	}
}
