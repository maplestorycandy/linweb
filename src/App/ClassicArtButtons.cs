using System;
using Godot;

namespace IdleLineage.App;

internal static class ClassicArtButtons
{
	private const string ConfirmPath = "res://assets/ui/buttons/confirm.png";

	private const string CancelPath = "res://assets/ui/buttons/cancel.png";

	private const float NativeWidth = 75f;

	private const float NativeConfirmHeight = 18f;

	private const float NativeCancelHeight = 17f;

	private const float Scale = 2f;

	public static readonly Vector2 ConfirmSize = new Vector2(150f, 36f);

	public static readonly Vector2 CancelSize = new Vector2(150f, 34f);

	public static TextureButton Confirm(Action pressed, string tooltip = "確認")
	{
		return Make("res://assets/ui/buttons/confirm.png", ConfirmSize, pressed, tooltip);
	}

	public static TextureButton Cancel(Action pressed, string tooltip = "取消")
	{
		return Make("res://assets/ui/buttons/cancel.png", CancelSize, pressed, tooltip);
	}

	private static TextureButton Make(string path, Vector2 size, Action pressed, string tooltip)
	{
		TextureButton button = new TextureButton
		{
			TextureNormal = GD.Load<Texture2D>(path),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Size = size,
			CustomMinimumSize = size,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = tooltip
		};
		button.MouseEntered += delegate
		{
			button.Modulate = new Color(1.18f, 1.18f, 1.18f);
		};
		button.MouseExited += delegate
		{
			button.Modulate = Colors.White;
		};
		button.ButtonDown += delegate
		{
			button.Modulate = new Color(0.78f, 0.78f, 0.78f);
		};
		button.ButtonUp += delegate
		{
			button.Modulate = Colors.White;
		};
		button.Pressed += delegate
		{
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.45f);
			pressed();
		};
		return button;
	}

	public static Button CreateIdentifyAllButton(int unappraisedCount, int scrollTotal, Action onAllPressed)
	{
		StyleBoxFlat btnNormal = new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.16f, 0.13f, 0.95f),
			BorderColor = new Color(0.68f, 0.54f, 0.28f, 0.9f),
			BorderWidthTop = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 4f,
			ContentMarginBottom = 4f
		};
		StyleBoxFlat btnHover = new StyleBoxFlat
		{
			BgColor = new Color(0.28f, 0.24f, 0.17f, 1f),
			BorderColor = new Color(0.96f, 0.84f, 0.46f, 1f),
			BorderWidthTop = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 4f,
			ContentMarginBottom = 4f
		};
		StyleBoxFlat btnPressed = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.10f, 0.08f, 1f),
			BorderColor = new Color(0.50f, 0.38f, 0.18f, 1f),
			BorderWidthTop = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 4f,
			ContentMarginBottom = 4f
		};
		StyleBoxFlat btnDisabled = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.12f, 0.14f, 0.6f),
			BorderColor = new Color(0.28f, 0.28f, 0.30f, 0.4f),
			BorderWidthTop = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 4f,
			ContentMarginBottom = 4f
		};

		bool enabled = unappraisedCount > 0 && scrollTotal > 0;
		Button btnAll = new Button
		{
			Text = unappraisedCount > 0 ? $"全部鑑定 ({unappraisedCount})" : "全部鑑定",
			CustomMinimumSize = new Vector2(136f, 34f),
			MouseDefaultCursorShape = enabled ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow,
			FocusMode = Control.FocusModeEnum.None,
			Disabled = !enabled,
			TooltipText = enabled
				? $"點擊批量鑑定背包中所有未鑑定的裝備與道具（共 {unappraisedCount} 件）"
				: (scrollTotal <= 0 ? "鑑定卷軸不足" : "背包中已無未鑑定物品")
		};
		btnAll.AddThemeStyleboxOverride("normal", btnNormal);
		btnAll.AddThemeStyleboxOverride("hover", btnHover);
		btnAll.AddThemeStyleboxOverride("pressed", btnPressed);
		btnAll.AddThemeStyleboxOverride("disabled", btnDisabled);
		btnAll.AddThemeColorOverride("font_color", new Color(0.96f, 0.88f, 0.65f));
		btnAll.AddThemeColorOverride("font_hover_color", new Color(1f, 0.98f, 0.85f));
		btnAll.AddThemeColorOverride("font_pressed_color", new Color(0.8f, 0.72f, 0.5f));
		btnAll.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.55f));
		btnAll.AddThemeFontSizeOverride("font_size", 14);

		if (enabled)
		{
			btnAll.Pressed += delegate
			{
				GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.45f);
				onAllPressed();
			};
		}
		return btnAll;
	}
}
