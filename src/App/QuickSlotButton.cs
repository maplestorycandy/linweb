using System;
using Godot;

namespace IdleLineage.App;

public sealed partial class QuickSlotButton : IconSlotButton
{
	public int Slot;

	public Action<int, string>? OnDropItem;

	public Action? OnActivate;

	public string DragPayload = "";

	public string DragLabel = "";

	public Action<int>? OnDragOut;

	private bool _dragging;

	private bool _clickCandidate;

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (!(inputEvent is InputEventMouseButton inputEventMouseButton) || inputEventMouseButton.ButtonIndex != MouseButton.Left)
		{
			return;
		}
		if (inputEventMouseButton.Pressed)
		{
			_clickCandidate = true;
			return;
		}
		bool num = _clickCandidate && !_dragging;
		_clickCandidate = false;
		if (num)
		{
			AcceptEvent();
			OnActivate?.Invoke();
		}
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType == Variant.Type.String)
		{
			return data.AsString().Length > 0;
		}
		return false;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		OnDropItem?.Invoke(Slot, data.AsString());
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (DragPayload.Length == 0)
		{
			return default(Variant);
		}
		_clickCandidate = false;
		_dragging = true;
		DragCursorFeedback.Begin();
		Label label = new Label
		{
			Text = "→ " + DragLabel
		};
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		SetDragPreview(label);
		return DragPayload;
	}

	public override void _Notification(int what)
	{
		if (!_dragging)
		{
			return;
		}
		if ((long)what == 11)
		{
			_dragging = false;
			DragCursorFeedback.End();
		}
		else if ((long)what == 22)
		{
			_dragging = false;
			DragCursorFeedback.End();
			if (!GetViewport().GuiIsDragSuccessful())
			{
				OnDragOut?.Invoke(Slot);
			}
		}
	}
}
