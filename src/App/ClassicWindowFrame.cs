using System;
using Godot;

namespace IdleLineage.App;

internal static class ClassicWindowFrame
{
	private const string AssetRoot = "res://assets/ui/classic/npc_dialog/";

	internal const int TextInset = 6;

	public static readonly ClassicWindowSpec HyperText = new ClassicWindowSpec(new Vector2(239f, 317f), "res://assets/ui/classic/npc_dialog/1969.png", new Vector2(6f, 9f), new Vector2(227f, 304f), "res://assets/ui/classic/npc_dialog/1968.png", new Vector2(15f, 38f), new Vector2(196f, 247f), new Vector2(217f, 38f), new Vector2(215f, 9f));

	public static (Control Root, ScrollContainer Scroll, VBoxContainer Content) Create(Vector2 position, ClassicWindowSpec spec, Action onClose, int zIndex = 0)
	{
		ArgumentNullException.ThrowIfNull(spec, "spec");
		ArgumentNullException.ThrowIfNull(onClose, "onClose");
		Control control = new Control
		{
			Name = "ClassicHyperTextWindow",
			Position = new Vector2(Mathf.Round(position.X), Mathf.Round(position.Y)),
			Size = spec.Size,
			CustomMinimumSize = spec.Size,
			ZIndex = zIndex,
			MouseFilter = Control.MouseFilterEnum.Stop,
			ClipContents = false
		};
		control.AddChild(Image("HyperTextBase", spec.BaseTexture, spec.BasePosition, spec.BaseSize), forceReadableName: false, Node.InternalMode.Disabled);
		control.AddChild(Image("HyperTextFrame", spec.FrameTexture, Vector2.Zero, spec.Size), forceReadableName: false, Node.InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Name = "HyperTextScroll",
			Position = spec.ContentPosition,
			Size = spec.ContentSize,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		MarginContainer marginContainer = new MarginContainer
		{
			Name = "HyperTextPadding"
		};
		string[] array = new string[2] { "margin_left", "margin_right" };
		foreach (string text in array)
		{
			marginContainer.AddThemeConstantOverride(text, 6);
		}
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Name = "HyperTextContent",
			CustomMinimumSize = new Vector2(spec.ContentSize.X - 12f, 0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 5);
		marginContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		scrollContainer.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		control.AddChild(scrollContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		control.AddChild(new ClassicHyperTextScrollBar(scrollContainer)
		{
			Position = spec.ScrollBarPosition
		}, forceReadableName: false, Node.InternalMode.Disabled);
		TextureButton textureButton = new TextureButton
		{
			Name = "HyperTextClose",
			Position = spec.ClosePosition,
			Size = new Vector2(20f, 19f),
			TextureNormal = Load("9183.png"),
			TextureHover = Load("9183.png"),
			TexturePressed = Load("9183.png"),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Keep,
			TooltipText = "關閉",
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		textureButton.Pressed += onClose;
		control.AddChild(textureButton, forceReadableName: false, Node.InternalMode.Disabled);
		return (Root: control, Scroll: scrollContainer, Content: vBoxContainer);
	}

	internal static Texture2D Load(string fileName)
	{
		return GD.Load<Texture2D>("res://assets/ui/classic/npc_dialog/" + fileName);
	}

	private static TextureRect Image(string name, string texturePath, Vector2 position, Vector2 size)
	{
		return new TextureRect
		{
			Name = name,
			Texture = GD.Load<Texture2D>(texturePath),
			Position = position,
			Size = size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}
}
