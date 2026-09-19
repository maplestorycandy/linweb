using Godot;

namespace IdleLineage.App;

internal sealed partial class SkillDragButton : IconSlotButton
{
	public string SkillId = "";

	public bool Draggable;

	private bool _dragging;

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (!Draggable || SkillId.Length == 0)
		{
			return default(Variant);
		}
		_dragging = true;
		DragCursorFeedback.Begin();
		Label label = new Label
		{
			Text = "→ " + SkillInfo.Name(SkillId)
		};
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		SetDragPreview(label);
		return "skill:" + SkillId;
	}

	public override void _Notification(int what)
	{
		if (_dragging && ((long)what == 22 || (long)what == 11))
		{
			_dragging = false;
			DragCursorFeedback.End();
		}
	}
}
