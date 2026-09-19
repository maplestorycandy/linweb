using System;
using Godot;

namespace IdleLineage.App;

internal static class ClassicTradeButtons
{
	private const string AssetRoot = "res://assets/ui/buttons/trade";

	public static readonly Vector2 NativeSize = new Vector2(53f, 17f);

	public static TextureButton Buy(Action? pressed = null, string tooltip = "買")
	{
		return Make("1992c.png", pressed, tooltip);
	}

	public static TextureButton Sell(Action? pressed = null, string tooltip = "賣")
	{
		return Make("1998c.png", pressed, tooltip);
	}

	public static TextureButton Cancel(Action? pressed = null, string tooltip = "取消")
	{
		return Make("1994c.png", pressed, tooltip);
	}

	public static TextureButton Confirm(Action? pressed = null, string tooltip = "確認")
	{
		return Make("1996c.png", pressed, tooltip);
	}

	private static TextureButton Make(string fileName, Action? pressed, string tooltip)
	{
		Texture2D texture2D = GD.Load<Texture2D>("res://assets/ui/buttons/trade/" + fileName);
		TextureButton button = new TextureButton
		{
			TextureNormal = texture2D,
			TextureHover = texture2D,
			TexturePressed = texture2D,
			TextureDisabled = texture2D,
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Size = NativeSize,
			CustomMinimumSize = NativeSize,
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = tooltip
		};
		button.MouseEntered += delegate
		{
			button.Modulate = new Color(1.15f, 1.15f, 1.15f);
		};
		button.MouseExited += delegate
		{
			button.Modulate = Colors.White;
		};
		button.ButtonDown += delegate
		{
			button.Modulate = new Color(0.76f, 0.76f, 0.76f);
		};
		button.ButtonUp += delegate
		{
			button.Modulate = Colors.White;
		};
		if (pressed != null)
		{
			button.Pressed += pressed;
		}
		return button;
	}
}
