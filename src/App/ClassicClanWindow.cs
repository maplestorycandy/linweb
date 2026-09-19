using System;
using Godot;

namespace IdleLineage.App;

internal static class ClassicClanWindow
{
	private const string AssetRoot = "res://assets/ui/clan";

	private const string DialogAssetRoot = "res://assets/ui/classic/npc_dialog";

	public static readonly Vector2 NativeSize = new Vector2(274f, 413f);

	private static readonly Vector2 ContentPosition = new Vector2(19f, 82f);

	private static readonly Vector2 ContentSize = new Vector2(230f, 298f);

	public static (Control Root, VBoxContainer Body) Create(Vector2 viewport, string title, Action onClose)
	{
		ArgumentNullException.ThrowIfNull(onClose, "onClose");
		Control control = new Control();
		control.Name = "ClassicClanWindow";
		control.Position = (viewport - NativeSize) * 0.5f;
		control.Size = NativeSize;
		control.MouseFilter = Control.MouseFilterEnum.Stop;
		control.AddChild(new TextureRect
		{
			Name = "OfficialPledgeFrame4451",
			Texture = GD.Load<Texture2D>("res://assets/ui/clan/4451.png"),
			Size = NativeSize,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		Panel panel = new Panel
		{
			Name = "LocalizedPledgeHeader",
			Position = new Vector2(18f, 42f),
			Size = new Vector2(232f, 38f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#160d0def".AsSpan()),
			BorderColor = Color.FromHtml("#4b3228".AsSpan()),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1
		});
		control.AddChild(panel, forceReadableName: false, Node.InternalMode.Disabled);
		Label label = new Label
		{
			Name = "LocalizedPledgeTitle",
			Text = title,
			Position = new Vector2(22f, 48f),
			Size = new Vector2(224f, 25f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#c6b78f".AsSpan()));
		control.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Name = "PledgeContentScroll",
			Position = ContentPosition,
			Size = ContentSize,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Name = "PledgeContent",
			CustomMinimumSize = new Vector2(ContentSize.X - 16f, 0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 6);
		scrollContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		control.AddChild(scrollContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicSideScrollBar classicSideScrollBar = ClassicSideScrollBar.ForScrollContainer(scrollContainer, ContentSize.Y, 32.0, hideWhenUnused: false);
		classicSideScrollBar.Name = "OfficialPledgeScrollBar";
		classicSideScrollBar.Position = new Vector2(252f, ContentPosition.Y);
		control.AddChild(classicSideScrollBar, forceReadableName: false, Node.InternalMode.Disabled);
		TextureButton textureButton = new TextureButton
		{
			Name = "OfficialPledgeClose",
			TextureNormal = LoadDialog("9183.png"),
			TextureHover = LoadDialog("9183.png"),
			TexturePressed = LoadDialog("9183.png"),
			Position = new Vector2(253f, 5f),
			Size = new Vector2(20f, 19f),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = "關閉"
		};
		textureButton.Pressed += onClose;
		control.AddChild(textureButton, forceReadableName: false, Node.InternalMode.Disabled);
		return (Root: control, Body: vBoxContainer);
	}

	private static Texture2D LoadDialog(string file)
	{
		return GD.Load<Texture2D>("res://assets/ui/classic/npc_dialog/" + file);
	}
}
