using System;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

internal static class ItemBlessingGlow
{
	internal const string ShaderPath = "res://assets/ui/item_blessing_glow.gdshader";

	internal static TextureRect? Create(Texture2D? texture, ItemBlessing blessing, TextureRect.StretchModeEnum stretchMode)
	{
		if (texture == null || blessing == ItemBlessing.Normal)
		{
			return null;
		}
		ShaderMaterial shaderMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/item_blessing_glow.gdshader")
		};
		shaderMaterial.SetShaderParameter("glow_color", (blessing == ItemBlessing.Blessed) ? Color.FromHtml("#f2c14e".AsSpan()) : Color.FromHtml("#e2938f".AsSpan()));
		return new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Texture = texture,
			StretchMode = stretchMode,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = shaderMaterial
		};
	}
}
