using System;
using Godot;

namespace IdleLineage.App;

internal sealed partial class ClassicHyperTextScrollBar : Control
{
	private const float TrackTop = 14f;

	private const float TrackHeight = 223f;

	private const float ThumbHeight = 16f;

	private readonly ScrollContainer _target;

	private readonly TextureButton _thumb;

	private bool _dragging;

	public ClassicHyperTextScrollBar(ScrollContainer target)
	{
		_target = target;
		base.Name = "HyperTextScrollBar";
		base.Size = new Vector2(17f, 247f);
		base.MouseFilter = MouseFilterEnum.Stop;
		AddChild(Texture("ScrollTrack", "1970.png", new Vector2(0f, 14f), new Vector2(12f, 224f)), forceReadableName: false, InternalMode.Disabled);
		AddChild(Arrow("ScrollUp", "2042.png", "2043.png", 0f, -24), forceReadableName: false, InternalMode.Disabled);
		AddChild(Arrow("ScrollDown", "2040.png", "2041.png", 237f, 24), forceReadableName: false, InternalMode.Disabled);
		_thumb = new TextureButton
		{
			Name = "ScrollThumb",
			Position = new Vector2(0f, 14f),
			Size = new Vector2(11f, 16f),
			TextureNormal = ClassicWindowFrame.Load("2044.png"),
			TexturePressed = ClassicWindowFrame.Load("2045.png"),
			TextureHover = ClassicWindowFrame.Load("2045.png"),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Keep,
			MouseFilter = MouseFilterEnum.Stop
		};
		_thumb.GuiInput += HandleThumbInput;
		AddChild(_thumb, forceReadableName: false, InternalMode.Disabled);
		SetProcess(enable: true);
	}

	public override void _Process(double delta)
	{
		VScrollBar vScrollBar = _target.GetVScrollBar();
		double num = Math.Max(0.0, vScrollBar.MaxValue - vScrollBar.Page);
		float num2 = ((num <= 0.0) ? 0f : ((float)((double)_target.ScrollVertical / num)));
		float s = 14f + num2 * 207f;
		_thumb.Position = new Vector2(0f, Mathf.Round(s));
		base.Visible = num > 0.0;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed)
		{
			SetFromThumbY(inputEventMouseButton.Position.Y - 8f);
			AcceptEvent();
		}
	}

	private void HandleThumbInput(InputEvent inputEvent)
	{
		if (!(inputEvent is InputEventMouseButton inputEventMouseButton))
		{
			if (inputEvent is InputEventMouseMotion inputEventMouseMotion && _dragging)
			{
				SetFromThumbY(_thumb.Position.Y + inputEventMouseMotion.Relative.Y);
				_thumb.AcceptEvent();
			}
		}
		else if (inputEventMouseButton.ButtonIndex == MouseButton.Left)
		{
			_dragging = inputEventMouseButton.Pressed;
			_thumb.AcceptEvent();
		}
	}

	private void SetFromThumbY(float y)
	{
		float num = 207f;
		float num2 = Mathf.Clamp(y, 14f, 14f + num);
		VScrollBar vScrollBar = _target.GetVScrollBar();
		double num3 = Math.Max(0.0, vScrollBar.MaxValue - vScrollBar.Page);
		_target.ScrollVertical = (int)Math.Round(num3 * (double)((num2 - 14f) / num));
	}

	private TextureButton Arrow(string name, string normal, string pressed, float y, int amount)
	{
		TextureButton textureButton = new TextureButton();
		textureButton.Name = name;
		textureButton.Position = new Vector2(0f, y);
		textureButton.Size = new Vector2(12f, 14f);
		textureButton.TextureNormal = ClassicWindowFrame.Load(normal);
		textureButton.TexturePressed = ClassicWindowFrame.Load(pressed);
		textureButton.TextureHover = ClassicWindowFrame.Load(normal);
		textureButton.IgnoreTextureSize = true;
		textureButton.StretchMode = TextureButton.StretchModeEnum.Keep;
		textureButton.MouseFilter = MouseFilterEnum.Stop;
		textureButton.Pressed += delegate
		{
			_target.ScrollVertical += amount;
		};
		return textureButton;
	}

	private static TextureRect Texture(string name, string file, Vector2 position, Vector2 size)
	{
		return new TextureRect
		{
			Name = name,
			Texture = ClassicWindowFrame.Load(file),
			Position = position,
			Size = size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		};
	}
}
