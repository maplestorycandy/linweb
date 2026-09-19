using System;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class ItemQualityColors
{
	public const string White = "#f2f2f2";

	public const string Green = "#7fd08a";

	public const string Blue = "#45b4ff";

	public const string Purple = "#c07fe8";

	public const string Orange = "#ff9d45";

	public const string Gold = "#f2c14e";

	public const string CursedRed = "#e2938f";

	public const float AffixFrameAlpha = 0.72f;

	public static string Hex(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (!item.IsIdentified)
		{
			return "#f2f2f2";
		}
		return Hex(item.Enhancement, item.Blessing);
	}

	public static string Hex(int enhancement, ItemBlessing blessing)
	{
		return blessing switch
		{
			ItemBlessing.Blessed => "#f2c14e", 
			ItemBlessing.Cursed => "#e2938f", 
			_ => (enhancement <= 0) ? "#f2f2f2" : (enhancement switch
			{
				1 => "#7fd08a", 
				2 => "#45b4ff", 
				3 => "#c07fe8", 
				_ => "#ff9d45", 
			}), 
		};
	}

	public static Color Of(ItemStack item)
	{
		return Color.FromHtml(Hex(item).AsSpan());
	}

	public static string FrameHex(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (!item.IsIdentified)
		{
			return "#f2f2f2";
		}
		if (item.ItemLevel > 0)
		{
			return EquipmentAffixRules.Quality(item).Color;
		}
		return Hex(item.Enhancement, item.Blessing);
	}

	public static Color FrameOf(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		Color result = Color.FromHtml(FrameHex(item).AsSpan());
		if (item.IsIdentified && item.ItemLevel > 0)
		{
			result.A = 0.72f;
		}
		return result;
	}

	public static bool Highlighted(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (item.IsIdentified)
		{
			if (item.Enhancement <= 0)
			{
				return item.Blessing != ItemBlessing.Normal;
			}
			return true;
		}
		return false;
	}

	public static bool Framed(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (item.IsIdentified)
		{
			if (item.ItemLevel <= 0)
			{
				return item.Blessing != ItemBlessing.Normal;
			}
			return true;
		}
		return false;
	}
}
