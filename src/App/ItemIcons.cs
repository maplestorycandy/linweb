using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace IdleLineage.App;

public static class ItemIcons
{
	public const int Size = 28;

	private const int GenericGroundGfx = 19;

	private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["gold"] = "res://assets/icons/items/金幣.png",
		["scroll_return"] = "res://assets/ui/items/scroll_return.png",
		["item_guardian_soul"] = "res://assets/icons/items/守護者的靈魂.png",
		["守護者的靈魂"] = "res://assets/icons/items/守護者的靈魂.png",
		["potion_heal"] = "res://assets/icons/items/紅色藥水.png",
		["potion_strong"] = "res://assets/icons/items/橙色藥水.png",
		["potion_ult"] = "res://assets/icons/items/白色藥水.png",
		["wpn_22"] = "res://assets/icons/weapons/銀箭.png",
		["wpn_5"] = "res://assets/icons/weapons/箭.png",
		["new_item_uncurse"] = "res://assets/icons/items/解除詛咒的卷軸.png",
		["item_pride_sealed_11"] = "res://assets/icons/items/封印的傲慢之塔傳送符(11F).png",
		["l1j_item_40002"] = "res://assets/icons/items/燈籠.png",
		["l1j_item_40018"] = "res://assets/icons/items/強化 自我加速藥水.png",
		["scroll_weapon"] = "res://assets/icons/items/scroll_weapon.png",
		["scroll_weapon_b"] = "res://assets/icons/items/scroll_weapon_b.png",
		["scroll_weapon_c"] = "res://assets/icons/items/scroll_weapon_c.png",
		["scroll_armor"] = "res://assets/icons/items/scroll_armor.png",
		["scroll_armor_b"] = "res://assets/icons/items/scroll_armor_b.png",
		["scroll_armor_c"] = "res://assets/icons/items/scroll_armor_c.png"
	};

	private static readonly Dictionary<string, Texture2D?> InventoryCache = new Dictionary<string, Texture2D?>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Texture2D?> GroundCache = new Dictionary<string, Texture2D?>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Rect2I> ContentCache = new Dictionary<string, Rect2I>(StringComparer.Ordinal);

	public static bool Has(string itemKey)
	{
		return For(itemKey) != null;
	}

	public static Texture2D? TryLoadTexture(string? resPath)
	{
		if (string.IsNullOrEmpty(resPath))
		{
			return null;
		}
		if (ResourceLoader.Exists(resPath))
		{
			try
			{
				return GD.Load<Texture2D>(resPath);
			}
			catch
			{
			}
		}
		Image img = new Image();
		if (FileAccess.FileExists(resPath) && img.Load(resPath) == Error.Ok)
		{
			return ImageTexture.CreateFromImage(img);
		}
		string absPath = ProjectSettings.GlobalizePath(resPath);
		if (System.IO.File.Exists(absPath) && img.Load(absPath) == Error.Ok)
		{
			return ImageTexture.CreateFromImage(img);
		}
		string exeDir = System.AppContext.BaseDirectory;
		if (resPath.StartsWith("res://", StringComparison.Ordinal))
		{
			string diskRel = System.IO.Path.Combine(exeDir, resPath.Substring(6).Replace('/', System.IO.Path.DirectorySeparatorChar));
			if (System.IO.File.Exists(diskRel) && img.Load(diskRel) == Error.Ok)
			{
				return ImageTexture.CreateFromImage(img);
			}
		}
		return null;
	}

	public static Texture2D? For(string itemKey, bool forGround = false)
	{
		if (string.IsNullOrEmpty(itemKey))
		{
			return null;
		}
		Dictionary<string, Texture2D?> dictionary = (forGround ? GroundCache : InventoryCache);
		if (dictionary.TryGetValue(itemKey, out var value))
		{
			return value;
		}
		string? text = ResolvePath(itemKey, forGround);
		if (text != null)
		{
			Texture2D? primary = TryLoadTexture(text);
			if (primary != null)
			{
				return dictionary[itemKey] = primary;
			}
		}

		// Fallback 1: Multi-folder and stem variations
		JsonObject? itemObj = GameDataProvider.Shared.Item(itemKey);
		string iName = itemObj?["n"]?.GetValue<string>() ?? "";
		string rawStem = itemObj?["iconStem"]?.GetValue<string>() ?? iName;
		if (!string.IsNullOrWhiteSpace(rawStem))
		{
			string safeStem = SafeFileStem(rawStem);
			List<string> candidateStems = new List<string>
			{
				safeStem,
				safeStem.Replace(" (", "(").Replace(" )", ")"),
				safeStem.Replace("（", "(").Replace("）", ")"),
				SafeFileStem(itemKey)
			};

			string[] folders = new string[] { "weapons", "armors", "accessories", "items", "skills" };
			foreach (string folder in folders)
			{
				foreach (string cStem in candidateStems)
				{
					string altPath = $"res://assets/icons/{folder}/{cStem}.png";
					Texture2D? altTex = TryLoadTexture(altPath);
					if (altTex != null)
					{
						return dictionary[itemKey] = altTex;
					}
				}
			}
		}

		// Fallback 2: Skill book / crystal inner spell fallback
		if (!string.IsNullOrEmpty(iName))
		{
			string innerSpell = "";
			int o = iName.IndexOf('(');
			int c = iName.IndexOf(')');
			if (o >= 0 && c > o) innerSpell = iName.Substring(o + 1, c - o - 1).Trim();
			if (string.IsNullOrEmpty(innerSpell))
			{
				o = iName.IndexOf('（');
				c = iName.IndexOf('）');
				if (o >= 0 && c > o) innerSpell = iName.Substring(o + 1, c - o - 1).Trim();
			}

			if (!string.IsNullOrEmpty(innerSpell))
			{
				string safeSpell = SafeFileStem(innerSpell);
				string[] spellPaths = new string[]
				{
					$"res://assets/icons/skills/{safeSpell}.png",
					$"res://assets/icons/items/魔法書({safeSpell}).png",
					$"res://assets/icons/items/魔法書 ({safeSpell}).png",
					$"res://assets/icons/items/魔法書（{safeSpell}）.png",
					$"res://assets/icons/items/精靈水晶({safeSpell}).png",
					$"res://assets/icons/items/精靈水晶 ({safeSpell}).png",
					$"res://assets/icons/items/魔法書{safeSpell}.png"
				};
				foreach (string sp in spellPaths)
				{
					Texture2D? spellTex = TryLoadTexture(sp);
					if (spellTex != null)
					{
						return dictionary[itemKey] = spellTex;
					}
				}
			}
		}
		string? scrollB64 = itemKey switch
		{
			"scroll_weapon_c" => ScrollWeaponCursedBase64,
			"scroll_weapon" => ScrollWeaponNormalBase64,
			"scroll_weapon_b" => ScrollWeaponBlessedBase64,
			"scroll_armor_c" => ScrollArmorCursedBase64,
			"scroll_armor_b" => ScrollArmorBlessedBase64,
			"scroll_armor" => ScrollArmorNormalBase64,
			_ => null
		};
		if (scrollB64 != null)
		{
			Image img = new Image();
			if (img.LoadPngFromBuffer(Convert.FromBase64String(scrollB64)) == Error.Ok)
			{
				return dictionary[itemKey] = ImageTexture.CreateFromImage(img);
			}
		}
		if (itemKey == ContentAdditions.GuardianSoulKey || itemKey == "守護者的靈魂")
		{
			Image img = new Image();
			if (img.LoadPngFromBuffer(Convert.FromBase64String(GuardianSoulIconBase64)) == Error.Ok)
			{
				return dictionary[itemKey] = ImageTexture.CreateFromImage(img);
			}
		}
		return dictionary[itemKey] = null;
	}

	public static Rect2I ContentRect(string itemKey)
	{
		if (ContentCache.TryGetValue(itemKey, out var value))
		{
			return value;
		}
		Texture2D texture2D = For(itemKey);
		Rect2I rect2I = default(Rect2I);
		if (texture2D != null)
		{
			Rect2I rect2I2 = new Rect2I(Vector2I.Zero, (Vector2I)texture2D.GetSize().Round());
			Rect2I rect2I3 = texture2D.GetImage()?.GetUsedRect() ?? default(Rect2I);
			rect2I = ((rect2I3.Size.X > 0 && rect2I3.Size.Y > 0) ? rect2I3 : rect2I2);
		}
		ContentCache[itemKey] = rect2I;
		return rect2I;
	}

	public static Control Slot(string itemKey)
	{
		Texture2D texture2D = For(itemKey);
		if (texture2D == null)
		{
			return new Control
			{
				CustomMinimumSize = new Vector2(28f, 28f)
			};
		}
		return new TextureRect
		{
			Texture = texture2D,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Size = new Vector2(28f, 28f),
			CustomMinimumSize = new Vector2(28f, 28f),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
	}

	private static string? ResolvePath(string itemKey, bool forGround)
	{
		if (!forGround && Paths.TryGetValue(itemKey, out string value))
		{
			return value;
		}
		JsonObject jsonObject = GameDataProvider.Shared.Item(itemKey);
		if (jsonObject == null)
		{
			return null;
		}
		if (forGround)
		{
			int value3;
			int value2 = ((jsonObject["l1jGroundGfx"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out value3) && value3 > 0) ? value3 : 19);
			string text = $"res://assets/icons/ground/{value2}.png";
			if (ResourceLoader.Exists(text))
			{
				return text;
			}
			string text2 = $"res://assets/icons/ground/{19}.png";
			if (!ResourceLoader.Exists(text2))
			{
				return null;
			}
			return text2;
		}
		string text3 = jsonObject["img"]?.GetValue<string>();
		if (!string.IsNullOrWhiteSpace(text3))
		{
			return "res://" + text3.Replace('\\', '/').TrimStart('/');
		}
		string text4 = jsonObject["n"]?.GetValue<string>();
		string text5 = jsonObject["iconStem"]?.GetValue<string>();
		string text6 = jsonObject["type"]?.GetValue<string>();
		if (string.IsNullOrWhiteSpace(text4))
		{
			return null;
		}
		string value4 = text6 switch
		{
			"wpn" => "weapons", 
			"arm" => "armors", 
			"acc" => "accessories", 
			_ => ((jsonObject["isArrow"] is JsonValue jv && jv.TryGetValue<bool>(out bool isArr) && isArr) || itemKey.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase)) ? "weapons" : (string.Equals(jsonObject["slot"]?.GetValue<string>(), "shield", StringComparison.OrdinalIgnoreCase) ? "armors" : "items"), 
		};
		return $"res://assets/icons/{value4}/{SafeFileStem(string.IsNullOrWhiteSpace(text5) ? text4 : text5)}.png";
	}

	internal static string SafeFileStem(string name)
	{
		char[] obj = new char[6] { '<', '>', '"', '|', '?', '*' };
		string text = name.Replace(':', '：').Replace('/', '／').Replace('\\', '＼');
		char[] array = obj;
		foreach (char oldChar in array)
		{
			text = text.Replace(oldChar, '_');
		}
		text = text.TrimEnd('.', ' ');
		if (text.Length <= 0)
		{
			return "_";
		}
		return text;
	}

	private const string GuardianSoulIconBase64 = "iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd+UAAAH90lEQVR4nC2Wh27cWBZED8nHTDY7R8myPME7ngE2YOd/9iv2C/YXd2a8DpIlK3VOzGnxnqcbBBoE2e/WvVV1S/vPv//V/vUf/6Rpa56XT6x3a5oGaA2EYSOEBbqOpum01NBCqzVo6LRtw2q15nSMQdMQhoFpCmzHwhQaZZnR1A2aZjMazekEPcTpnHOOM6q65HA4cTgcqJsGXTPx3A6BZSFMwTk+czzuqKoSIQwcz8O1HeqmpKpTdF1g2B5B6KEbUJU5ZZ2RZzlNmxEUXQI6iFMS8/XhjrIu2R+3nOOEpgXPs6iahv1xr+7tdmv2+w1VVRAGAfOLSwLfB60hK1J0TeCHPoPxCF2D3W7F4bhms1mTZCVlA3XVIGqt4XD+hqqsaoRlY9kug8EEx/HYbjY8PT2wXj9TVhm0DY5rE4Yh12/eYNomVVNBozEYDOj3+/R7PZJ4RFWmrNZLzlmMedrjux2EYQp1iNZU6HUFbYtje3hewGLxivn8grzIKauCPD9jGDr9/oDpbMF3P/xAt9elG/VoW51Op6c6MJ3O6XbfUhQ5m/2epGoRjqlmL+pGw/M6qi3pbs/+cMAyj6CZvHv3N66vX2MKiziOub//BDQKeRh2CPyQbqfLZHxBXbcIQ5CmCbPZQqH9+ZeYTzc3LLcbGnKS7CRJkxCnKUkWk2UFVSUp2mBaDp1Oh9lsTlEUPD8/YRiaQjAeTwmDDpZpEUU9+n1DvScPlKz0PBfb8ZhOLhkOJ/h+QN7mVHUuDzyx2W4VS4VpMhqNCYKQq6srut0ujm0zGU/49ddf6XZDkiSm0wlVIYEfoGsaRVHSNhqmbeEELpquQ6tj2zadsEfgd9DyM0J+daUvcBwX3/PoD/r0egPeXL8mikLKssDQWt6+fUsYepyORzzfZTyWhQXc3d1z8/lWHTiZzJlOZ/R7A2zXpshKdAz0RqA1BpbtIGSVnutiWia9bsR8NmUxv+T7779TBLm9/cxq+ay0F4aBulzPVtSXhz98veOP338jiTPG40fevfsFS1gIoZOm6Z/oWwzNRAgToWktGi2+6xJFEcPhgMXFlEG/z2a74cvtR/WHdV3y5s21esZyLHwvwDRNHh/vubv7zGF/ZLvd0O1GLBZzOh2fpqlo6hJT6KDp6DSIPI+pigLHNgl8R7Ut6oRUVc7L8yM3Nx+4uXlPkWfs90tcVxLCpt8fMhwMOR6k+2R/2lgpXRAJQmqzLHNFwMB3ySso8hKhtZLOBrblYDsujuMo6OfziS+3N3z69D/2+z1pGrNcPdK2Nb7vK6aOJ3N0zcCxHKLIYL5YMJ3N8DyPLEvY7jYKZRiEiFyjyA5Sjz625RJ1+vS7Q+kEHI9HRfMkTtTg5b2yKCnKFA35kaatkyYZmiHQMJnOLvnLT+94dXmJ4zq8vDzz+PgVwzRwfY88T5ThC89xMDS5GYRyDClqiSgIIoKgQ9TtY5q2co2mdTEMA8excF1PGTZIdi74+Ze/8+Pbnwg7EcfTkdVmpWZ92XvF1/s7lk8PqmhhW2AYNW1bKo+Moj5toxMGXXr9MfP5FVmWIoSN7Zi4roNlCZq25Xg44PkB19c/cnlxRacTUdOy3m7I8ozLq9cIQ+f58U4hLPIcEYSCMJQ0r5Rnep5PtztQ+3A0OvDd9wVyfYxGU3q9rhJ9nMTc3Hzk+WnJYDjDcQPyoiTNMtAMDGEyHI2YzaaK5YfTnqYtEKaGGI0GjIc9DL3hsFuz240ZDMZYlqkcRrpFVVYsLi5wfUkqmw8f3rPZ7knSlDTNqOtaES7qdvFcafyu2olSSi8vL+zWazRNI/R9RD/qMh71yfKCND6wXj3RNDWGIbBNm0E/QtMNdMMgSRO15ZtvkYBur09/OGC2mCnD8F0bQ9cwHEs997JesXp6Ybve0tZSeo60T4He1upGnp1J4gO2bXE8nhS60XjKcDRVIk+lZstKeebl1RWe77GYL5TQpfZepMHrBmEnpKlbHr7c8/zwTHZOsG0TaaMizzMlg7zIMOxWvShZuF6lrJYv5IXc8CG0LqYwpDUxGg6wTIPxeKIyTJFl3D49stlsiEJp7Au1YT58/MDT41dVpHQoWyI8nk9Yjk1dFjhmgy0soiCkHI3YbzY8P9yrDS4FHnY6CvVkOGQ2Gau5fPz0gS+3t4qxdVVhvnrN/96/5/bmhj9+/y/r9QrHMpWE6hqEzC+yGnll5Y6H+y/oaNRNS1XK+6UaujTg5HRECEF/MCCKurwsl3y5+cz7P35TziSLydOE8+nM3f09y5dnylxuG5+m1bBMB6EbFsJyqRuZtCRT5YAlKXTVKl032K6XaiZny1K7rqkKqiJX6LerZw67FafjCcuyOB+PSm/x+aTIJxOfadvKUPyggwijb/lEzjJPs2/oqorzOVbRQjiCeL9XIauybQxhsGwqZdoydggNenI+pklV1TRNq5D2eoYyeqnLwI/oDye4boCYX1wzmU2pq1IltPhwpG1bXEfqqFYOLyVQ1xVxWajAmyaxQiOlItfabDIlz3PV/uP5pH7bloUfBBi6iW35eK6vEoWIukOKsiE5p6RJTl7U1GWlNFdVldKTRqNQyxAsU51M6VJvjuuqlsldJ9nd0lDmKYftjqKssBxXRY2q2vD08Ijc2mK7OxIGcqgyAvgqcZdlSV7WxIksIoFWRvyappYFyIXdKBPX0MjSRvlq29SkEmUu545qtWS+9OX4nHE6JuR1xv8BwfQ+5yDjuZIAAAAASUVORK5CYII=";

