using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class IdentifyScrollText
{
	private static readonly (string Key, string Label)[] NumericFields = new(string, string)[15]
	{
		("dmgS", "小型傷害"),
		("dmgL", "大型傷害"),
		("ac", "AC"),
		("safe", "安定值"),
		("hit", "命中"),
		("dmg", "額外傷害"),
		("str", "力量"),
		("dex", "敏捷"),
		("con", "體質"),
		("int", "智力"),
		("wis", "精神"),
		("cha", "魅力"),
		("hp", "HP"),
		("mp", "MP"),
		("mr", "抗魔")
	};

	public static string Describe(IGameData data, ItemStack target, bool newlyIdentified)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(target, "target");
		JsonObject jsonObject = data.Item(target.ItemKey);
		string text = L1jItemIdentityRules.DisplayName(data, target);
		List<string> list = new List<string>
		{
			newlyIdentified ? ("✓ 已鑑定：" + text) : ("✓ " + text + "（原本已鑑定）"),
			"祝福：" + BlessingName(target.Blessing) + "\u3000強化：" + Enhancement(target.Enhancement)
		};
		if (target.ItemLevel > 0)
		{
			EquipmentAffixQuality equipmentAffixQuality = EquipmentAffixRules.Quality(target);
			int value = (int)Math.Ceiling(Math.Max((jsonObject == null) ? 0.0 : CombatSkill.ReadDouble(jsonObject, "minLvl"), EquipmentAffixRules.RequiredLevel(target)));
			list.Add($"品質：{equipmentAffixQuality.Name}\u3000物品等級：{target.ItemLevel}\u3000需求等級：{value}");
			list.AddRange(target.Affixes.Select((EquipmentAffixRoll affix) => "• " + EquipmentAffixRules.Format(affix)));
		}
		List<string> list2 = new List<string>();
		(string, string)[] numericFields = NumericFields;
		for (int num = 0; num < numericFields.Length; num++)
		{
			var (propertyName, value2) = numericFields[num];
			if (jsonObject?[propertyName] is JsonValue jsonValue && double.TryParse(jsonValue.ToJsonString().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && !(Math.Abs(result) < 1E-06))
			{
				list2.Add($"{value2} {result:+0.##;-0.##;0}");
			}
		}
		if (list2.Count > 0)
		{
			list.Add(string.Join("\u3000", list2));
		}
		if (jsonObject?["d"] is JsonValue jsonValue2 && jsonValue2.TryGetValue<string>(out string value3) && !string.IsNullOrWhiteSpace(value3))
		{
			list.Add(value3);
		}
		return string.Join("\n", list);
	}

	public static string Failure(L1jIdentifyFailure failure)
	{
		return failure switch
		{
			L1jIdentifyFailure.ScrollMissing => "找不到這張鑑定卷軸；未消耗。", 
			L1jIdentifyFailure.ScrollLocked => "鎖定中的鑑定卷軸無法使用；未消耗。", 
			L1jIdentifyFailure.LevelTooLow => "角色等級尚未達到這張卷軸的使用需求；未消耗。", 
			L1jIdentifyFailure.LevelTooHigh => "角色等級已超過這張象牙塔卷軸的使用上限；未消耗。", 
			L1jIdentifyFailure.TargetMissing => "目標物品已不在身上；未消耗卷軸。", 
			L1jIdentifyFailure.TargetIsSourceScroll => "不能用卷軸鑑定它自己；未消耗。", 
			_ => "目前無法鑑定；未消耗卷軸。", 
		};
	}

	private static string BlessingName(ItemBlessing blessing)
	{
		return blessing switch
		{
			ItemBlessing.Blessed => "祝福", 
			ItemBlessing.Cursed => "詛咒", 
			_ => "一般", 
		};
	}

	private static string Enhancement(int value)
	{
		if (value <= 0)
		{
			if (value >= 0)
			{
				return "+0";
			}
			return $"−{-value}";
		}
		return $"+{value}";
	}
}
