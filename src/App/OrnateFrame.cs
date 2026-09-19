using System;
using Godot;

namespace IdleLineage.App;

public static class OrnateFrame
{
	public const string BaseTexturePath = "res://assets/ui/classic/npc_dialog/1968.png";

	public const string FrameTexturePath = "res://assets/ui/classic/npc_dialog/1969.png";

	public const string CloseTexturePath = "res://assets/ui/classic/npc_dialog/9183.png";

	public const float PadLeft = 17f;

	public const float PadTop = 34f;

	public const float PadRight = 27f;

	public const float PadBottom = 12f;

	private const float CloseBadgeY = 9f;

	private const float CloseBadgeInset = 24f;

	private static readonly Vector2 CloseHitSize = new Vector2(20f, 22f);

	private const int FrameSliceLeft = 24;

	private const int FrameSliceTop = 34;

	private const int FrameSliceRight = 27;

	private const int FrameSliceBottom = 18;

	private static readonly Vector2 BaseInset = new Vector2(6f, 9f);

	private static readonly Vector2 MinimumSize = new Vector2(84f, 86f);

	public static Vector2 BodySize(Vector2 frameSize)
	{
		return new Vector2(Mathf.Max(0f, frameSize.X - 17f - 27f), Mathf.Max(0f, frameSize.Y - 34f - 12f));
	}

	public static (Control Root, Control Body) Create(Vector2 position, Vector2 size, Action? onClose, int zIndex = 0)
	{
		Vector2 vector = new Vector2(Mathf.Max(MinimumSize.X, size.X), Mathf.Max(MinimumSize.Y, size.Y));
		Control control = new Control
		{
			Position = position,
			Size = vector,
			ZIndex = zIndex,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		control.AddChild(new NinePatchRect
		{
			Name = "OrnateBase1968",
			Texture = GD.Load<Texture2D>("res://assets/ui/classic/npc_dialog/1968.png"),
			Position = BaseInset,
			Size = new Vector2(vector.X - 12f, vector.Y - 13f),
			PatchMarginLeft = 17,
			PatchMarginTop = 34,
			PatchMarginRight = 27,
			PatchMarginBottom = 12,
			AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Stretch,
			AxisStretchVertical = NinePatchRect.AxisStretchMode.Stretch,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		control.AddChild(new NinePatchRect
		{
			Name = "OrnateFrame1969",
			Texture = GD.Load<Texture2D>("res://assets/ui/classic/npc_dialog/1969.png"),
			Size = vector,
			PatchMarginLeft = 24,
			PatchMarginTop = 34,
			PatchMarginRight = 27,
			PatchMarginBottom = 18,
			AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Stretch,
			AxisStretchVertical = NinePatchRect.AxisStretchMode.Stretch,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		if (onClose != null)
		{
			Texture2D texture2D = GD.Load<Texture2D>("res://assets/ui/classic/npc_dialog/9183.png");
			TextureButton textureButton = new TextureButton
			{
				Name = "OrnateClose9183",
				TextureNormal = texture2D,
				TextureHover = texture2D,
				TexturePressed = texture2D,
				IgnoreTextureSize = true,
				StretchMode = TextureButton.StretchModeEnum.Keep,
				Position = new Vector2(vector.X - 24f, 9f),
				Size = CloseHitSize,
				FocusMode = Control.FocusModeEnum.None,
				TooltipText = "關閉"
			};
			textureButton.Pressed += onClose;
			control.AddChild(textureButton, forceReadableName: false, Node.InternalMode.Disabled);
		}
		Control control2 = new Control
		{
			Position = new Vector2(17f, 34f),
			Size = BodySize(vector),
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		control.AddChild(control2, forceReadableName: false, Node.InternalMode.Disabled);
		return (Root: control, Body: control2);
	}

	public static Label Title(string text)
	{
		return new Label
		{
			Text = text,
			Position = new Vector2(2f, 0f),
			LabelSettings = new LabelSettings
			{
				FontSize = 15,
				FontColor = new Color(0.88f, 0.78f, 0.45f)
			}
		};
	}
}
