using System;
using Godot;

namespace IdleLineage.App;

internal sealed partial class ClassicSlotButton : IconSlotButton
{
	public Func<(bool Ok, string Text)>? OnActivate;

	public string ItemKey { get; init; } = "";

	public override Control? _MakeCustomTooltip(string forText)
	{
		return ClassicItemTooltip.Build(forText, ItemKey);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (OnActivate != null && @event is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed && inputEventMouseButton.DoubleClick)
		{
			AcceptEvent();
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
			OnActivate();
		}
	}
}
