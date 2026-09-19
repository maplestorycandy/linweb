using System;
using System.Collections.Generic;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class StatusLabels
{
	private const int MaxChips = 4;

	private const double HideCountdownAbove = 600.0;

	private static readonly Dictionary<string, string> StatusNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["freeze"] = "冰凍",
		["stun"] = "暈眩",
		["stone"] = "石化",
		["sleep"] = "沉睡",
		["paralyze"] = "麻痺",
		["poison"] = "中毒",
		["burn"] = "燃燒",
		["blind"] = "目盲",
		["poisonsilence"] = "沉默毒",
		["poisonparalyzing"] = "麻痺毒",
		["poisonparalyzed"] = "麻痺",
		["bleed"] = "出血",
		["potionFrost"] = "藥水霜化",
		["foulWater"] = "汙濁之水",
		["weaken"] = "弱化",
		["disease"] = "疾病",
		["vacuum"] = "真空",
		["broken"] = "損壞",
		["slow"] = "緩速",
		["slowatk"] = "攻速降低",
		["mrhalf"] = "魔抗減半",
		["magicseal"] = "魔法封印",
		["silence"] = "沉默",
		["fragile"] = "脆弱",
		["shatter"] = "碎裂",
		["armorbreak"] = "破甲",
		["confuse"] = "混亂",
		["panic"] = "恐慌",
		["guardbreak"] = "護衛毀滅",
		["doom"] = "死神",
		["terror"] = "恐懼無助",
		["preciseshot"] = "精準射擊",
		["wet"] = "潮濕",
		["lock"] = "鎖定",
		["shiver"] = "戰慄",
		["energybreak"] = "能量粉碎"
	};

	private static readonly Dictionary<string, string> BuffNames = new Dictionary<string, string>
	{
		["mob_self_haste"] = "加速",
		["poly"] = "變身"
	};

	public static string Line(Combatant c, bool revealElement = false)
	{
		List<(string, string)> list = new List<(string, string)>();
		string key;
		foreach (KeyValuePair<string, int> status in c.Statuses)
		{
			status.Deconstruct(out key, out var value);
			string text = key;
			int num = value;
			if (num > 0 && !text.StartsWith('_'))
			{
				list.Add((text, StatusName(text) + Countdown((double)num * 0.1)));
			}
		}
		List<(string, string)> list2 = new List<(string, string)>();
		foreach (KeyValuePair<string, double> buff in c.Buffs)
		{
			buff.Deconstruct(out key, out var value2);
			string text2 = key;
			double num2 = value2;
			if (!(num2 <= 0.0) && !text2.StartsWith('_'))
			{
				list2.Add((text2, BuffName(text2) + Countdown(num2)));
			}
		}
		string text3 = (revealElement ? ElementName(c.Element) : null);
		if (list.Count == 0 && list2.Count == 0 && text3 == null)
		{
			return "";
		}
		list.Sort(((string Key, string Text) a, (string Key, string Text) b) => string.CompareOrdinal(a.Key, b.Key));
		list2.Sort(((string Key, string Text) a, (string Key, string Text) b) => string.CompareOrdinal(a.Key, b.Key));
		List<string> list3 = new List<string>(list.Count + list2.Count + ((text3 != null) ? 1 : 0));
		if (text3 != null)
		{
			list3.Add(text3);
		}
		foreach (var item3 in list)
		{
			string item = item3.Item2;
			list3.Add(item);
		}
		foreach (var item4 in list2)
		{
			string item2 = item4.Item2;
			list3.Add(item2);
		}
		if (list3.Count > 4)
		{
			int num3 = 3;
			list3.RemoveRange(num3, list3.Count - num3);
			list3.Add("…");
		}
		return string.Join(" ", list3);
	}

	private static string? ElementName(string? element)
	{
		string text = element?.Trim().ToLowerInvariant();
		switch (text)
		{
		default:
			if (text.Length != 0)
			{
				goto case null;
			}
			goto case "none";
		case null:
			if (text == null)
			{
				return null;
			}
			return element + "屬性";
		case "fire":
			return "火屬性";
		case "water":
			return "水屬性";
		case "wind":
			return "風屬性";
		case "earth":
			return "地屬性";
		case "none":
			return "無屬性";
		}
	}

	private static string StatusName(string key)
	{
		if (!StatusNames.TryGetValue(key, out string value))
		{
			return key;
		}
		return value;
	}

	public static string StatusDisplayName(string key)
	{
		if (!string.IsNullOrWhiteSpace(key))
		{
			return StatusName(key);
		}
		return "";
	}

	private static string BuffName(string key)
	{
		if (BuffNames.TryGetValue(key, out string value))
		{
			return value;
		}
		return SkillInfo.Name(key);
	}

	private static string Countdown(double seconds)
	{
		if (!(seconds >= 600.0))
		{
			return ((int)Math.Ceiling(seconds)).ToString();
		}
		return "";
	}
}
