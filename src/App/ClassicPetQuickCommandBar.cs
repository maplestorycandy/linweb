using System;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

internal sealed partial class ClassicPetQuickCommandBar : Control
{
	private const string AssetRoot = "res://assets/ui/pet-commands";

	private static readonly Vector2 NativeSize = new Vector2(144f, 39f);

	public ClassicPetQuickCommandBar(float scale, Action<PetCommandStatus> execute)
	{
		ArgumentNullException.ThrowIfNull(execute, "execute");
		base.Size = NativeSize * scale;
		base.CustomMinimumSize = base.Size;
		base.MouseFilter = MouseFilterEnum.Stop;
		AddSurface(2242, scale);
		AddSurface(2243, scale);
		AddCommand(2244, 2245, new Vector2(11f, 14f), PetCommandStatus.Aggressive, "積極攻擊", scale, execute);
		AddCommand(2250, 2251, new Vector2(36f, 15f), PetCommandStatus.Defensive, "防禦主人", scale, execute);
		AddCommand(2790, 2791, new Vector2(61f, 15f), PetCommandStatus.Extend, "散開", scale, execute);
		AddCommand(2788, 2789, new Vector2(86f, 15f), PetCommandStatus.Alert, "警戒", scale, execute);
		AddCommand(2248, 2249, new Vector2(111f, 14f), PetCommandStatus.Stay, "休息／停止", scale, execute);
	}

	private void AddSurface(int surface, float scale)
	{
		AddChild(new TextureRect
		{
			Texture = Load(surface),
			Size = NativeSize * scale,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
	}

	private void AddCommand(int normalSurface, int pressedSurface, Vector2 nativePosition, PetCommandStatus command, string tooltip, float scale, Action<PetCommandStatus> execute)
	{
		Texture2D texture2D = Load(normalSurface);
		TextureButton textureButton = new TextureButton
		{
			TextureNormal = texture2D,
			TextureHover = texture2D,
			TexturePressed = Load(pressedSurface),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			Position = nativePosition * scale,
			Size = new Vector2(22f, 22f) * scale,
			FocusMode = FocusModeEnum.None,
			TooltipText = tooltip
		};
		textureButton.Pressed += delegate
		{
			execute(command);
		};
		AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);
	}

	private static Texture2D Load(int surface)
	{
		return GD.Load<Texture2D>($"{"res://assets/ui/pet-commands"}/{surface}.png");
	}
}
