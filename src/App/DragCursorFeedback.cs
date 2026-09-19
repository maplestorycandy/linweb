using Godot;

namespace IdleLineage.App;

internal static class DragCursorFeedback
{
	public static bool IsDragging { get; private set; }

	public static void Begin()
	{
		IsDragging = true;
		Input.SetDefaultCursorShape(Input.CursorShape.Drag);
	}

	public static void End()
	{
		IsDragging = false;
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
	}
}
