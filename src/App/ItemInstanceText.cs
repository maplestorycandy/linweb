using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class ItemInstanceText
{
	public static string DisplayName(IGameData data, ItemStack item, PetRoster roster)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(item, "item");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		string text = L1jItemIdentityRules.DisplayName(data, item);
		if (!item.IsIdentified)
		{
			return text;
		}
		if (item.PetUid.Length == 0)
		{
			return text;
		}
		PetInstance petInstance = roster.Find(item.PetUid);
		return (petInstance == null) ? (text + "［失效項圈］") : $"{text}[Lv.{petInstance.Level} {petInstance.DisplayName}] HP {petInstance.Hp:0}/{petInstance.MaxHp:0}";
	}

	public static string StackCorner(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (!item.IsIdentified)
		{
			return CompactCount(item.Quantity);
		}
		if (item.Enhancement > 0)
		{
			return $"+{item.Enhancement}";
		}
		if (item.Enhancement < 0)
		{
			return $"−{-item.Enhancement}";
		}
		return CompactCount(item.Quantity);
	}

	public static string AffixTooltip(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (!item.IsIdentified || item.ItemLevel <= 0)
		{
			return "";
		}
		EquipmentAffixQuality equipmentAffixQuality = EquipmentAffixRules.Quality(item);
		List<string> list = new List<string> { $"\n品質：{equipmentAffixQuality.Name}\u3000物品等級 {item.ItemLevel}" };
		list.AddRange(item.Affixes.Select((EquipmentAffixRoll affix) => "• " + EquipmentAffixRules.Format(affix)));
		return string.Join("\n", list);
	}

	public static string BlessingTooltip(IGameData data, ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(item, "item");
		if (!item.IsIdentified || item.Blessing == ItemBlessing.Normal)
		{
			return "";
		}
		string text = data.Item(item.ItemKey)?["type"]?.GetValue<string>() ?? "";
		bool flag;
		switch (text)
		{
		case "wpn":
		case "arm":
		case "acc":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "";
		}
		if (item.Blessing == ItemBlessing.Cursed)
		{
			return "\n詛咒效果：穿戴後無法卸下或替換，必須先解除詛咒。";
		}
		if (text == "wpn")
		{
			string text2 = (WeaponDurabilityRules.CanBeDamaged(data, item) ? "；攻擊硬質怪時，同一攻擊祝福來源使武器損壞率由 10% 降為 3%" : "");
			return "\n祝福效果：作為攻擊來源時（遠程以箭矢為準），對不死／惡魔系 1～3 類追加 1～5 傷害" + text2 + "。";
		}
		return "\n祝福狀態：L1J-TW main 未賦予防具／飾品額外 AC、MR 或減傷。";
	}

	public static string DetailTooltip(IGameData data, ItemStack item)
	{
		string extra = "";
		if (item.ItemKey == ContentAdditions.GuardianSoulKey || item.ItemKey == "item_guardian_soul")
		{
			extra = "\n" + ArpgEngineScreen.GetGuardianSoulFullTooltipText();
		}
		return BlessingTooltip(data, item) + AffixTooltip(item) + extra;
	}

	public static string CompactCount(long value)
	{
		if (value >= 10000)
		{
			if (value < 1000000000)
			{
				if (value >= 1000000)
				{
					return $"{(double)value / 1000000.0:0.#}M";
				}
				return $"{(double)value / 1000.0:0.#}K";
			}
			return $"{(double)value / 1000000000.0:0.#}B";
		}
		if (value > 1)
		{
			return value.ToString();
		}
		return "";
	}
}
