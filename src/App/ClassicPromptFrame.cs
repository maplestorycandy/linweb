using System;
using Godot;

namespace IdleLineage.App;

internal static class ClassicPromptFrame
{
	private const string AssetRoot = "res://assets/ui/windows";

	public static readonly Vector2 FrameSize = new Vector2(161f, 124f);

	private static readonly Vector2 PlatePosition = new Vector2(4f, 9f);

	private static readonly Vector2 PlateSize = new Vector2(153f, 112f);

	public static readonly Rect2 BodyRect = new Rect2(new Vector2(20f, 33f), new Vector2(121f, 76f));

	private const string BodyNodeName = "PromptBody";

	public static (Control Root, Control Body) Create(Vector2 position, Action? onClose, int zIndex = 0)
	{
		Control control = new Control
		{
			Position = position,
			Size = FrameSize,
			ZIndex = zIndex,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		control.AddChild(Art("1951.png", PlatePosition, PlateSize), forceReadableName: false, Node.InternalMode.Disabled);
		control.AddChild(Art("1952.png", Vector2.Zero, FrameSize), forceReadableName: false, Node.InternalMode.Disabled);
		if (onClose != null)
		{
			Button button = new Button
			{
				Flat = true,
				Text = "",
				Position = new Vector2(143f, 12f),
				Size = new Vector2(15f, 14f),
				FocusMode = Control.FocusModeEnum.None,
				TooltipText = "關閉"
			};
			button.Pressed += onClose;
			control.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		}
		Control control2 = new Control
		{
			Name = "PromptBody",
			Position = BodyRect.Position,
			Size = BodyRect.Size,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		control.AddChild(control2, forceReadableName: false, Node.InternalMode.Disabled);
		return (Root: control, Body: control2);
	}

	private static TextureRect Art(string fileName, Vector2 position, Vector2 size)
	{
		return new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/windows/" + fileName),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Position = position,
			Size = size,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}
}
