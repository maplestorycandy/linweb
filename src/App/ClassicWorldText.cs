using Godot;

namespace IdleLineage.App;

internal static class ClassicWorldText
{
	private static readonly Color OutlineColor = new Color(0.01f, 0.01f, 0.01f, 0.96f);

	public static void Apply(Label label, Color color, int fontSize, int outlineSize = 2)
	{
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", OutlineColor);
		label.AddThemeConstantOverride("outline_size", outlineSize);
	}
}
