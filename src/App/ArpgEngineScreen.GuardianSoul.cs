using System;
using System.Linq;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Core;

namespace IdleLineage.App;

public partial class ArpgEngineScreen
{
	public static ArpgEngineScreen? ActiveInstance { get; private set; }
	public bool IsGuardianActive => _isGuardianActive;
	public double GuardianTimeRemaining => _guardianTimeRemaining;

	private static Font? _damageFont;
	private static bool _damageFontLoaded;

	private static Font? GetDamageFont()
	{
		if (!_damageFontLoaded)
		{
			_damageFontLoaded = true;
			if (ResourceLoader.Exists("res://assets/fonts/Cubic_11.ttf"))
			{
				_damageFont = GD.Load<Font>("res://assets/fonts/Cubic_11.ttf");
			}
			else if (ResourceLoader.Exists("res://assets/fonts/NotoSansTC-VF.ttf"))
			{
				_damageFont = GD.Load<Font>("res://assets/fonts/NotoSansTC-VF.ttf");
			}
		}
		return _damageFont;
	}

	public static string GetGuardianRemainingTimeString()
	{
		if (ActiveInstance == null || !ActiveInstance._isGuardianActive)
		{
			int initMins = (int)(GameRateConfig.GuardianSoulDurationSeconds / 60.0);
			return $"持續時間：{initMins} 分鐘（未使用/尚未啟動）";
		}
		int totalSecs = (int)Math.Max(0.0, ActiveInstance._guardianTimeRemaining);
		int mins = totalSecs / 60;
		int secs = totalSecs % 60;
		return $"剩餘時間：{mins} 分 {secs:D2} 秒";
	}

	public static string GetGuardianSoulFullTooltipText()
	{
		string timeStr = GetGuardianRemainingTimeString();
		return $"【能力加成】\n• 最大生命力 (HP) +{(int)GameRateConfig.GuardianSoulHpBonus}\n• 最大魔力 (MP) +{(int)GameRateConfig.GuardianSoulMpBonus}\n• 受到物理與魔法傷害減少 {GameRateConfig.GuardianSoulDamageReductionPercent:0.#}%\n\n【持續時間】\n• {timeStr}\n\n※ 世界唯一掉落物";
	}

	public void FloatDamage(Vector2 at, int damage, bool crit = false)
	{
		if (damage <= 0) return;

		float jitterX = _rng.Next(-10, 11);
		float jitterY = _rng.Next(-8, 9);
		Vector2 startPos = at + new Vector2(-24f + jitterX, -62f + jitterY);

		Label label = new Label
		{
			Text = $"{damage}",
			Position = startPos,
			Size = new Vector2(48f, 22f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 2100
		};

		Font? font = GetDamageFont();
		if (font != null)
		{
			label.AddThemeFontOverride("font", font);
		}

		Color fontColor = crit ? Color.FromHtml("#ffb246") : Color.FromHtml("#f0a248");
		Color outlineColor = Color.FromHtml("#461804");

		label.AddThemeFontSizeOverride("font_size", crit ? 25 : 20);
		label.AddThemeColorOverride("font_color", fontColor);
		label.AddThemeColorOverride("font_outline_color", outlineColor);
		label.AddThemeConstantOverride("outline_size", 4);

		label.AddThemeColorOverride("font_shadow_color", new Color(0.12f, 0.04f, 0.01f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);

		label.PivotOffset = new Vector2(24f, 11f);
		_ui.AddChild(label, forceReadableName: false, InternalMode.Disabled);

		Tween tween = label.CreateTween();
		tween.SetParallel();

		label.Scale = new Vector2(1.35f, 1.35f);
		tween.TweenProperty(label, "scale", Vector2.One, 0.16)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);

		tween.TweenProperty(label, "position:y", startPos.Y - 36f, 0.85)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);

		tween.TweenProperty(label, "modulate:a", 0.0, 0.45)
			.SetDelay(0.4);

		tween.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	private void InitGuardianSoul()
	{
		ActiveInstance = this;

		CombatEngine.IsGuardianSoulInWorldCheck = () =>
			_isGuardianActive ||
			_groundDrops.Any(d => d.ItemKey == ContentAdditions.GuardianSoulKey) ||
			CombatInventory.Count(_engine.Player, ContentAdditions.GuardianSoulKey) > 0;

		_purpleFrameOverlay = new PurpleFrameOverlay
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 2999
		};
		_purpleFrameOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_purpleFrameOverlay, forceReadableName: false, InternalMode.Disabled);

		foreach (var st in _engine.Player.InventoryStacks)
		{
			if (st.ItemKey == ContentAdditions.GuardianSoulKey)
			{
				st.IsIdentified = true;
			}
		}

