using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class L1jNpcSpriteRenderer
{
	public const string WalkAction = "walk";

	public const string AttackAction = "attack";

	public const string DamageAction = "damage";

	public const string DeathAction = "death";

	private static readonly Dictionary<(int Gfx, int Heading), Rect2> VisualBounds = new Dictionary<(int, int), Rect2>();

	public static bool TryMeasureVisualBounds(IGameData data, int gfx, int? heading, out Rect2 bounds)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		bounds = default(Rect2);
		if (!L1jNpcSpriteCatalog.TryGet(data, gfx, out L1jGfxSprite sprite) || !sprite.Actions.TryGetValue("idle", out L1jSpriteAction value))
		{
			return false;
		}
		int num = sprite.ResolveHeading(heading);
		if (VisualBounds.TryGetValue((gfx, num), out bounds))
		{
			if (bounds.Size.X > 0f)
			{
				return bounds.Size.Y > 0f;
			}
			return false;
		}
		string path = $"res://assets/npc/{gfx}/{value.Prefix}_h{num}_0.png";
		Texture2D? texture2D = GodotDataFiles.TryLoadTexture(path);
		if (texture2D != null)
		{
			Image? image = texture2D.GetImage();
			if (image == null)
			{
				string? disk = GodotDataFiles.TryResolveDiskPath(path);
				if (disk != null && File.Exists(disk))
				{
					image = Image.LoadFromFile(disk);
				}
			}
			if (image != null && image.GetWidth() > 0 && image.GetHeight() > 0)
			{
				Rect2I rect2I = image.GetUsedRect();
				foreach (L1jSpriteLayer renderedClothe in sprite.RenderedClothes)
				{
					string path2 = $"res://assets/npc/{gfx}/{value.Prefix}{renderedClothe.Suffix}_h{num}_0.png";
					Texture2D? texture2D2 = GodotDataFiles.TryLoadTexture(path2);
					if (texture2D2 == null)
					{
						continue;
					}
					Image? image2 = texture2D2.GetImage();
					if (image2 == null)
					{
						string? disk2 = GodotDataFiles.TryResolveDiskPath(path2);
						if (disk2 != null && File.Exists(disk2))
						{
							image2 = Image.LoadFromFile(disk2);
						}
					}
					if (image2 != null)
					{
						Rect2I usedRect = image2.GetUsedRect();
						if (usedRect.Size.X > 0 && usedRect.Size.Y > 0)
						{
							rect2I = ((rect2I.Size.X > 0 && rect2I.Size.Y > 0) ? rect2I.Merge(usedRect) : usedRect);
						}
					}
				}
				if (rect2I.Size.X > 0 && rect2I.Size.Y > 0)
				{
					L1jSpriteBox box = sprite.Box;
					Vector2 vector = (((object)box != null) ? new Vector2((float)box.X0 - 24f, box.Y0) : new Vector2((float)(-image.GetWidth()) * 0.5f, -image.GetHeight()));
					bounds = new Rect2(vector + new Vector2(rect2I.Position.X, rect2I.Position.Y), new Vector2(rect2I.Size.X, rect2I.Size.Y));
				}
			}
		}
		VisualBounds[(gfx, num)] = bounds;
		if (bounds.Size.X > 0f)
		{
			return bounds.Size.Y > 0f;
		}
		return false;
	}

	public static bool TryMeasureNameplateAnchor(IGameData data, int gfx, int? heading, out float contentHeight, out float centerX)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		contentHeight = 0f;
		centerX = 0f;
		if (!TryMeasureVisualBounds(data, gfx, heading, out var bounds))
		{
			return false;
		}
		contentHeight = Math.Max(0f, 0f - bounds.Position.Y);
		centerX = bounds.Position.X + bounds.Size.X * 0.5f;
		return contentHeight > 0f;
	}

	public static bool TryAddSprite(Node2D root, IGameData data, int gfx, int? heading, out float contentHeight)
	{
		ArgumentNullException.ThrowIfNull(root, "root");
		ArgumentNullException.ThrowIfNull(data, "data");
		contentHeight = 0f;
		if (!L1jNpcSpriteCatalog.TryGet(data, gfx, out L1jGfxSprite sprite))
		{
			return false;
		}
		if (!sprite.Actions.TryGetValue("idle", out L1jSpriteAction value))
		{
			return false;
		}
		int value2 = sprite.ResolveHeading(heading);
		double ticksPerSecond = L1jNpcSpriteCatalog.TicksPerSecond(data);
		int fallbackTicks = L1jNpcSpriteCatalog.TicksDefault(data);
		int height;
		SpriteFrames spriteFrames = LoadFrames(gfx, $"{value.Prefix}_h{value2}", value.Ticks, fallbackTicks, ticksPerSecond, out height);
		if (spriteFrames == null)
		{
			return false;
		}
		int height2;
		if (sprite.Shadow.HasValue)
		{
			SpriteFrames spriteFrames2 = LoadFrames(gfx, $"{value.Prefix}_s_h{value2}", value.Ticks, fallbackTicks, ticksPerSecond, out height2);
			if (spriteFrames2 != null)
			{
				AnimatedSprite2D animatedSprite2D = CreateAnimatedSprite(spriteFrames2, height, sprite.Box);
				animatedSprite2D.ZIndex = -1;
				root.AddChild(animatedSprite2D, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		AnimatedSprite2D animatedSprite2D2 = CreateAnimatedSprite(spriteFrames, height, sprite.Box);
		ApplyBlend(animatedSprite2D2, sprite.Attr);
		root.AddChild(animatedSprite2D2, forceReadableName: false, Node.InternalMode.Disabled);
		foreach (L1jSpriteLayer renderedClothe in sprite.RenderedClothes)
		{
			SpriteFrames spriteFrames3 = LoadFrames(gfx, $"{value.Prefix}{renderedClothe.Suffix}_h{value2}", value.Ticks, fallbackTicks, ticksPerSecond, out height2);
			if (spriteFrames3 != null)
			{
				AnimatedSprite2D animatedSprite2D3 = CreateAnimatedSprite(spriteFrames3, height, sprite.Box);
				ApplyBlend(animatedSprite2D3, renderedClothe.Attr);
				root.AddChild(animatedSprite2D3, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		float num;
		if (!TryMeasureNameplateAnchor(data, gfx, heading, out var contentHeight2, out var _))
		{
			L1jSpriteBox box = sprite.Box;
			num = (((object)box != null) ? Math.Max(1, -box.Y0) : height);
		}
		else
		{
			num = contentHeight2;
		}
		contentHeight = num;
		return true;
	}

	public static bool HasSprite(IGameData data, int gfx)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (L1jNpcSpriteCatalog.TryGet(data, gfx, out L1jGfxSprite sprite))
		{
			return sprite.Actions.ContainsKey("idle");
		}
		return false;
	}

	public static bool TryAddCombatSprite(Node2D root, IGameData data, int gfx, int heading, out L1jNpcCombatSpritePlayer player, out float contentHeight)
	{
		player = null;
		contentHeight = 0f;
		if (!L1jNpcSpriteCatalog.TryGet(data, gfx, out L1jGfxSprite sprite) || !sprite.Actions.ContainsKey("idle"))
		{
			return false;
		}
		try
		{
			player = new L1jNpcCombatSpritePlayer(root, data, gfx, sprite, heading);
			contentHeight = player.ContentHeight;
			return true;
		}
		catch (InvalidDataException ex)
		{
			GD.PushWarning("[NPC動態] " + ex.Message + " 將降級為同 gfx 靜態立繪／名牌。");
			player = null;
			return false;
		}
	}

	internal static SpriteFrames? LoadFrames(int gfx, string prefix, IReadOnlyList<double>? ticks, int fallbackTicks, double ticksPerSecond, out int height, bool loop = true)
	{
		height = 0;
		List<Texture2D> list = new List<Texture2D>();
		for (int i = 0; i < 64; i++)
		{
			string path = $"res://assets/npc/{gfx}/{prefix}_{i}.png";
			Texture2D? texture2D = GodotDataFiles.TryLoadTexture(path);
			if (texture2D == null)
			{
				break;
			}
			list.Add(texture2D);
			height = Math.Max(height, texture2D.GetHeight());
		}
		if (list.Count == 0)
		{
			return null;
		}
		SpriteFrames spriteFrames = new SpriteFrames();
		spriteFrames.SetAnimationSpeed("default", ticksPerSecond);
		spriteFrames.SetAnimationLoopMode("default", (SpriteFrames.LoopMode)(loop ? 1 : 0));
		for (int j = 0; j < list.Count; j++)
		{
			double val = ((ticks != null && j < ticks.Count) ? ticks[j] : ((double)fallbackTicks));
			spriteFrames.AddFrame("default", list[j], (float)Math.Max(0.0001, val));
		}
		return spriteFrames;
	}

	internal static AnimatedSprite2D CreateAnimatedSprite(SpriteFrames frames, int height, L1jSpriteBox? box)
	{
		AnimatedSprite2D obj = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Animation = "default"
		};
		ApplyAnchor(obj, height, box);
		obj.Play();
		return obj;
	}

	internal static void ApplyBlend(AnimatedSprite2D sprite, int attr)
	{
		ArgumentNullException.ThrowIfNull(sprite, "sprite");
		sprite.Modulate = Colors.White;
		sprite.Material = (((attr & 8) != 0) ? new CanvasItemMaterial
		{
			BlendMode = CanvasItemMaterial.BlendModeEnum.Add
		} : null);
	}

	internal static void ApplyAnchor(AnimatedSprite2D sprite, int height, L1jSpriteBox? box)
	{
		ArgumentNullException.ThrowIfNull(sprite, "sprite");
		if ((object)box != null)
		{
			sprite.Centered = false;
			sprite.Position = new Vector2((float)box.X0 - 24f, box.Y0);
		}
		else
		{
			sprite.Centered = true;
			sprite.Position = new Vector2(0f, (float)(-height) * 0.5f);
		}
	}
}
