using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class SummonRules
{
	public const double FollowDistance = 88.0;

	public const double CombatLeashDistance = 520.0;

	public const double WarpDistance = 900.0;

	public const double CorpseLifetimeSeconds = 1.0;

	public const int SummonNoControlMaximumLevel = 52;

	private static readonly HashSet<string> SummonSkillIds = new HashSet<string>(StringComparer.Ordinal) { "sk_summon", "sk_zombie", "sk_elf_summon", "sk_elf_summon2" };

	public static IReadOnlySet<string> SkillIds => SummonSkillIds;

	public static bool IsSummonSkill(string skillId, JsonObject source)
	{
		if (SummonSkillIds.Contains(skillId))
		{
			return source["summon"] is JsonObject;
		}
		return false;
	}

	public static IReadOnlyList<SummonFormInfo> AvailableForms(IGameData data, Combatant owner, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		JsonObject jsonObject = L1jSummonRules.Roster(data, skillId);
		if (jsonObject != null)
		{
			return L1jAvailableForms(data, owner, skillId, jsonObject);
		}
		switch (skillId)
		{
		case "sk_summon":
			return AvailableTieredForms(data, owner);
		case "sk_zombie":
			return Array.Empty<SummonFormInfo>();
		case "sk_elf_summon":
		case "sk_elf_summon2":
			return AvailableSpiritForms(data, owner, skillId);
		default:
			return Array.Empty<SummonFormInfo>();
		}
	}

	public static bool TryCreatePlan(IGameData data, Combatant owner, string skillId, string? preferredForm, out SummonPlan? plan, int existingPetCost = 0)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		plan = null;
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject == null || !IsSummonSkill(skillId, jsonObject))
		{
			return false;
		}
		JsonObject jsonObject2 = L1jSummonRules.Roster(data, skillId);
		IReadOnlyList<SummonUnitPlan> readOnlyList;
		if (jsonObject2 != null)
		{
			readOnlyList = BuildL1jSummons(data, owner, skillId, jsonObject2, existingPetCost);
		}
		else
		{
			IReadOnlyList<SummonUnitPlan> readOnlyList2;
			switch (skillId)
			{
			case "sk_summon":
				readOnlyList2 = BuildTieredSummons(data, owner, preferredForm);
				break;
			case "sk_zombie":
				readOnlyList2 = null;
				break;
			case "sk_elf_summon":
			case "sk_elf_summon2":
				readOnlyList2 = BuildSpirit(data, owner, skillId);
				break;
			default:
				readOnlyList2 = null;
				break;
			}
			readOnlyList = readOnlyList2;
		}
		IReadOnlyList<SummonUnitPlan> readOnlyList3 = readOnlyList;
		if (readOnlyList3 == null || readOnlyList3.Count == 0)
		{
			return false;
		}
		double durationSeconds = Math.Max(1.0 / 60.0, CombatSkill.ReadDouble(jsonObject, "dur", 3600.0));
		JsonObject jsonObject3 = L1jSummonRules.Roster(data, skillId);
		plan = new SummonPlan(skillId, durationSeconds, readOnlyList3, (jsonObject3 != null) ? L1jSummonRules.PetCostPerUnit(jsonObject3, owner) : 0);
		return true;
	}

	public static Combatant CreateCombatant(SummonUnitPlan plan, Combatant owner, string key, int bornSeq, WorldPoint position)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return new Combatant
		{
			Kind = CombatantKind.Summon,
			Key = key,
			Disp = plan.Form,
			Avatar = (string.IsNullOrWhiteSpace(plan.Avatar) ? plan.Form : plan.Avatar),
			Level = Math.Max(1, plan.Level),
			BornSeq = bornSeq,
			Pos = position,
			Radius = 22.0,
			MoveSpeed = ((plan.MoveSpeed > 0.0) ? plan.MoveSpeed : Math.Max(120.0, owner.MoveSpeed)),
			AttackRange = plan.AttackRange,
			AggroRange = 480.0,
			ProjectileSpeed = 560.0,
			ProjectileTurnRate = 7.0,
			BasicProjectileKind = (((object)plan.MagicAttack == null) ? string.Empty : "bolt"),
			Hp = plan.MaxHp,
			MaxHp = plan.MaxHp,
			Element = plan.Element,
			AttackElement = plan.Element,
			Size = "S",
			D = 
			{
				AttackInterval = plan.AttackIntervalSeconds,
				ArmorClass = plan.ArmorClass,
				MagicResist = plan.MagicResistance,
				DamageReduction = plan.DamageReduction,
				MeleeHit = plan.MeleeHit,
				Hit = plan.MeleeHit,
				MeleeDamage = plan.MeleeDamage,
				AttackDiceSmall = Math.Max(1, plan.AttackDice),
				AttackDiceLarge = Math.Max(1, plan.AttackDice)
			}
		};
	}

	public static WorldPoint FormationPoint(Combatant owner, int index, int count)
	{
		int num = Math.Max(1, count);
		int num2 = Math.Clamp(index, 0, num - 1);
		double num3 = Math.PI / 2.0 + Math.PI * 2.0 * (double)num2 / (double)num;
		double num4 = ((num <= 3) ? 64 : 82);
		return new WorldPoint(owner.Pos.X + Math.Cos(num3) * num4, owner.Pos.Y + Math.Sin(num3) * num4);
	}

	private static IReadOnlyList<SummonUnitPlan>? BuildL1jSummons(IGameData data, Combatant owner, string skillId, JsonObject roster, int existingPetCost)
	{
		string text = CombatSkill.ReadString(roster, "mode");
		int num = L1jSummonRules.SummonCountByPetCost(roster, owner, existingPetCost);
		if (num <= 0)
		{
			return null;
		}
		JsonObject jsonObject = null;
		switch (text)
		{
		case "tiered":
			jsonObject = L1jSummonRules.TierFor(roster, owner.Level)?["unit"] as JsonObject;
			break;
		case "elemental":
		{
			string text2 = NormalizeElement(owner.ElfElement);
			if (text2 == "none")
			{
				return null;
			}
			jsonObject = L1jSummonRules.FormFor(roster, text2)?["unit"] as JsonObject;
			break;
		}
		case "corpse":
			jsonObject = L1jSummonRules.ZombieFor(roster, owner)?["unit"] as JsonObject;
			break;
		default:
			throw new InvalidDataException($"L1J summon roster '{skillId}' has an unknown mode '{text}'.");
		}
		if (jsonObject == null)
		{
			return null;
		}
		SummonUnitPlan item = L1jSummonRules.UnitPlan(jsonObject);
		List<SummonUnitPlan> list = new List<SummonUnitPlan>(num);
		for (int i = 0; i < num; i++)
		{
			list.Add(item);
		}
		return list;
	}

	private static IReadOnlyList<SummonFormInfo> L1jAvailableForms(IGameData data, Combatant owner, string skillId, JsonObject roster)
	{
		string text = CombatSkill.ReadString(roster, "mode");
		JsonObject jsonObject = ((text == "corpse") ? (L1jSummonRules.ZombieFor(roster, owner)?["unit"] as JsonObject) : ((text == "tiered") ? (L1jSummonRules.TierFor(roster, owner.Level)?["unit"] as JsonObject) : (L1jSummonRules.FormFor(roster, NormalizeElement(owner.ElfElement))?["unit"] as JsonObject)));
		if (jsonObject != null)
		{
			return new SummonFormInfo[1]
			{
				new SummonFormInfo(CombatSkill.ReadString(jsonObject, "n"), CombatSkill.ReadInt(jsonObject, "lv"), 0, Unlocked: true, NeedsControl: false, string.Empty)
			};
		}
		return new SummonFormInfo[1]
		{
			new SummonFormInfo(CombatSkill.ReadString(roster, "l1jName"), 0, 0, Unlocked: false, NeedsControl: false, (text == "elemental") ? "需要先取得屬性" : "沒有可用的階梯")
		};
	}

	private static IReadOnlyList<SummonUnitPlan>? BuildTieredSummons(IGameData data, Combatant owner, string? preferredForm)
	{
		if (!(data.Table("SUMMON_TIERS") is JsonArray jsonArray))
		{
			return null;
		}
		bool flag = HasSummonControl(owner, data);
		JsonObject jsonObject = null;
		JsonObject jsonObject2 = null;
		int num = -1;
		if (!string.IsNullOrWhiteSpace(preferredForm) && flag)
		{
			for (int i = 0; i < jsonArray.Count; i++)
			{
				if (jsonArray[i] is JsonObject jsonObject3 && jsonObject3["mobs"] is JsonArray source)
				{
					JsonObject jsonObject4 = source.OfType<JsonObject>().FirstOrDefault((JsonObject candidate) => string.Equals(CombatSkill.ReadString(candidate, "n"), preferredForm, StringComparison.Ordinal));
					if (jsonObject4 != null && TierQualified(owner, jsonObject3) && (!CombatSkill.ReadBool(jsonObject4, "ring") || flag))
					{
						jsonObject = jsonObject3;
						jsonObject2 = jsonObject4;
						num = i;
						break;
					}
				}
			}
		}
		if (jsonObject2 == null)
		{
			for (int num2 = 0; num2 < jsonArray.Count; num2++)
			{
				if (jsonArray[num2] is JsonObject jsonObject5 && jsonObject5["mobs"] is JsonArray source2 && source2.FirstOrDefault() is JsonObject jsonObject6 && CombatSkill.ReadInt(jsonObject5, "reqLv") <= 52 && TierQualified(owner, jsonObject5))
				{
					jsonObject = jsonObject5;
					jsonObject2 = jsonObject6;
					num = num2;
				}
			}
		}
		if (jsonObject == null || jsonObject2 == null)
		{
			return null;
		}
		int num3 = CombatSkill.ReadInt(jsonObject, "fixedCount");
		if (num3 <= 0)
		{
			int num4 = Math.Max(1, CombatSkill.ReadInt(jsonObject, "div"));
			int max = Math.Max(0, CombatSkill.ReadInt(jsonObject, flag ? "ringCap" : "cap"));
			num3 = Math.Clamp((int)Math.Floor((Math.Max(0.0, owner.D.Cha) + 6.0) / (double)num4), 0, max);
		}
		if (num3 <= 0)
		{
			return null;
		}
		string form = CombatSkill.ReadString(jsonObject2, "n");
		int num5 = Math.Max(1, CombatSkill.ReadInt(jsonObject2, "lv"));
		double num6 = Math.Max(1.0, CombatSkill.ReadDouble(jsonObject2, "hp"));
		double num7 = Math.Max(1.0, CombatSkill.ReadDouble(jsonObject2, "aspd", 20.0));
		int num8 = Math.Max(1, CombatSkill.ReadInt(jsonObject, "cap"));
		double num9 = Math.Pow(MedianMobHp(jsonObject) / num6, 0.35);
		double num10 = Math.Max(0.01, CombatSkill.ReadDouble(jsonObject, "premium", 1.0));
		double num11 = (39.0 + 0.09 * Math.Max(0.0, owner.D.Cha) * (double)Math.Max(1, owner.Level)) * (1.0 + (double)Math.Max(0, num) * 0.06) * num10 * num9 / (double)num8 * (num7 / 10.0);
		double num12 = Math.Round(num11 * 0.55);
		int num13 = Math.Max(1, (int)Math.Round((num11 - num12) * 2.0));
		(double Damage, double Hit, double MagicDamage) tuple = EquipmentBonuses(owner, data);
		double item = tuple.Damage;
		double item2 = tuple.Hit;
		double item3 = tuple.MagicDamage;
		double num14 = 1.0 * (1.0 + Math.Min(12.0, Math.Max(0.0, owner.D.MagicDamage + item3)) / 80.0);
		double meleeHit = (double)owner.Level + Math.Floor((double)owner.Level * 0.75 + Math.Max(0.0, owner.D.Cha) * 0.35) + Math.Floor((double)num5 / 8.0) + (double)Math.Max(0, num) + 0.0 + item2 - (double)num5;
		double meleeDamage = Math.Max(0.0, Math.Floor((num12 + item) * num14));
		int attackDice = Math.Max(1, (int)Math.Round((double)num13 * num14));
		IReadOnlyList<SummonProcProfile> procs = BuildProcProfiles(jsonObject2, owner, num5, num, num14);
		List<SummonUnitPlan> list = new List<SummonUnitPlan>(num3);
		for (int num15 = 0; num15 < num3; num15++)
		{
			list.Add(new SummonUnitPlan(form, num5, num6, num7 / 10.0, 12.0, 10.0 - Math.Floor((double)num5 / 4.0), Math.Floor((double)num5 / 10.0), meleeHit, meleeDamage, attackDice, "none", null, procs, null));
		}
		return list;
	}

	private static IReadOnlyList<SummonFormInfo> AvailableTieredForms(IGameData data, Combatant owner)
	{
		if (!(data.Table("SUMMON_TIERS") is JsonArray source))
		{
			return Array.Empty<SummonFormInfo>();
		}
		bool flag = HasSummonControl(owner, data);
		List<SummonFormInfo> list = new List<SummonFormInfo>();
		foreach (JsonObject item in source.OfType<JsonObject>())
		{
			if (!(item["mobs"] is JsonArray source2))
			{
				continue;
			}
			int num = Math.Max(0, CombatSkill.ReadInt(item, "reqLv"));
			int num2 = Math.Max(0, CombatSkill.ReadInt(item, "reqCha"));
			if (CombatSkill.ReadInt(item, "fixedCount") <= 0)
			{
				num2 = Math.Max(num2, Math.Max(0, CombatSkill.ReadInt(item, "div") - 6));
			}
			bool flag2 = owner.Level >= num;
			bool flag3 = owner.D.Cha >= (double)num2;
			foreach (JsonObject item2 in source2.OfType<JsonObject>())
			{
				string text = CombatSkill.ReadString(item2, "n");
				if (text.Length != 0)
				{
					bool unlocked = flag2 && flag3 && flag;
					string lockReason = ((!flag2) ? $"需要等級 {num}" : ((!flag3) ? $"需要魅力 {num2}" : ((!flag) ? "需要召喚控制裝備" : string.Empty)));
					list.Add(new SummonFormInfo(text, num, num2, unlocked, CombatSkill.ReadBool(item2, "ring"), lockReason));
				}
			}
		}
		return (from form in list
			orderby form.RequiredLevel descending, form.RequiredCharisma descending
			select form).ThenBy<SummonFormInfo, string>((SummonFormInfo form) => form.Name, StringComparer.Ordinal).ToArray();
	}

	private static IReadOnlyList<SummonUnitPlan>? BuildSpirit(IGameData data, Combatant owner, string skillId)
	{
		string text = NormalizeElement(owner.ElfElement);
		if (text == "none")
		{
			return null;
		}
		bool flag = !(skillId == "sk_elf_summon2") && false;
		JsonObject jsonObject = (flag ? (data.Table("SPIRIT_KING") as JsonObject) : ((data.Table("SPIRIT_DEF") as JsonObject)?[skillId] as JsonObject));
		JsonObject jsonObject2 = (jsonObject?["ele"] as JsonObject)?[text] as JsonObject;
		if (jsonObject == null || jsonObject2 == null)
		{
			return null;
		}
		int num = Math.Max(1, CombatSkill.ReadInt(jsonObject, "lv"));
		double maxHp = Math.Max(1.0, CombatSkill.ReadDouble(jsonObject2, "hp"));
		double num2 = Math.Max(1.0, CombatSkill.ReadDouble(jsonObject2, "aspd", 18.0));
		(int Count, int Sides) tuple = ReadDice(jsonObject2["dice"]);
		int item = tuple.Count;
		int item2 = tuple.Sides;
		double num3 = Math.Max(0.01, CombatSkill.ReadDouble(jsonObject2, "scale", 20.0));
		(double Damage, double Hit, double MagicDamage) tuple2 = EquipmentBonuses(owner, data);
		double item3 = tuple2.Damage;
		double item4 = tuple2.Hit;
		double item5 = tuple2.MagicDamage;
		double flatDamage = Math.Floor(Math.Max(0.0, owner.D.Cha) * (double)Math.Max(1, owner.Level) / num3) + item3;
		double damageMultiplier = Math.Max(0.01, CombatSkill.ReadDouble(jsonObject, "dmgMult", 1.0)) * (1.0 + Math.Min(12.0, Math.Max(0.0, owner.D.MagicDamage + item5)) / 40.0);
		double hitValue = (double)owner.Level + CombatSkill.ReadDouble(jsonObject, "hitLvOff") + Math.Floor((double)owner.Level * 0.75 + Math.Max(0.0, owner.D.Cha) * 0.35) + item4;
		double magicResistancePenetration = CombatSkill.ReadDouble(jsonObject, "mrPenBase") + Math.Floor(Math.Max(0.0, owner.D.Cha) / 10.0);
		string form = SpiritFormName(((data.Table("SPIRIT_ELE_ZH") as JsonObject)?[text] as JsonValue)?.GetValue<string>() ?? text, skillId, flag);
		SummonAoeAttackProfile aoeAttack = (flag ? BuildSpiritKingAoe(jsonObject, text, item2, flatDamage, damageMultiplier, magicResistancePenetration) : null);
		return new SummonUnitPlan[1]
		{
			new SummonUnitPlan(form, num, maxHp, num2 / 10.0, 72.0, 10.0 - Math.Floor((double)num / 4.0), Math.Floor((double)num / 10.0), 0.0, 0.0, 1, text, new SummonMagicAttackProfile(item, item2, flatDamage, damageMultiplier, hitValue, magicResistancePenetration, text), Array.Empty<SummonProcProfile>(), aoeAttack)
		};
	}

	private static IReadOnlyList<SummonFormInfo> AvailableSpiritForms(IGameData data, Combatant owner, string skillId)
	{
		string text = NormalizeElement(owner.ElfElement);
		if (!(text == "none"))
		{
			bool flag = !(skillId == "sk_elf_summon2") && false;
			JsonObject obj = (flag ? (data.Table("SPIRIT_KING") as JsonObject) : ((data.Table("SPIRIT_DEF") as JsonObject)?[skillId] as JsonObject));
			JsonObject jsonObject = (obj?["ele"] as JsonObject)?[text] as JsonObject;
			string name = SpiritFormName(((data.Table("SPIRIT_ELE_ZH") as JsonObject)?[text] as JsonValue)?.GetValue<string>() ?? text, skillId, flag);
			bool flag2 = obj != null && jsonObject != null;
			return new SummonFormInfo[1]
			{
				new SummonFormInfo(name, 0, 0, flag2, NeedsControl: false, flag2 ? string.Empty : "缺少對應屬性資料")
			};
		}
		return new SummonFormInfo[1]
		{
			new SummonFormInfo("屬性精靈", 0, 0, Unlocked: false, NeedsControl: false, "尚未選擇精靈屬性")
		};
	}

	private static string SpiritFormName(string elementName, string skillId, bool king)
	{
		if (!king)
		{
			if (!(skillId == "sk_elf_summon2"))
			{
				return elementName + "屬性精靈";
			}
			return "強力" + elementName + "屬性精靈";
		}
		return elementName + "精靈王";
	}

	private static IReadOnlyList<SummonProcProfile> BuildProcProfiles(JsonObject mob, Combatant owner, int summonLevel, int tierIndex, double damageMultiplier)
	{
		if (!(mob["proc"] is JsonArray source))
		{
			return Array.Empty<SummonProcProfile>();
		}
		int num = Math.Max(1, (int)Math.Floor((double)summonLevel + (double)Math.Max(1, owner.Level) * 0.35 + (double)(Math.Max(0, tierIndex) * 2) + Math.Max(0.0, owner.D.Cha) * 0.5));
		int num2 = Math.Max(1, (int)Math.Ceiling((double)summonLevel * 0.6));
		List<SummonProcProfile> list = new List<SummonProcProfile>();
		foreach (JsonObject item in source.OfType<JsonObject>())
		{
			SummonProcKind? summonProcKind = CombatSkill.ReadString(item, "kind") switch
			{
				"poison" => SummonProcKind.Poison, 
				"poisonAll" => SummonProcKind.PoisonAll, 
				"magic" => SummonProcKind.Magic, 
				"magicAll" => SummonProcKind.MagicAll, 
				_ => null, 
			};
			double num3 = Math.Clamp(CombatSkill.ReadDouble(item, "p"), 0.0, 1.0);
			if (!summonProcKind.HasValue || num3 <= 0.0)
			{
				continue;
			}
			bool flag;
			if (summonProcKind.HasValue)
			{
				SummonProcKind valueOrDefault = summonProcKind.GetValueOrDefault();
				if ((uint)valueOrDefault <= 1u)
				{
					flag = true;
					goto IL_0193;
				}
			}
			flag = false;
			goto IL_0193;
			IL_0193:
			bool flag2 = flag;
			list.Add(new SummonProcProfile(summonProcKind.Value, num3, CombatSkill.ReadString(item, "name"), NormalizeElement(CombatSkill.ReadString(item, "ele")), (!flag2) ? 2 : 0, (!flag2) ? num2 : 0, flag2 ? Math.Max(1.0, Math.Floor((double)num / 2.0)) : ((double)num), flag2 ? 1.0 : (damageMultiplier * Math.Max(0.01, CombatSkill.ReadDouble(item, "heavy", 1.0))), CombatSkill.ReadBool(item, "slow"), CombatSkill.ReadBool(item, "stun"), CombatRangeRules.AreaEffectRadius(item)));
		}
		return list;
	}

	private static SummonAoeAttackProfile? BuildSpiritKingAoe(JsonObject root, string element, int diceSides, double flatDamage, double damageMultiplier, double magicResistancePenetration)
	{
		if (!(root["aoe"] is JsonObject jsonObject))
		{
			return null;
		}
		double num = Math.Clamp(CombatSkill.ReadDouble(jsonObject, "p"), 0.0, 1.0);
		if (num <= 0.0)
		{
			return null;
		}
		string value;
		string name = (((jsonObject["names"] as JsonObject)?[element] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? (value ?? "元素風暴") : "元素風暴");
		return new SummonAoeAttackProfile(num, name, element, 2, Math.Max(1, diceSides), Math.Floor(flatDamage / 2.0), damageMultiplier, magicResistancePenetration, CombatRangeRules.AreaEffectRadius(jsonObject));
	}

	private static bool TierQualified(Combatant owner, JsonObject tier)
	{
		if (owner.Level >= CombatSkill.ReadInt(tier, "reqLv"))
		{
			if (CombatSkill.ReadInt(tier, "reqCha") > 0)
			{
				return owner.D.Cha >= (double)CombatSkill.ReadInt(tier, "reqCha");
			}
			return true;
		}
		return false;
	}

	private static bool HasSummonControl(Combatant owner, IGameData data)
	{
		return owner.EquippedItems.Values.Any(delegate(ItemStack stack)
		{
			JsonObject jsonObject = data.Item(stack.ItemKey);
			return jsonObject != null && CombatSkill.ReadBool(jsonObject, "summonCtrl");
		});
	}

	private static (double Damage, double Hit, double MagicDamage) EquipmentBonuses(Combatant owner, IGameData data)
	{
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		foreach (ItemStack value in owner.EquippedItems.Values)
		{
			JsonObject jsonObject = data.Item(value.ItemKey);
			if (jsonObject != null)
			{
				num += CombatSkill.ReadDouble(jsonObject, "summonDmg");
				num2 += CombatSkill.ReadDouble(jsonObject, "summonHit");
				num3 += CombatSkill.ReadDouble(jsonObject, "summonMdmg");
			}
		}
		return (Damage: num, Hit: num2, MagicDamage: num3);
	}

	private static double MedianMobHp(JsonObject tier)
	{
		double[] array = (from mob in (tier["mobs"] as JsonArray)?.OfType<JsonObject>()
			select Math.Max(1.0, CombatSkill.ReadDouble(mob, "hp", 1.0)) into value
			orderby value
			select value).ToArray() ?? Array.Empty<double>();
		if (array.Length != 0)
		{
			return array[array.Length / 2];
		}
		return 1.0;
	}

	private static (int Count, int Sides) ReadDice(JsonNode? source)
	{
		if (!(source is JsonArray { Count: >=2 } jsonArray))
		{
			return (Count: 1, Sides: 1);
		}
		return (Count: Math.Max(1, ReadInt(jsonArray[0], 1)), Sides: Math.Max(1, ReadInt(jsonArray[1], 1)));
	}

	private static int ReadInt(JsonNode? node, int fallback)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return fallback;
		}
		return value;
	}

	private static double TableNumber(IGameData data, string tableName, double fallback)
	{
		if (!(data.Table(tableName) is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return fallback;
		}
		return value;
	}

	private static string NormalizeElement(string? element)
	{
		return element?.Trim().ToLowerInvariant() switch
		{
			"fire" => "fire", 
			"water" => "water", 
			"wind" => "wind", 
			"earth" => "earth", 
			_ => "none", 
		};
	}
}