private const string ScrollWeaponCursedBase64 = "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";

	private const string ScrollWeaponNormalBase64 = "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";

	private const string ScrollWeaponBlessedBase64 = "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";

	private const string ScrollArmorCursedBase64 = "iVBORw0KGgoAAAANSUhEUgAAAC4AAAAuCAYAAABXuSs3AAAUB0lEQVR4nH1ZO3Rc13Xd9/PeffPmB4DAkBQJUBQB2ytCh46srErq5Cqs4k6d3SWVU8WdU8lVXMWdOqlTKqkSqzBpoLW8Fhl7GbD4GRADzPd97i9r3wEU2ZYzXI+Y733nnrPPPufsK96++db7IqIQQpiyYz5AegRIxMH6+dU7ArMgxDRAzq7fizHW3/2O9/4M/8+jUOahAMz//d43MmIAESBiLCQwEDEU/EwgGNokYxim+ys1tdDPvZDjCDTiztbOvkIcKYiRDH4gEAsRYbgYv8C/6SaQtY147iPG6bVAc204n6ebCfGtUd990DH8K6EGMsohDeYGRPBGIBpEj2Rg+ouhkmJHCFEoIUdSYsB1o5DNKojPA9QsCEx1prEfrWui9XXfFD/raPWeEhLeW0QRa6FV4YI/qZ19gqgQIGYecRxinAbEmYvhxDr3vPXuucr0+iY+NN77WSbVrtZ6VwoxcDacGpMdJcNDaBAC+kXxD/1e+aibayhE5Eoi1xJaKkYPVVWhaWporZGbErWLj71QqEOApi9VxEiGMOyZ/L1SKr6GT54MBZSCVdjLBPZWjftCCjlkSKMUDaGjIHa0lnuFyB/6GFM0hI5FCGpKjymlUjSDwjTT+UEIYepbMY7eNXVlnwhbFa0Q7yq4YqPXQxsD8ixDURTYKAx8kUNBQOoc9nwG2iNjgBZAggaxpqNgqCB9RPCEioeEgBKR4YYUYkhjGeaISKg0EICUciCU3FnO55+toRETZCTEkBHQQu4KoYpq1X6ZcCvEwGh1VOTZw57Jj7omh9GCXoS3XFIQd7DBY75YYDGfo7Uevf4W84r5Bk2DU3ISczQuxPVliW1eHorJE8K3GA4hzKx3z2zwz10Mp/R+FKgJC36mhBil74bI706J1yzL9ouyeOi9H4sYUtI11j5t6uXTqcBBocVD37bjXMmDnc0t3Bj0kWUZVJ4hL3hbidm0Aj0bAWgaTeNVwMgoDSMkNCKcjohRIkqJAA8vBNroRy76Ux/c2COccRF61wc/dsGf5p3iEDEk9smk2ucmnGtPhVKFyrORbevnzA8j9VHRyR8pZCM4hU6mj/qdAsNud9jv9XBnNMLm5iYWsyXsyQlcAIwpcEnDUyjX0WFgDCED5yEkN8UPBB2GSNgEDx8c4wPeWEhpep3y78te9z1lcrjgUdv2q6IoHllrn0kph1rrkWvacbVcfZEpddDt94+gDaRW2OoPMdq5gX7HINMSG70utjeGGG1tIVMaRa6xmM3xX//53/jDn05w9ursi6IwD8vOoBCR/hY0BQNSEL0enAeUgEr/6GkJFyOi8/DWwgs9jiI0pjBHO7dGj/bevofN7RsJj6umfrS1tYW6bQ6kJDtkaFbVaD6fP860xmC4gXsPfpA2WeQZyo6BbxqsZlPeEpv9HspuidA62BAwr2pczGaYLhbj2uFJmZv3GmdTojqp1h6/fhBTJstRCA0EjxAcXAwIIiATHiG6aZSyzgpzsL29jQcPHuDOvT3ITNNg4hlN0zAX0lr1qsL8cgqtFDY2NzHcHKD1LtGeyTM00cFGt46YN8i9Q10t0+/G529wPr3Eoq4+8RLjzOSoFu3YKz+IQTSaYb1OOk8jQ4CXAc5ZtG0Ny8V8g8a30F2z28J/fXj4d6ODH/0QBz86SAZvbW1gVVeYzWZwQa4ZSKbKiNa3RCAWlYF9/RKm7KAVEmerJZbLObGITp5hUS3h6KiqQds0GE/e4MXZa4gsLzpG/vhivvjUZMUR4et8ONUBmF6xTxGFgBMRtbewTY3a1ggxwjI9JerWua9lJ0utAAtE27ZorEXVrDd4Mb1cezwZLrGcL3B2dpZgZi4ucP/gANVljbausVjMkmMKkyEO+pBKYHo+p1VwrcVstUJlHZ05ViLbCRIzB5yysnuBMam7iUDNF14CbfCw1qNu14YzFYKKsCI8F0qZjY3B4263gyxXCUo+WCyWMzjvU7W1wSJYB+ccJpNzvBy/RLVcIdMGW6MR6rrGsqrQ2hpCkNoYDqBuGsymU9Ag+IiL+Yww+byN/lgJJHolMaQuBnGmWfejRCMFBiR8JUj/AQ0CWoT1hhDO2hiOh/3Nn969exc3drYxGAyglIJwApPJBNa5xLmkpzZEzOdznJ+f482bN2iq+iTP871XL75JkWEO5J0cWaZhg0M1maQIstgxH1QQKYp123zlAk6jkLUN/pmWepfhZA+l+R87vyhRV64dC61HWgqEXMELNfaIZxbhWevDcdHv4K07tzAc9mFKk4oBvf769Us0tsVwczMZsFqtcPbmdXp/MnnzMSuolPLDV69eDZlHxhj0fRdNJlE3VYoCvMPW1ja00AhSwMZAmJwREfR0691x1LoRUZoI0ayhIlCzjDbePdVSfBCEhBWx9kqcOeAkCDkLUs7IHv3ButslvoWUiRHOLyaoSG11DWttStLxeIzJxeS3bdt+nSm937b18fnk7JFzjhW2uJzmCCKmdYzJ0O/3UzkXSqaC7Ul7AScQCiwuqaGDIMYN+6tEh/S4EGhsDM/a6Pdd8MPGtk/baI+hFaKWjYg6MQ87teVymRK29B4i1wnPiY28x3y5SNBZLpfHLPPZMNsPzs/qtnniL6djejHP83db1xw0rn3qnDvd2t78qOx1WQsS/CRkcgArL7HNRk0oaXwIY5I+ZwXN8ky40PNBqVkbcRy9bWrXPiGulJKjTOoDrdQuqYfllu0m2UdnGbplkTbDGw6Hw8QqzJMizw/zXi9VwtlsVq9Wq8/b2nIzB1mn+LBjilGM8V32LpnKURZdnL+ZIFcZtMzgbIAPGEsphkLJgvwaRJiyRWFz920B4mRR+/BV5e0XxKQ2xa6Occ97O/YOY5OZo1u7d/Di7BU2NjaQFzkcPN6cjdEry5SIJ7///drz1qIsilSgTJ7jd7/7XfH61cvT0Pppr1M87uSdUXARvvFnMshBPa/xzR9fpEbqzmgIrXNIqSG0Zm9fNz48lVoN2bxBydTsfWs4KTElgpBj4igIORUiFALapMkFchAhodmClp11tazrhOfJ+TkuJhOUZZnokyWezztFkTZC7BIeKpMjevjs7OxjYp8RNcYcmbxzZBu3x+Qsyx6UUKmpYnSa0DxtnTt2zp8WuX4oYxxGIWr9vaOWEJxBCwGxnvukJCsMR+zatjcTJFISTueYXs4wny1QVw2qVZ2MJGxubG1jtHMTFxcX8C4g+DgznfxotVr9x2Kx+oQ/z3OxQ8M5q65Wq70bN0Ta6JUNXCfxd+rvQ6rqU0YlNVnfZ3Ty/1+/b+hFLkxYMAFZFelx4pobadu2VkoVbLbu3LmDd955B998803yPnv1qqq+bNv2mOvRaHaTeZ4fcnZlVzmdTg+63S60XEfzqpe/dt4gRtHwQrxilb/1YBJcX8xwFpNVs0pF5dWrV0/n8/lvyRzdbvfDLMv2GH4aobV+NJ1OsVgsUiKT8qy1z51zJ1yLDMH12cOTvXRmduldVtjZ5RRSqvS76HwjQiyERBqcOctGqDoVoL/06pXBdbpCGnrHxD1pi8bMljO8fv36eDwe/9Ta8Hw47P2MVbHX69GzR4wIw0yIvHjxIhnQ6XSws7Pzb8vl8lOus6qbz+n9qmqf8j7EeKfTeY+/Y+RiTNF7Ro/ziiLurO0STQiYkd2+1+PXHo4h1DScxC+dGFRVk7K93x8eVlXz0XK5/EwIZbyPtXOh6PcHCf+kR8KD3+12+7h37z62t0d4+frFTwir5bL6+WKx+KSu6yfXzqrr+qusmz9idIL1cE17Sv6nl4WIhZJyxFGRMCaJ/E2o0BM0npeHH4egdi8vL1F0CzIEy/bR5eXlvy6XzZfcAPFKI/b39z++ceNGSlJCil0iWwAafOvWrXVH2VhG4jEvFrPlovqUa7BtlVIWvnX8PovTCW0hvgmltg1fXwtK4u07t953VX2aSrOQ+0VRPHR1dcqQ5pk6TMkUIgYb/Y+8DuO3dt/6FXsNJuXl5eWvzs8v/omtRlliUJZlUsIePHjwyf7+foIJ4TKdTqfLavXZcHPwU35+eTH7DWHD79MBRd55xBkz1ySPgOVsgbPX40+nFxe/ZsSV0IldYlDrQinl9M88nvAUY3NFf6nckg0ypQ+Gw+HPYo5mtVqNhRAj9hZN07wn5cVAKSRZjgmYynyWpaiksIeQ3idrzOfzT6XWQ3qSHM2k7nQ6eyYrQDaxzbpzZJf4/WpYgBR6LXn8heGzK8MZmh2WW74uy/L97e3tQhhd/Onln75CKUeD/gYuJtMdjlFKRqyWmJWlfyagjG09Vssa00vy/Pxktaw/b137dWXrL/PCHHofxtygKTt7ZaeEEjpBikYTYldEkfTMpEKItT4jnDdJkJLZQP7Vzta8WdDrVzif0jvsv5MUlueHfE64cKOMxK1bo383hiKTYv/REM+pok4mhMmviVeu2zT2KaFHGF5FE5EtrHcpD/jgMLz2OgbXglLCecRQRG/YYHGw+DOP88bf3UAMMdFhamq0hq2X2NraGnJQZlLRiM3NzV8Mh8NhaqLa9pibZZdIWru6nicRSMBcsdWU/TQbp6skRxvWkBIc9xGT4etCmNSwQRJkRRz4GGotwq6UcfhnhhPP14ySQiVl6sSuVVlSXW/YS7req1ev6KXPGY3VanVUVdUX11Cr6/q4aZpDa22i07QhiybrZPvrqIiGvQtxTf5v8zYZayubht9rx9HjnInXKIDRUu1mSh6oTH+/x688vEs8XQs8XPjmzVFqsDiWORcwGGx8lOfmXe8jNjdv/IJGktba1h0vl9UhoUGfFUX5kA0WjCQWhsGGU631Hr1Nvs9Vvi5a1cW10d+FbvI8H3muD8lCWW7Whkchkr4dpeBYz2ljqmXcVVqOeEM2awxj0ckRhUNVz2FKjfv7e4/IIIgSxuz//OXLl6yqRyzP1lJncbNux3w4GAw+Knu94WQ2+Zw9de3bJ4RAwrKkzLdmkkgtXlJyWEt7SmBHBmI60NDdbq4+7ChRKAqkAqogEyQhTkg0rn5KMZ2KVev9cdE1D7dvbhyUgxwQDbq9HnbL27h1dyeJPuTq+apK9Ff2cwjtf3JxfnFycXn2sW/9+ODB/i/v3buXkvXmja0POJotqtVj6jCcnIqyA10YvH75Enfv7+JiPMH0/A1YS4S1hQ5xr9T6faPkXodU0lQpL74DFQ5MYtjpdH6M0MK2zbOiyB/d3Nn8+c72ZlJsTS5hMgEZ1kpVp0vZrIBZmWT4RAK9XonJm/MmIjQ3bmz+8tbtEUbbW9gc9pOIOdgYwseIs8vJWi5me1Csq/FkfIZqtUhSM4WiLMZ9ejqPcc/EgK7Mkn6cyciJUoyS0BKD8bYd9wrzmI26F2J8czj8xQ/vv4PRziZ8dAh1i5baC6WITEOxBc00oimg8wy3RzfXhwLOHawWy398e/dt3Ln9VjphUEqmMY4Vkt8tex1Urk3e5uRPY8/PXqO6nKNeLmrhncmlOMyl5IVc0Gl5Uo61ztYeJ940xK4R4ojnMt3cfHjj5vZo//49HLx9L3m1aquk+0EqONci+BZ+WVO9xWpZgSPVYLCB2zdvgXlCofT+vXdSoo1fvkqFpV/2Eu2x16aHuxsDNkz44+kp/ufZM4xfvJr6VT12C8LEG6P0YUdl6MgMGWkly+CjhlY5NE8HlIiJTfqd4qehbaa9PB89uHsX+3fvolQKWFbIRUC37FGKAxqXigb1u8iJxdmUoMvZlDyPt27eQpEb3L59O/F9vVwlQ7WgjtdgtVwmmEQFePYm0ymmkwlmZ29+g9ZBujAsonxodIZOlqFUOTLq9pE35wCpKDPHoYhxzOM5ngWF1k7RNoirGrPXY5y3KzT1MunaqteFNiZpKMgURJRQRY4iy2E6lNcpqEf0ym7qwZl89DQ3Q9qbXl5C+bWUMZleon71MhUp9vlGssA1zxOLeIyUVCMeouVCQUmaKhMFMz+kS4dXlMTFiKzi22ZslDxC2+KbP/wBFy9OoEmOoU2FYdpa5N0uTyYSxje2d5CXHUBn67BDJrxubW8nY3kjQqVfdtcTTYyJ/nj51uLifJKmJJb7tqphdHYkXRhkIR5oIUcUT9PpgieyI4KjCEo1yl+dSMR0/lOwAA273UIFm6b2ubfYGvZRFhqNc+uw25B0vZW1xy9fn+1zyKBE1hkMHjXWHtfWPXnwg4OPKE3Qs51OCb+9nYTQ7e3tJI4qpVNEOHiwr2lWNSbnb35ZZuYDCMejnN1kNJ0ZA1pHXRHgwUNgUoQI8aO7d/9FNK0JdU1sPy6lOOQRnA6kI4rwQMfkyIzBZV2j9gE8GaCq24owthHpEKsN8ZjwCZEn0JjyuIVVrtPpvleW5SEZ4f69e2Bh4iQ1X80xny2xXM2fNqvmqWubEw21RwrMoz40QozI1qz15BDJztEHqNzAa732uA9x7H0cS6kH6QyIxykUXpgM3KDQ6eBQZQZK8YxRI0SPgDDy3o3TLCjcLAZRuxBOefoc4GdtW329rOxnZlYdKS1G5+cXD9lOsDWw3nF45sBywhFNhGhKkw09RJpxg1Aj3pPHJjHyeIeVVcBdHUDoGEVtQ3zuvD+VWT5MTY5et5aC3C0CrBLJ8Kg1JKUBxZB55iGCs/sW9pn0duAgplHFdGrM7KeE5mw4sbZ6TgdN/PyfFdZaN+mT0KSzsjzbTz23XFfwCNm4KKZSqiGbAQqglPUQJKSS8EqR0eIsHW9HMV02zWdWqsPWq3dzGQseGyK6dJKbehmVUjXphusTUFE3AU9rF57wIt7Za3CAFlIY6n08FLuWOJS+EniuRKb0nB2oEA1b3craL3UUu60IxzqKPQ2/y76HNYZ5qJQaspVxMdT/C2IKnASznb/KAAAAAElFTkSuQmCC";

	private const string ScrollArmorBlessedBase64 = "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";

	private const string ScrollArmorNormalBase64 = "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";
}
