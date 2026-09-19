using System;
using System.Collections.Generic;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class StatusIcons
{
	public readonly record struct Row(string Icon, string Label, double Seconds);

	public const int Size = 28;

	public const float IdleAlpha = 0.55f;

	private static readonly IReadOnlyDictionary<string, string> PotionIcons = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["haste"] = "加速術",
		["brave"] = "勇敢藥水",
		["third_haste"] = "三段加速",
		["third_speed"] = "三段加速",
		["blue"] = "藍色藥水",
		["cautious"] = "慎重藥水",
		["elfcookie"] = "精靈餅乾",
		["poly"] = "變形術"
	};

	private static readonly IReadOnlyDictionary<string, string> DebuffIcons = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["stun"] = "衝擊之暈",
		["silence"] = "禁言",
		["magicseal"] = "魔法封印",
		["poison"] = "毒咒",
		["poisonsilence"] = "禁言",
		["poisonparalyzing"] = "毒咒",
		["weaken"] = "弱化術",
		["disease"] = "疾病術",
		["blind"] = "闇盲咒術"
	};

	private static readonly IReadOnlyDictionary<string, string> SkillIcons = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["sk_sunlight"] = "日光術",
		["sk_shield"] = "保護罩",
		["sk_holy_wpn"] = "神聖武器",
		["sk_ench_wpn"] = "擬似魔法武器",
		["sk_reveal"] = "無所遁形術",
		["sk_load_up"] = "負重強化",
		["sk_shield2"] = "鎧甲護持",
		["sk_dex_up"] = "通暢氣脈術",
		["sk_magic_shield"] = "魔法屏障",
		["sk_meditation"] = "冥想術",
		["sk_haste_spell"] = "加速術",
		["sk_str_up"] = "體魄強健術",
		["sk_bless_wpn"] = "祝福魔法武器",
		["sk_greater_haste"] = "加速術",
		["sk_berserk"] = "狂暴術",
		["sk_holy_dash"] = "神聖疾走",
		["sk_blizzard_storm"] = "冰雪颶風",
		["sk_fire_prison"] = "火牢",
		["sk_invisible"] = "隱身術",
		["sk_heal_energy_storm"] = "治癒能量風暴",
		["sk_holy_barrier"] = "聖結界",
		["sk_soul_up"] = "靈魂昇華",
		["sk_solid_shield"] = "堅固防護",
		["sk_reduction_armor"] = "增幅防禦",
		["sk_spike_armor"] = "尖刺盔甲",
		["sk_counter_barrier"] = "反擊屏障",
		["sk_elf_mr"] = "魔法防禦",
		["sk_elf_purify"] = "淨化精神",
		["sk_elf_eleres"] = "屬性防禦",
		["sk_elf_singleres"] = "單屬性防禦",
		["sk_elf_firewpn"] = "火焰武器",
		["sk_elf_windshot"] = "風之神射",
		["sk_elf_winddash"] = "風之疾走",
		["sk_elf_earthguard"] = "大地防護",
		["sk_elf_watervital"] = "水之元氣",
		["sk_elf_dancefire"] = "舞躍之火",
		["sk_elf_stormeye"] = "暴風之眼",
		["sk_elf_earthshield"] = "大地屏障",
		["sk_elf_earthbless"] = "大地的祝福",
		["sk_elf_blazewpn"] = "烈炎武器",
		["sk_elf_flamesoul"] = "烈焰之魂",
		["sk_elf_stormshot"] = "暴風神射",
		["sk_elf_steelguard"] = "鋼鐵防護",
		["sk_elf_attrfire"] = "屬性之火",
		["sk_elf_physboost"] = "體能激發",
		["sk_elf_energyboost"] = "能量激發",
		["sk_elf_mirror"] = "鏡反射",
		["sk_dark_str"] = "力量提升",
		["sk_dark_mrup"] = "影之防護",
		["sk_dark_stealth"] = "暗隱術",
		["sk_dark_poison"] = "附加劇毒",
		["sk_dark_dex"] = "敏捷提升",
		["sk_dark_poisonres"] = "毒性抵抗",
		["sk_dark_burn"] = "燃燒鬥志",
		["sk_dark_walkhaste"] = "行走加速",
		["sk_dark_fang"] = "暗影之牙",
		["sk_dark_dodge"] = "暗影閃避",
		["sk_dark_erup"] = "迴避提升",
		["sk_dark_double"] = "雙重破壞",
		["sk_illu_ogre"] = "幻覺：歐吉",
		["sk_illu_cube_burn"] = "立方：燃燒",
		["sk_illu_mirror"] = "鏡像",
		["sk_illu_focus"] = "專注",
		["sk_illu_lich"] = "幻覺：巫妖",
		["sk_illu_cube_quake"] = "立方：地裂",
		["sk_illu_golem"] = "幻覺：鑽石高崙",
		["sk_illu_cube_shock"] = "立方：衝擊",
		["sk_illu_endure"] = "耐力",
		["sk_illu_avatar"] = "幻覺：化身",
		["sk_illu_insight"] = "洞察",
		["sk_illu_cube_harmony"] = "立方：和諧",
		["sk_illu_pain"] = "疼痛的歡愉",
		["sk_dragon_armor"] = "龍之護鎧",
		["sk_dragon_flameslash"] = "燃燒擊砍",
		["sk_dragon_awaken_antares"] = "覺醒：安塔瑞斯",
		["sk_dragon_bloodlust"] = "血之渴望",
		["sk_dragon_awaken_falion"] = "覺醒：法利昂",
		["sk_dragon_deadlybody"] = "致命身軀",
		["sk_dragon_awaken_baraka"] = "覺醒：巴拉卡斯",
		["sk_royal_precise"] = "精準目標",
		["sk_royal_burnweapon"] = "灼熱武器",
		["sk_royal_bravewill"] = "勇猛意志",
		["sk_royal_shield"] = "閃亮之盾",
		["sk_warrior_endurance"] = "體能強化",
		["sk_warrior_outlaw"] = "亡命之徒"
	};

	private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	public static List<Row> Rows(Combatant player, (MagicDollDefinition Definition, double RemainingSeconds, Combatant Follower)? activeDoll = null)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		List<Row> rows = new List<Row>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		double guardianSecs = 0;
		if (ArpgEngineScreen.ActiveInstance != null && ArpgEngineScreen.ActiveInstance.IsGuardianActive && ArpgEngineScreen.ActiveInstance.GuardianTimeRemaining > 0)
		{
			guardianSecs = ArpgEngineScreen.ActiveInstance.GuardianTimeRemaining;
		}
		else if (player.Statuses.TryGetValue("guardian_soul", out int ticks) && ticks > 0)
		{
			guardianSecs = ticks / 25.0;
		}
		if (guardianSecs > 0)
		{
			string label = "【守護者的靈魂】\n【能力加成】\n• 最大生命力 (HP) +500\n• 最大魔力 (MP) +200\n• 受到物理與魔法傷害減少 5%";
			Add("守護者的靈魂", guardianSecs, label);
		}
		if (activeDoll.HasValue)
		{
			var doll = activeDoll.Value;
			string desc = MagicDollRules.AbilityDescription(doll.Definition.Type, doll.Definition.Name);
			string label = $"【{doll.Definition.Name}】\n效果：{desc}";
			Add(doll.Definition.Name, doll.RemainingSeconds, label);
		}
		if (player.Buffs.GetValueOrDefault("sk_greater_haste") > 0.0)
		{
			Add("加速術", player.Buffs["sk_greater_haste"], "強力加速術");
		}
		string value;
		string key;
		foreach (KeyValuePair<string, string> potionIcon in PotionIcons)
		{
			potionIcon.Deconstruct(out value, out key);
			string key2 = value;
			string text = key;
			double valueOrDefault = player.Buffs.GetValueOrDefault(key2);
			if (valueOrDefault > 0.0)
			{
				Add(text, valueOrDefault, text);
			}
		}
		foreach (KeyValuePair<string, string> skillIcon in SkillIcons)
		{
			skillIcon.Deconstruct(out key, out value);
			string text2 = key;
			string icon = value;
			double valueOrDefault2 = player.Buffs.GetValueOrDefault(text2);
			if (valueOrDefault2 > 0.0)
			{
				Add(icon, valueOrDefault2, SkillInfo.Name(text2));
			}
		}
		foreach (KeyValuePair<string, string> debuffIcon in DebuffIcons)
		{
			debuffIcon.Deconstruct(out value, out key);
			string key3 = value;
			string text3 = key;
			int valueOrDefault3 = player.Statuses.GetValueOrDefault(key3);
			if (valueOrDefault3 > 0)
			{
				Add(text3, (double)valueOrDefault3 * 0.1, "異常：" + text3);
			}
		}
		return rows;
		void Add(string text4, double seconds, string label)
		{
			if (text4.Length != 0 && seen.Add(text4))
			{
				rows.Add(new Row(text4, label, Math.Max(0.0, seconds)));
			}
		}
	}

	public static Texture2D? Texture(string iconName)
	{
		if (string.IsNullOrEmpty(iconName))
		{
			return null;
		}
		if (Cache.TryGetValue(iconName, out Texture2D value))
		{
			return value;
		}
		string path = "res://assets/state-icons/" + iconName + ".jpg";
		Texture2D texture2D = LoadTextureFile(path);
		if (texture2D == null)
		{
			texture2D = LoadTextureFile("res://assets/state-icons/" + iconName + ".png");
		}
		if (texture2D == null)
		{
			texture2D = ItemIcons.For(iconName);
		}
		if (texture2D == null)
		{
			string cleanName = ItemIcons.SafeFileStem(iconName);
			texture2D = LoadTextureFile($"res://assets/icons/items/{cleanName}.png");
			if (texture2D == null)
			{
				texture2D = LoadTextureFile($"res://assets/icons/items/魔法娃娃：{cleanName}.png");
			}
		}
		Cache[iconName] = texture2D;
		return texture2D;
	}

	private static Texture2D? LoadTextureFile(string resPath)
	{
		if (string.IsNullOrEmpty(resPath))
		{
			return null;
		}
		Texture2D? direct = GodotDataFiles.TryLoadTexture(resPath);
		if (direct != null)
		{
			return direct;
		}
		if (ResourceLoader.Exists(resPath))
		{
			return GD.Load<Texture2D>(resPath);
		}
		Image img = new Image();
		string absPath = ProjectSettings.GlobalizePath(resPath);
		if (FileAccess.FileExists(resPath) && img.Load(resPath) == Error.Ok)
		{
			return ImageTexture.CreateFromImage(img);
		}
		if (System.IO.File.Exists(absPath) && img.Load(absPath) == Error.Ok)
		{
			return ImageTexture.CreateFromImage(img);
		}
		string exeDir = System.AppContext.BaseDirectory;
		if (resPath.StartsWith("res://"))
		{
			string diskRel = System.IO.Path.Combine(exeDir, resPath.Substring(6).Replace('/', System.IO.Path.DirectorySeparatorChar));
			if (System.IO.File.Exists(diskRel) && img.Load(diskRel) == Error.Ok)
			{
				return ImageTexture.CreateFromImage(img);
			}
		}
		return null;
	}
}
