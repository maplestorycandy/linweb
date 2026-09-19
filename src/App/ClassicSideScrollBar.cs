using System;
using Godot;

namespace IdleLineage.App;

internal sealed partial class ClassicSideScrollBar : Control
{
	private const string AssetRoot = "res://assets/ui/scrollbar";

	private readonly Func<double> _getValue;

	private readonly Func<double> _getMaximum;

	private readonly Action<double> _setValue;

	private readonly double _step;

	private readonly bool _hideWhenUnused;

	private readonly float _arrowHeight;

	private readonly float _thumbHeight;

	private readonly TextureButton _up;

	private readonly TextureButton _down;

	private readonly TextureButton _thumb;

	private bool _dragging;

	private float _dragOffsetY;

	public ClassicSideScrollBar(Func<double> getValue, Func<double> getMaximum, Action<double> setValue, double step, float height, float scale = 1f, bool hideWhenUnused = true)
	{
		_getValue = getValue ?? throw new ArgumentNullException("getValue");
		_getMaximum = getMaximum ?? throw new ArgumentNullException("getMaximum");
		_setValue = setValue ?? throw new ArgumentNullException("setValue");
		_step = Math.Max(double.Epsilon, step);
		_hideWhenUnused = hideWhenUnused;
		_arrowHeight = 14f * scale;
		_thumbHeight = 16f * scale;
		float num = 12f * scale;
		base.Size = new Vector2(num, height);
		base.CustomMinimumSize = base.Size;
		base.MouseFilter = MouseFilterEnum.Stop;
		AddChild(new TextureRect
		{
			Name = "OfficialScrollTrack2039",
			Texture = Load("2039.png"),
			Position = new Vector2(0f, _arrowHeight),
			Size = new Vector2(num, Math.Max(0f, height - _arrowHeight * 2f)),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_up = Arrow("OfficialScrollUp", "2042.png", "2043.png", 0f, num, scale);
		_down = Arrow("OfficialScrollDown", "2040.png", "2041.png", height - _arrowHeight, num, scale);
		_up.Pressed += delegate
		{
			SetClamped(_getValue() - _step);
		};
		_down.Pressed += delegate
		{
			SetClamped(_getValue() + _step);
		};
		_up.GuiInput += HandleChildWheelInput;
		_down.GuiInput += HandleChildWheelInput;
		AddChild(_up, forceReadableName: false, InternalMode.Disabled);
		AddChild(_down, forceReadableName: false, InternalMode.Disabled);
		_thumb = new TextureButton
		{
			Name = "OfficialScrollThumb",
			TextureNormal = Load("2044.png"),
			TextureHover = Load("2044.png"),
			TexturePressed = Load("2045.png"),
			Position = new Vector2((num - 11f * scale) * 0.5f, _arrowHeight),
			Size = new Vector2(11f * scale, _thumbHeight),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Stop,
			FocusMode = FocusModeEnum.None
		};
		_thumb.GuiInput += HandleThumbInput;
		AddChild(_thumb, forceReadableName: false, InternalMode.Disabled);
		SetProcess(enable: true);
	}

	public static ClassicSideScrollBar ForScrollContainer(ScrollContainer target, float height, double step, bool hideWhenUnused = true)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		return new ClassicSideScrollBar(() => target.ScrollVertical, () => Math.Max(0.0, target.GetVScrollBar().MaxValue - target.GetVScrollBar().Page), delegate(double value)
		{
			target.ScrollVertical = (int)Math.Round(value);
		}, step, height, 1f, hideWhenUnused);
	}

	public static ClassicSideScrollBar ForRange(Godot.Range target, float height, double step, bool hideWhenUnused = true)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		return new ClassicSideScrollBar(() => target.Value, () => Math.Max(0.0, target.MaxValue - target.Page), delegate(double value)
		{
			target.Value = value;
		}, step, height, 1f, hideWhenUnused);
	}

