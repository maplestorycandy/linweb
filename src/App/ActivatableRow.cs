using System;
using Godot;

namespace IdleLineage.App;

public partial class ActivatableRow : HBoxContainer
{
	public Action? OnActivate;

	public override void _GuiInput(InputEvent @event)
	{
		if (OnActivate != null && @event is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed && inputEventMouseButton.DoubleClick)
		{
			AcceptEvent();
			OnActivate();
		}
	}
}
