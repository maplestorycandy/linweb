using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class HostilePlayerGenerator
{
	public const double SkillPickChance = 0.55;

	public static readonly string[] ClassIds = new string[8] { "royal", "knight", "elf", "mage", "dark", "dragon", "illusion", "warrior" };

	private static readonly IReadOnlyDictionary<string, string[]> StatPriorities = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		["royal"] = new string[6] { "str", "cha", "con", "dex", "wis", "int" },
		["knight"] = new string[6] { "str", "con", "dex", "wis", "cha", "int" },
		["elf"] = new string[6] { "dex", "con", "wis", "int", "str", "cha" },
		["mage"] = new string[6] { "int", "wis", "con", "dex", "cha", "str" },
		["dark"] = new string[6] { "dex", "str", "con", "wis", "int", "cha" },
		["dragon"] = new string[6] { "con", "str", "wis", "dex", "int", "cha" },
		["illusion"] = new string[6] { "int", "wis", "con", "str", "dex", "cha" },
		["warrior"] = new string[6] { "str", "con", "dex", "wis", "cha", "int" }
	};

	public const string RoyalMaleAvatar = "王子";

	public const string RoyalFemaleAvatar = "公主";

	public static HostilePlayerTemplate? GenerateCandidateAt(IGameData data, ICombatRandom random, int level, IEnumerable<string>? takenNames = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(random, "random");
		return GenerateCandidate(data, random, Math.Clamp(level, 1, 99), new HashSet<string>(takenNames ?? Array.Empty<string>(), StringComparer.Ordinal));
	}

	private static HostilePlayerTemplate? GenerateCandidate(IGameData data, ICombatRandom random, int level, IReadOnlySet<string> takenNames)
	{
		string classId = ClassIds[Math.Min(ClassIds.Length - 1, (int)(random.NextDouble() * (double)ClassIds.Length))];
		bool male = random.NextDouble() < 0.5;
		string displayName = RollName(random, takenNames);
		string avatar = AvatarFor(classId, male);
		string text = "hire-" + Guid.NewGuid().ToString("N");
		Dictionary<string, int> allocations = RollCreationAllocations(random, classId);
		Dictionary<string, int> levelStatBonuses = RollLevelStatBonuses(random, classId, level, allocations);
		Combatant combatant;
		try
		{
			combatant = CombatantBuilder.CreatePlayer(data, new PlayerCombatantSpec(text, displayName, classId, level)
			{
				Avatar = avatar,
				Allocations = allocations,
				LevelStatBonuses = levelStatBonuses
			});
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is KeyNotFoundException || ex is InvalidOperationException) ? 1 : 0) != 0)
		{
			return null;
		}
		IReadOnlyList<string> readOnlyList = RollSkills(data, random, combatant);
		foreach (string item in readOnlyList)
		{
			combatant.LearnedSkills.Add(item);
		}
		IReadOnlyDictionary<string, ItemStack> equippedItems = RollEquipment(data, random, combatant);
		HostilePlayerTemplate hostilePlayerTemplate = new HostilePlayerTemplate(text, displayName, classId, level)
		{
			Avatar = avatar,
			Allocations = allocations,
			LevelStatBonuses = levelStatBonuses,
			EquippedItems = equippedItems,
			LearnedSkills = readOnlyList
		};
		if (TemplateBuilds(data, hostilePlayerTemplate))
		{
			return hostilePlayerTemplate;
		}
		return hostilePlayerTemplate with
		{
			EquippedItems = new Dictionary<string, ItemStack>()
		};
	}

	private static bool TemplateBuilds(IGameData data, HostilePlayerTemplate template)
	{
		try
		{
			CombatantBuilder.CreatePlayer(data, new PlayerCombatantSpec("hostile-" + template.RosterId, template.DisplayName, template.ClassId, template.Level)
			{
				Avatar = template.Avatar,
				Allocations = template.Allocations,
				LevelStatBonuses = template.LevelStatBonuses,
				EquippedItems = template.EquippedItems
			});
			return true;
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	internal static string AvatarFor(string classId, bool male)
	{
		if (!ClassKitRegistry.TryGet(classId, out ClassKit kit) || kit == null)
		{
			return string.Empty;
		}
		string defaultAvatar = kit.DefaultAvatar;
		if (string.Equals(defaultAvatar, "王子", StringComparison.Ordinal))
		{
			if (!male)
			{
				return "公主";
			}
			return "王子";
		}
		if (male || defaultAvatar.Length == 0 || defaultAvatar[0] != '男')
		{
			return defaultAvatar;
		}
		return "女" + defaultAvatar.Substring(1);
	}

	private static string RollName(ICombatRandom random, IReadOnlySet<string> takenNames)
	{
		for (int i = 0; i < 8; i++)
		{
			string text = PlayerNames.Random(random);
			if (!takenNames.Contains(text))
			{
				return text;
			}
		}
		for (int j = 0; j < PlayerNames.Combinations * 8; j++)
		{
			string text2 = PlayerNames.FromSeed(j);
			if (!takenNames.Contains(text2))
			{
				return text2;
			}
		}
		return PlayerNames.Random(random);
	}

	private static IReadOnlyList<string> RollSkills(IGameData data, ICombatRandom random, Combatant probe)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		bool flag = false;
		foreach (var (text2, jsonNode2) in data.Skills)
		{
			if (!(jsonNode2 is JsonObject) || !ClassKitRegistry.CanLearnSkill(probe, text2, data, out var _))
			{
				continue;
			}
			AllySkillClass allySkillClass = AllyBehaviorRules.Classify(data, text2);
			if (allySkillClass != AllySkillClass.None)
			{
				bool flag2 = (uint)(allySkillClass - 1) <= 1u;
				bool flag3 = flag2;
				if (flag3)
				{
					list2.Add(text2);
				}
				if (random.NextDouble() < 0.55)
				{
					list.Add(text2);
					flag = flag || flag3;
				}
			}
		}
		if (!flag && list2.Count > 0)
		{
			list.Add(list2[Math.Min(list2.Count - 1, (int)(random.NextDouble() * (double)list2.Count))]);
		}
		return list;
	}

	private static IReadOnlyDictionary<string, ItemStack> RollEquipment(IGameData data, ICombatRandom random, Combatant probe)
	{
		Dictionary<string, List<(string Key, long Price)>> bySlot = new Dictionary<string, List<(string, long)>>(StringComparer.Ordinal);
		bool value2 = default(bool);
		foreach (var (text2, jsonNode2) in data.Items)
		{
			if (!(jsonNode2 is JsonObject jsonObject))
			{
				continue;
			}
			string text3 = jsonObject["type"]?.GetValue<string>() ?? "";
			bool flag = CombatSkill.ReadBool(jsonObject, "isArrow") || CombatSkill.ReadBool(jsonObject, "isSting");
			bool flag2;
			switch (text3)
			{
			case "wpn":
			case "arm":
			case "acc":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			if (!flag2 && !flag)
			{
				continue;
			}
			long value;
			long num = ((jsonObject["p"] is JsonValue jsonValue && jsonValue.TryGetValue<long>(out value)) ? value : 0);
			if (num > 0 && !(jsonObject["relic"] is JsonValue jsonValue2 && jsonValue2.TryGetValue<bool>(out value2) && value2) && EquipmentRules.Evaluate(data, probe, new ItemStack("gen-probe", text2, 1L)).Allowed)
			{
				string key = (flag ? "arrow" : ((text3 == "wpn") ? "wpn" : CombatSkill.ReadString(jsonObject, "slot")));
				if (!bySlot.TryGetValue(key, out List<(string, long)> value3))
				{
					value3 = (bySlot[key] = new List<(string, long)>());
				}
				value3.Add((text2, num));
			}
		}
		int generatedUid = 0;
		Pick("wpn", 1.0, "wpn");
		if (probe.ClassId == "warrior" && probe.LearnedSkills.Contains("sk_warrior_dualaxe"))
		{
			Pick("wpn", 0.65, "offwpn");
		}
		Pick("armor", 1.0, "armor");
		Pick("tshirt", 0.9, "tshirt");
		Pick("helm", 0.9, "helm");
		Pick("boots", 0.85, "boots");
		Pick("gloves", 0.85, "gloves");
		Pick("cloak", 0.85, "cloak");
		Pick("amulet", 0.8, "amulet");
		Pick("belt", 0.8, "belt");
		Pick("ring", 0.85, "ring1");
		Pick("ring", 0.85, "ring2");
		if (probe.Level >= 76)
		{
			Pick("ring", 0.6, "ring3");
		}
		if (probe.Level >= 81)
		{
			Pick("ring", 0.6, "ring4");
		}
		Pick("ear", 0.8, "ear1");
		if (probe.Level >= 59)
		{
			Pick("ear", 0.6, "ear2");
		}
		if (!probe.EquippedItems.TryGetValue("wpn", out ItemStack value4) || !EquipmentRules.IsTwoHandedWeapon(data, probe, value4.ItemKey))
		{
			Pick("shield", 0.75, "shield");
		}
		if (AmmunitionRules.RequiresArrow(data, probe))
		{
			Pick("arrow", 1.0, "arrow");
		}
		return CombatEquipment.Snapshot(probe);
		bool Pick(string baseSlot, double chance, string requiredSlot)
		{
			if (random.NextDouble() >= chance)
			{
				return false;
			}
			if (!bySlot.TryGetValue(baseSlot, out List<(string, long)> value5) || value5.Count == 0)
			{
				return false;
			}
			value5.Sort(((string Key, long Price) left, (string Key, long Price) right) => left.Price.CompareTo(right.Price));
			int num2 = (int)Math.Round(Math.Clamp(Math.Clamp((double)probe.Level / 60.0, 0.02, 1.0) + (random.NextDouble() * 2.0 - 1.0) * 0.15, 0.0, 1.0) * (double)(value5.Count - 1));
			for (int num3 = 0; num3 < value5.Count; num3++)
			{
				string item = value5[(num2 + num3) % value5.Count].Item1;
				JsonObject jsonObject2 = data.Item(item);
				if (jsonObject2 != null)
				{
					if (baseSlot == "arrow")
					{
						bool flag3 = AmmunitionRules.UsesStings(data, probe);
						if (!CombatSkill.ReadBool(jsonObject2, flag3 ? "isSting" : "isArrow"))
						{
							continue;
						}
					}
					ItemStack candidate = new ItemStack($"hostile-gear-{++generatedUid}", item, (!(baseSlot == "arrow")) ? 1 : 5000)
					{
						Enhancement = PreEnhancedLootRules.RollEnhancement(jsonObject2, random.NextDouble())
					};
					EquipmentEligibilityResult equipmentEligibilityResult = EquipmentRules.Evaluate(data, probe, candidate);
					if (equipmentEligibilityResult.Allowed && string.Equals(equipmentEligibilityResult.Slot, requiredSlot, StringComparison.Ordinal))
					{
						probe.InventoryStacks.Add(candidate);
						if (CombatEquipment.TryEquip(data, probe, candidate.Uid).Success)
						{
							return true;
						}
						probe.InventoryStacks.RemoveAll((ItemStack itemStack) => itemStack.Uid == candidate.Uid);
					}
				}
			}
			return false;
		}
	}

	private static Dictionary<string, int> RollCreationAllocations(ICombatRandom random, string classId)
	{
		ClassGrowthRules.ClassGrowthProfile classGrowthProfile = ClassGrowthRules.Profile(classId);
		Dictionary<string, int> current = BaseStatMap(classGrowthProfile);
		return RollStatPoints(random, classId, classGrowthProfile.FreePoints, 18, current);
	}

	private static Dictionary<string, int> RollLevelStatBonuses(ICombatRandom random, string classId, int level, IReadOnlyDictionary<string, int> allocations)
	{
		Dictionary<string, int> dictionary = BaseStatMap(ClassGrowthRules.Profile(classId));
		foreach (KeyValuePair<string, int> allocation in allocations)
		{
			allocation.Deconstruct(out var key, out var value);
			string key2 = key;
			int val = value;
			dictionary[key2] = dictionary.GetValueOrDefault(key2) + Math.Max(0, val);
		}
		return RollStatPoints(random, classId, L1jLevelStatRules.EarnedLevelPoints(level), 35, dictionary);
	}

	private static Dictionary<string, int> RollStatPoints(ICombatRandom random, string classId, int pointCount, int maximum, Dictionary<string, int> current)
	{
		string[] array = StatPriorities[classId];
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		for (int i = 0; i < pointCount; i++)
		{
			int num = Math.Min(array.Length - 1, (int)(random.NextDouble() * random.NextDouble() * (double)array.Length));
			string text = null;
			for (int j = 0; j < array.Length; j++)
			{
				string text2 = array[(num + j) % array.Length];
				if (current.GetValueOrDefault(text2) < maximum)
				{
					text = text2;
					break;
				}
			}
			if (text == null)
			{
				break;
			}
			current[text] = current.GetValueOrDefault(text) + 1;
			dictionary[text] = dictionary.GetValueOrDefault(text) + 1;
		}
		return dictionary;
	}

	private static Dictionary<string, int> BaseStatMap(ClassGrowthRules.ClassGrowthProfile profile)
	{
		return new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["str"] = profile.Str,
			["dex"] = profile.Dex,
			["con"] = profile.Con,
			["int"] = profile.Int,
			["wis"] = profile.Wis,
			["cha"] = profile.Cha
		};
	}
}