	public static ClassicSideScrollBar ForPages(BaseButton previous, BaseButton next, Label pageLabel, float height, float scale, bool hideWhenUnused = true)
	{
		ArgumentNullException.ThrowIfNull(previous, "previous");
		ArgumentNullException.ThrowIfNull(next, "next");
		ArgumentNullException.ThrowIfNull(pageLabel, "pageLabel");
		return new ClassicSideScrollBar(() => PageNumbers(pageLabel.Text).Current, () => Math.Max(0, PageNumbers(pageLabel.Text).Count - 1), delegate(double value)
		{
			(int Current, int Count) tuple = PageNumbers(pageLabel.Text);
			int item = tuple.Current;
			int item2 = tuple.Count;
			int num = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, item2 - 1));
			BaseButton baseButton = ((num < item) ? previous : next);
			for (int i = 0; i < Math.Abs(num - item); i++)
			{
				baseButton.EmitSignal(BaseButton.SignalName.Pressed);
			}
		}, 1.0, height, scale, hideWhenUnused);
	}

	public override void _Process(double delta)
	{
		if (_dragging)
		{
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				SetFromThumbY(GetLocalMousePosition().Y - _dragOffsetY);
			}
			else
			{
				_dragging = false;
			}
		}
		double num = Math.Max(0.0, _getMaximum());
		double num2 = Math.Clamp(_getValue(), 0.0, num);
		float num3 = Math.Max(0f, base.Size.Y - _arrowHeight * 2f - _thumbHeight);
		float num4 = ((num <= 0.0) ? 0f : ((float)(num2 / num)));
		_thumb.Position = new Vector2(_thumb.Position.X, Mathf.Round(_arrowHeight + num3 * num4));
		_up.Disabled = num <= 0.0 || num2 <= 0.0;
		_down.Disabled = num <= 0.0 || num2 >= num;
		base.Visible = !_hideWhenUnused || num > 0.0;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (TryScrollByWheel(inputEvent))
		{
			AcceptEvent();
		}
		else if (inputEvent is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed && !(inputEventMouseButton.Position.Y < _arrowHeight) && !(inputEventMouseButton.Position.Y > base.Size.Y - _arrowHeight))
		{
			SetFromThumbY(inputEventMouseButton.Position.Y - _thumbHeight * 0.5f);
			AcceptEvent();
		}
	}

	private void HandleThumbInput(InputEvent inputEvent)
	{
		if (TryScrollByWheel(inputEvent))
		{
			_thumb.AcceptEvent();
		}
		else if (inputEvent is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left)
		{
			_dragging = inputEventMouseButton.Pressed;
			if (inputEventMouseButton.Pressed)
			{
				_dragOffsetY = inputEventMouseButton.Position.Y;
			}
			_thumb.AcceptEvent();
		}
	}

	private void HandleChildWheelInput(InputEvent inputEvent)
	{
		if (TryScrollByWheel(inputEvent))
		{
			if (inputEvent is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				_up.AcceptEvent();
			}
			else
			{
				_down.AcceptEvent();
			}
		}
	}

	private bool TryScrollByWheel(InputEvent inputEvent)
	{
		if (!(inputEvent is InputEventMouseButton { Pressed: not false, ButtonIndex: var buttonIndex }))
		{
			return false;
		}
		double num = buttonIndex switch
		{
			MouseButton.WheelUp => 0.0 - _step, 
			MouseButton.WheelDown => _step, 
			_ => 0.0, 
		};
		if (num == 0.0)
		{
			return false;
		}
		SetClamped(_getValue() + num);
		return true;
	}

	private void SetFromThumbY(float y)
	{
		float num = Math.Max(0f, base.Size.Y - _arrowHeight * 2f - _thumbHeight);
		if (!(num <= 0f))
		{
			float num2 = Mathf.Clamp(y, _arrowHeight, _arrowHeight + num);
			SetClamped(_getMaximum() * (double)((num2 - _arrowHeight) / num));
		}
	}

	private void SetClamped(double value)
	{
		_setValue(Math.Clamp(value, 0.0, Math.Max(0.0, _getMaximum())));
	}

	private TextureButton Arrow(string name, string normal, string pressed, float y, float width, float scale)
	{
		Texture2D texture2D = Load(normal);
		return new TextureButton
		{
			Name = name,
			TextureNormal = texture2D,
			TextureHover = texture2D,
			TextureDisabled = texture2D,
			TexturePressed = Load(pressed),
			Position = new Vector2(0f, y),
			Size = new Vector2(width, 14f * scale),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			FocusMode = FocusModeEnum.None
		};
	}

	private static (int Current, int Count) PageNumbers(string text)
	{
		string[] array = text.Split('/', StringSplitOptions.TrimEntries);
		if (array.Length != 2 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2))
		{
			return (Current: 0, Count: 1);
		}
		return (Current: Math.Max(0, result - 1), Count: Math.Max(1, result2));
	}

	private static Texture2D Load(string fileName)
	{
		return GD.Load<Texture2D>("res://assets/ui/scrollbar/" + fileName);
	}
}
