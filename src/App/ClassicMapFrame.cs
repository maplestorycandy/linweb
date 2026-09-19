using System;
using Godot;

namespace IdleLineage.App;

internal static class ClassicMapFrame
{
	private const string AssetRoot = "res://assets/ui/classic/map/";

	private const string ColourLayerPath = "res://assets/ui/classic/map/8970.png";

	private const string OpaqueMaskPath = "res://assets/ui/classic/map/8971.png";

	private const string MaskShaderPath = "res://assets/ui/classic/map/classic_map_base.gdshader";

	private const string CloseTexturePath = "res://assets/ui/classic/npc_dialog/9183.png";

	private const int Slice = 38;

	private const string BaseNodeName = "MapWindowBase8971";

	private const string FrameNodeName = "MapWindowFrame8970";

	private const string BodyNodeName = "MapWindowBody";

	private const string CloseNodeName = "MapWindowClose";

	public const float PadLeft = 18f;

	public const float PadTop = 24f;

	public const float PadRight = 18f;

	public const float PadBottom = 18f;

	public const float TitleContentTop = 30f;

	private const float TitleSafeLeft = 46f;

	private const float TitleSafeTop = 26f;

	public static Vector2 BodySize(Vector2 frameSize)
	{
		return new Vector2(Mathf.Max(0f, frameSize.X - 18f - 18f), Mathf.Max(0f, frameSize.Y - 24f - 18f));
	}

	public static (Control Root, Control Body) Create(Vector2 position, Vector2 size, Action? onClose, int zIndex = 0)
	{
		Vector2 vector = new Vector2(Mathf.Max(76f, size.X), Mathf.Max(76f, size.Y));
		Control control = new Control
		{
			Position = position,
			Size = vector,
			ZIndex = zIndex,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		ShaderMaterial material = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/classic/map/classic_map_base.gdshader")
		};
		control.AddChild(new TextureRect
		{
			Name = "MapWindowBase8971",
			Texture = GD.Load<Texture2D>("res://assets/ui/classic/map/8971.png"),
			Material = material,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Size = vector,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		control.AddChild(new NinePatchRect
		{
			Name = "MapWindowFrame8970",
			Texture = GD.Load<Texture2D>("res://assets/ui/classic/map/8970.png"),
			Size = vector,
			PatchMarginLeft = 38,
			PatchMarginTop = 38,
			PatchMarginRight = 38,
			PatchMarginBottom = 38,
			AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Stretch,
			AxisStretchVertical = NinePatchRect.AxisStretchMode.Stretch,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		Control control2 = new Control
		{
			Name = "MapWindowBody",
			Position = new Vector2(18f, 24f),
			Size = BodySize(vector),
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		control.AddChild(control2, forceReadableName: false, Node.InternalMode.Disabled);
		if (onClose != null)
		{
			Texture2D texture2D = GD.Load<Texture2D>("res://assets/ui/classic/npc_dialog/9183.png");
			TextureButton textureButton = new TextureButton
			{
				Name = "MapWindowClose",
				TextureNormal = texture2D,
				TextureHover = texture2D,
				TexturePressed = texture2D,
				IgnoreTextureSize = true,
				StretchMode = TextureButton.StretchModeEnum.Keep,
				Position = new Vector2(vector.X - 29f, 8f),
				Size = new Vector2(20f, 19f),
				FocusMode = Control.FocusModeEnum.None,
				TooltipText = "關閉"
			};
			textureButton.Pressed += onClose;
			control.AddChild(textureButton, forceReadableName: false, Node.InternalMode.Disabled);
		}
		return (Root: control, Body: control2);
	}

	public static void Resize(Control root, Vector2 size)
	{
		ArgumentNullException.ThrowIfNull(root, "root");
		Vector2 vector = (root.Size = new Vector2(Mathf.Max(76f, size.X), Mathf.Max(76f, size.Y)));
		TextureRect nodeOrNull = root.GetNodeOrNull<TextureRect>("MapWindowBase8971");
		if (nodeOrNull != null)
		{
			nodeOrNull.Size = vector;
		}
		NinePatchRect nodeOrNull2 = root.GetNodeOrNull<NinePatchRect>("MapWindowFrame8970");
		if (nodeOrNull2 != null)
		{
			nodeOrNull2.Size = vector;
		}
		Control nodeOrNull3 = root.GetNodeOrNull<Control>("MapWindowBody");
		if (nodeOrNull3 != null)
		{
			nodeOrNull3.Size = BodySize(vector);
		}
		TextureButton nodeOrNull4 = root.GetNodeOrNull<TextureButton>("MapWindowClose");
		if (nodeOrNull4 != null)
		{
			nodeOrNull4.Position = new Vector2(vector.X - 29f, 8f);
		}
	}

	public static void MakeScrollbarsTransparent(Node root)
	{
		ArgumentNullException.ThrowIfNull(root, "root");
		if (root is ScrollContainer scrollContainer)
		{
			MakeTransparent(scrollContainer.GetVScrollBar());
			MakeTransparent(scrollContainer.GetHScrollBar());
		}
		foreach (Node child in root.GetChildren())
		{
			MakeScrollbarsTransparent(child);
		}
	}

	private static void MakeTransparent(ScrollBar? bar)
	{
		if (bar != null)
		{
			string[] array = new string[5] { "scroll", "scroll_focus", "grabber", "grabber_highlight", "grabber_pressed" };
			foreach (string text in array)
			{
				bar.AddThemeStyleboxOverride(text, new StyleBoxEmpty());
			}
		}
	}

	public static Label Title(string text)
	{
		Label label = new Label();
		label.Text = text;
		label.Position = new Vector2(46f, 26f);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", 15);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#e5c974".AsSpan()));
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#17110b".AsSpan()));
		label.AddThemeConstantOverride("outline_size", 2);
		return label;
	}
}
