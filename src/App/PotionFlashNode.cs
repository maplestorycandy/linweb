using System;
using Godot;

namespace IdleLineage.App;

public sealed partial class PotionFlashNode : AnimatedSprite2D
{
	private static readonly Vector2 UpperBodyOffset = new Vector2(0f, -40f);

	private const float FlashScale = 0.5f;

	private const float FlashAlpha = 0.6f;

	private Func<Vector2>? _follow;

	public void Start(SpriteFrames frames, Func<Vector2> follow)
	{
		_follow = follow;
		base.SpriteFrames = frames;
		base.TextureFilter = TextureFilterEnum.Nearest;
		base.Scale = new Vector2(0.5f, 0.5f);
		base.Modulate = new Color(1f, 1f, 1f, 0.6f);
		Vector2 vector = follow();
		base.Position = vector + UpperBodyOffset;
		base.ZIndex = Depth.Of(vector.Y, 3);
		base.AnimationFinished += base.QueueFree;
		Play("flash");
	}

	public override void _Process(double delta)
	{
		if (_follow != null)
		{
			Vector2 vector = _follow();
			base.Position = vector + UpperBodyOffset;
			base.ZIndex = Depth.Of(vector.Y, 3);
		}
	}
}
