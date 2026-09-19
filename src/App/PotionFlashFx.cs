using System;
using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

public static class PotionFlashFx
{
	private const double FramesPerSecond = 12.0;

	private const string AnimationName = "flash";

	private static readonly IReadOnlyDictionary<string, string> FlashByItem = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["potion_heal"] = "heal",
		["potion_strong"] = "strong",
		["potion_ult"] = "ult",
		["potion_haste"] = "haste",
		["potion_brave"] = "brave",
		["new_item_140"] = "brave",
		["companion_potion_heal"] = "heal",
		["companion_potion_strong"] = "strong",
		["companion_potion_ult"] = "ult",
		["l1j_item_40010"] = "heal",
		["l1j_item_40011"] = "strong",
		["l1j_item_40012"] = "ult",
		["l1j_item_40013"] = "haste",
		["l1j_item_40014"] = "brave",
		["l1j_item_40016"] = "brave",
		["l1j_item_40018"] = "haste",
		["l1j_item_40019"] = "heal",
		["l1j_item_40020"] = "strong",
		["l1j_item_40021"] = "ult",
		["l1j_item_40022"] = "heal",
		["l1j_item_40023"] = "strong",
		["l1j_item_40024"] = "ult"
	};

	private static readonly Dictionary<string, SpriteFrames?> Cache = new Dictionary<string, SpriteFrames>(StringComparer.Ordinal);

	public static void TryPlay(Node2D parent, string itemKey, Func<Vector2> follow)
	{
		ArgumentNullException.ThrowIfNull(parent, "parent");
		ArgumentNullException.ThrowIfNull(follow, "follow");
		if (string.IsNullOrEmpty(itemKey)) return;
		if (!FlashByItem.TryGetValue(itemKey, out string? value))
		{
			if (itemKey.Contains("heal") || itemKey.Contains("red"))
				value = "heal";
			else if (itemKey.Contains("strong") || itemKey.Contains("orange"))
				value = "strong";
			else if (itemKey.Contains("ult") || itemKey.Contains("white"))
				value = "ult";
			else if (itemKey.Contains("haste"))
				value = "haste";
			else if (itemKey.Contains("brave"))
				value = "brave";
			else if (itemKey.Contains("third") || itemKey.Contains("cake") || itemKey.Contains("49138"))
				value = "brave";
		}
		if (!string.IsNullOrEmpty(value))
		{
			SpriteFrames spriteFrames = BuildFrames(value);
			if (spriteFrames != null)
			{
				PotionFlashNode potionFlashNode = new PotionFlashNode();
				parent.AddChild(potionFlashNode, forceReadableName: false, Node.InternalMode.Disabled);
				potionFlashNode.Start(spriteFrames, follow);
			}
		}
	}

	private static SpriteFrames? BuildFrames(string prefix)
	{
		if (Cache.TryGetValue(prefix, out SpriteFrames value))
		{
			return value;
		}
		SpriteFrames spriteFrames = new SpriteFrames();
		spriteFrames.AddAnimation("flash");
		spriteFrames.SetAnimationLoopMode("flash", SpriteFrames.LoopMode.None);
		spriteFrames.SetAnimationSpeed("flash", 12.0);
		int num = 0;
		for (int i = 0; i < 6; i++)
		{
			string path = $"res://assets/effects/potionfx/{prefix}_{i}.png";
			if (!ResourceLoader.Exists(path))
			{
				break;
			}
			spriteFrames.AddFrame("flash", GD.Load<Texture2D>(path));
			num++;
		}
		SpriteFrames spriteFrames2 = ((num > 0) ? spriteFrames : null);
		Cache[prefix] = spriteFrames2;
		return spriteFrames2;
	}
}