		if (CombatInventory.Count(_engine.Player, ContentAdditions.GuardianSoulKey) > 0 && !_isGuardianActive)
		{
			ActivateGuardianSoul(silent: true);
		}
	}

	public void TriggerScreenShake(float duration = 2.0f, float intensity = 14.0f)
	{
		_screenShakeDuration = duration;
		_screenShakeIntensity = intensity;
	}

	public void PlayLevelUpEffect(Vector2 worldPos)
	{
		LevelUpEffect fx = new LevelUpEffect
		{
			Position = worldPos,
			ZIndex = Depth.Of(worldPos.Y) + 50
		};
		_arena.AddChild(fx, forceReadableName: false, InternalMode.Disabled);
	}

	private void ActivateGuardianSoul(bool silent = false)
	{
		_isGuardianActive = true;
		_guardianTimeRemaining = GameRateConfig.GuardianSoulDurationSeconds;
		_engine.Player.AddStatus("guardian_soul", (int)(GameRateConfig.GuardianSoulDurationSeconds * 25.0));
		CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
		_engine.Player.Hp = _engine.Player.MaxHp;
		_engine.Player.Mp = _engine.Player.MaxMp;

		foreach (var st in _engine.Player.InventoryStacks)
		{
			if (st.ItemKey == ContentAdditions.GuardianSoulKey)
			{
				st.IsIdentified = true;
			}
		}

		if (string.IsNullOrEmpty(_savedOriginalPlayerName))
		{
			_savedOriginalPlayerName = _engine.Player.Disp;
		}
		_engine.Player.Disp = "**守護者**";
		_playerView?.ShowName();
		_playerView?.SetNameWithoutLevel("**守護者**", _engine.Player.Level);
		_playerView?.SetNameColor(Color.FromHtml("#3b82f6"));
		_playerView?.SetTitle(_engine.Player.Title);

		if (!silent)
		{
			_purpleFrameRemaining = 4.0f;
			TriggerScreenShake(4.0f, 14.0f);
			if (_purpleFrameOverlay != null)
			{
				_purpleFrameOverlay.Alpha = 0.95f;
				_purpleFrameOverlay.QueueRedraw();
			}
			Float(PlayerPos(), "★ 獲得【守護者的靈魂】！", Color.FromHtml("#c084fc"), big: true);
			int durMins = (int)(GameRateConfig.GuardianSoulDurationSeconds / 60.0);
			SlabLog($"[color=#c084fc]★ 獲得世界唯一神器【守護者的靈魂】！化身為【**守護者**】（持續{durMins}分鐘）！[/color]");
			GameAudio.Instance?.PlayEvent("levelup");
		}
		else
		{
			_purpleFrameRemaining = 0f;
			if (_purpleFrameOverlay != null)
			{
				_purpleFrameOverlay.Alpha = 0f;
				_purpleFrameOverlay.QueueRedraw();
			}
		}
	}

	private void DeactivateGuardianSoul()
	{
		_isGuardianActive = false;
		_guardianTimeRemaining = 0.0;
		_purpleFrameRemaining = 0f;
		_engine.Player.Statuses.Remove("guardian_soul");
		CombatInventory.TryRemove(_engine.Player, ContentAdditions.GuardianSoulKey, 1);
		if (!string.IsNullOrEmpty(_savedOriginalPlayerName))
		{
			_engine.Player.Disp = _savedOriginalPlayerName;
			_savedOriginalPlayerName = "";
		}
		_playerView?.ShowName();
		_playerView?.SetNameWithoutLevel(_engine.Player.Disp, _engine.Player.Level);
		_playerView?.SetNameColor(Color.FromHtml("#c9d1de"));
		_playerView?.SetTitle(_engine.Player.Title);
		CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
		if (_purpleFrameOverlay != null)
		{
			_purpleFrameOverlay.Alpha = 0f;
			_purpleFrameOverlay.QueueRedraw();
		}
		_buffIconSig = "";
		_bagRefresh?.Invoke();
		SlabLog("[color=#e2938f]守護者的靈魂時效已過，力量回歸天地，你已還原為原有名號。[/color]");
	}

	private void UpdateGuardianSoul(double delta)
	{
		if (_screenShakeDuration > 0f)
		{
			_screenShakeDuration -= (float)delta;
			float factor = Mathf.Clamp(_screenShakeDuration / 4.0f, 0f, 1f);
			_screenShakeOffset = new Vector2(
				(float)(_rng.NextDouble() * 2.0 - 1.0) * _screenShakeIntensity * factor,
				(float)(_rng.NextDouble() * 2.0 - 1.0) * _screenShakeIntensity * factor
			);
		}
		else
		{
			_screenShakeOffset = Vector2.Zero;
		}

		if (_isGuardianActive)
		{
			_guardianTimeRemaining -= delta;

			// 四周紫色效果縮短為剛獲得時的 4 秒震動及紫光閃爍，4 秒結束後立即消失
			if (_purpleFrameRemaining > 0f)
			{
				_purpleFrameRemaining -= (float)delta;
				_purpleFramePulse += (float)delta;
				if (_purpleFrameOverlay != null)
				{
					float flash = 0.55f + 0.4f * Mathf.Sin(_purpleFramePulse * 12.0f);
					float fade = Mathf.Clamp(_purpleFrameRemaining / 0.8f, 0f, 1f);
					_purpleFrameOverlay.Alpha = flash * fade;
					_purpleFrameOverlay.QueueRedraw();
				}
			}
			else if (_purpleFrameOverlay != null && _purpleFrameOverlay.Alpha > 0f)
			{
				_purpleFrameOverlay.Alpha = 0f;
				_purpleFrameOverlay.QueueRedraw();
			}

			if (_playerView != null)
			{
				_playerView.ShowName();
				_playerView.SetNameWithoutLevel("**守護者**", _engine.Player.Level);
				_playerView.SetNameColor(Color.FromHtml("#3b82f6"));
				_playerView.SetTitle(_engine.Player.Title);
			}
			if (_guardianTimeRemaining <= 0.0)
			{
				DeactivateGuardianSoul();
			}
		}
	}
}
