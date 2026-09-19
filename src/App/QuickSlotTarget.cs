using System;
using Godot;

namespace IdleLineage.App;

public sealed partial class QuickSlotTarget : Control
{
	public int Slot;

	public Action<int, string>? OnDropItem;

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
}
