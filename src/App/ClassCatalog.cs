using System;
using System.Collections.Generic;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class ClassCatalog
{
	public static readonly ClassDef[] All = new ClassDef[8]
	{
		Def("royal", "王族", "王子", "公主", "blunt", "town_talking", "只有王族能創立血盟、率領成員並爭奪城堡。\n戰鬥能力較為平均，真正的力量來自領導與同伴。"),
		Def("knight", "騎士", "男騎士", "女騎士", "sword1", "town_silver_knight", "擅長近距離戰鬥，能使用多種武器與厚重防具。\n攻擊與防禦穩定，是最適合站在隊伍前線的職業。"),
		Def("elf", "妖精", "男妖精", "女妖精", "bow", "town_elf", "善用弓箭與精靈魔法，能在遠距離穩定輸出。\n能力均衡，並可選擇不同元素發展自己的戰鬥方式。"),
		Def("mage", "法師", "男法師", "女法師", "blunt", "town_talking", "能學習最多種類的魔法，以強力法術與輔助能力改變戰局。\n初期身體較弱，成長後具有極高的魔法潛力。"),
		Def("dark", "黑暗妖精", "男黑暗妖精", "女黑暗妖精", "dagger", "town_silent", "使用匕首、雙刀、鋼爪與十字弓，擅長高速爆發。\n防禦較不穩定，但能在短時間內造成致命傷害。"),
		Def("illusion", "幻術師", "男幻術士", "女幻術士", "wand", "town_hyperia", "操縱精神、幻象與奇古獸作戰，兼具傷害與隊伍支援。\n熟練後能用多樣化的幻術左右戰場。"),
		Def("dragon", "龍騎士", "男龍騎士", "女龍騎士", "chainsword", "town_behemoth", "繼承龍之血脈，使用鎖鏈劍與龍之力量突破防線。\n能以生命力換取強大攻擊，掌握弱點後爆發力驚人。"),
		Def("warrior", "戰士", "男戰士", "女戰士", "sword2", "town_heine", "以強健體魄與沉重武器壓制敵人，擁有優秀的耐久力。\n擅長近身持續作戰，即使被包圍也能守住前線。")
	};

	private static ClassDef Def(string id, string name, string maleAvatar, string femaleAvatar, string weapon, string returnTown, string description)
	{
		ClassGrowthRules.ClassGrowthProfile classGrowthProfile = ClassGrowthRules.Profile(id);
		return new ClassDef(id, name, maleAvatar, femaleAvatar, weapon, returnTown, classGrowthProfile.FreePoints, classGrowthProfile.Str, classGrowthProfile.Dex, classGrowthProfile.Con, classGrowthProfile.Int, classGrowthProfile.Wis, classGrowthProfile.Cha, description);
	}

	public static PlayerBuild ToBuild(ClassDef def, bool male, IReadOnlyDictionary<string, int>? allocations = null, int level = 1, string characterName = "")
	{
		return new PlayerBuild(def.Id, def.Name, def.Avatar(male), def.Weapon, male, level)
		{
			CharacterName = characterName.Trim(),
			Allocations = ((allocations == null) ? new Dictionary<string, int>() : new Dictionary<string, int>(allocations)),
			ReturnTown = def.ReturnTown,
			StartingGold = 5000L
		};
	}

	public static ClassDef? Find(string idOrName)
	{
		idOrName = idOrName switch
		{
			"darkelf" => "dark", 
			"dknight" => "dragon", 
			"illusionist" => "illusion", 
			"幻術士" => "illusion", 
			_ => idOrName, 
		};
		ClassDef[] all = All;
		foreach (ClassDef classDef in all)
		{
			if (string.Equals(classDef.Id, idOrName, StringComparison.Ordinal) || string.Equals(classDef.Name, idOrName, StringComparison.Ordinal))
			{
				return classDef;
			}
		}
		return null;
	}
}
