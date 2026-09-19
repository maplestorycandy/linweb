using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PetRules
{
	public const string DefinitionTable = "L1J_PET_TYPES";

	public const int StorageMaximum = 180;

	public const int EvolutionLevel = 30;

	public const int MainPetCost = 6;

	public const int MainHighPetCost = 12;

	public const int InitialFood = 20;

	public const int FollowFoodMinimum = 10;

	public const string EvolutionFruitItemKey = "item_evo_fruit";

	public const string VictoryFruitItemKey = "item_victory_fruit";

	public const string ReviveScrollItemKey = "scroll_revive";

	public const double ReviveDelaySeconds = 0.0;

	public const double ReviveHealthRatio = 0.25;

	public const double FollowDistance = 96.0;

	public const double CombatLeashDistance = 480.0;

	public const double WarpDistance = 864.0;

	public const double SkillRange = 72.0;

	public const double AreaEffectRadius = 72.0;

	public const double BasicDamageTune = 1.0;

	public const double SkillDamageTune = 1.0;

	public const double HitTune = 0.0;

	public const double RoyalCompanionCharmCostRate = 0.8;

	public const int MaximumLevel = 50;

	public static double TierDamageMultiplier(int tier)
	{
		return 1.0;
	}

	public static IReadOnlyList<PetDefinition> Definitions(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return L1jPetTypeCatalog.Load(data).ByForm.Keys.Select((string form) => Definition(data, form)).ToArray();
	}

	public static PetDefinition Definition(IGameData data, string form)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(form, "form");
		if (!L1jPetTypeCatalog.Load(data).TryGet(form, out L1jPetTypeDefinition definition))
		{
			throw new KeyNotFoundException("Unknown main pet form '" + form + "'.");
		}
		string text = $"l1j_{definition.BaseNpcId}";
		JsonObject mob = data.Mob(text) ?? throw new InvalidDataException($"main pet '{definition.Form}' is missing npc.sql template {definition.BaseNpcId}.");
		L1jMobInstanceStats l1jMobInstanceStats = L1jMobTemplateRules.Resolve(mob);
		MobBasicAttackProfile mobBasicAttackProfile = MobBasicAttackRules.Resolve(mob);
		bool flag = Enumerable.Range(1, 10).Any((int index) => mob[(index == 1) ? "mag" : $"mag{index}"] is JsonObject);
		double num = Math.Max(1.0 / 60.0, ReadDouble(mob, "atkSpd", 2.0));
		return new PetDefinition(definition.Form, (mobBasicAttackProfile.Kind == MobBasicAttackKind.Magic) ? PetKind.Magic : PetKind.Physical, 0, l1jMobInstanceStats.Level, l1jMobInstanceStats.MaximumHealth, l1jMobInstanceStats.MaximumMana, definition.HpGrowthMin, definition.HpGrowthMax, definition.MpGrowthMin, definition.MpGrowthMax, Math.Max(0.0, ReadDouble(mob, "hpRegen")), Math.Max(0.0, ReadDouble(mob, "mpRegen")), 60.0 / num, 0.0, 0.5, MainPetCostFor(definition.BaseNpcId), (definition.EvolutionItemId > 0) ? (definition.EvolutionForm ?? string.Empty) : string.Empty, flag, flag, HasExtraSkill: false, HasDebuffSkill: false, 1.0, 1.0, 0.0, 0.0, 0.0, definition.BaseNpcId, text, definition.DefyMessageId);
	}

	public static int MainPetCostFor(int baseNpcId)
	{
		if ((baseNpcId != 45313 && (uint)(baseNpcId - 45710) > 2u) || 1 == 0)
		{
			return 6;
		}
		return 12;
	}

	public static IReadOnlyList<PetSkillDefinition> Skills(IGameData data, string form)
	{
		Definition(data, form);
		return Array.Empty<PetSkillDefinition>();
	}

	public static PetInstance CreateInstance(IGameData data, string form, string uid, int? level = null)
	{
		PetDefinition petDefinition = Definition(data, form);
		int num = Math.Clamp(level ?? petDefinition.StartingLevel, 1, 50);
		double num2 = petDefinition.StartingHp;
		double num3 = petDefinition.StartingMp;
		for (int i = petDefinition.StartingLevel; i < num; i++)
		{
			num2 += (double)petDefinition.HpGrowthMin;
			num3 += (double)petDefinition.MpGrowthMin;
		}
		return new PetInstance(uid, petDefinition.Form, num, Math.Max(1.0, num2), Math.Max(0.0, num3))
		{
			Lawful = L1jMobTemplateRules.Resolve(data.Mob(petDefinition.MobKey)).Lawful,
			Experience = ProgressionRules.ExperienceAtLevel(data, num)
		};
	}

	public static PetDerivedStats Derive(IGameData data, PetInstance pet, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(pet, "pet");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		PetDefinition petDefinition = Definition(data, pet.Form);
		JsonObject jsonObject = data.Mob(petDefinition.MobKey);
		L1jMobInstanceStats l1jMobInstanceStats = L1jMobTemplateRules.Resolve(jsonObject);
		MobBasicAttackProfile mobBasicAttackProfile = MobBasicAttackRules.Resolve(jsonObject);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		int num11 = 0;
		int num12 = 0;
		L1jPetItemCatalog l1jPetItemCatalog = L1jPetItemCatalog.Load(data);
		foreach (var (text2, itemStack2) in pet.Equipment)
		{
			if (!l1jPetItemCatalog.TryGet(itemStack2.ItemKey, out L1jPetItemDefinition definition) || definition.Slot != text2)
			{
				throw new InvalidDataException($"Pet '{pet.Uid}' carries non-main equipment '{itemStack2.ItemKey}'.");
			}
			L1jPetItemStats stats = definition.Stats;
			num += stats.HitModifier;
			num2 += stats.DamageModifier;
			num3 += stats.ArmorClass;
			num4 += stats.Strength;
			num5 += stats.Constitution;
			num6 += stats.Dexterity;
			num7 += stats.Intelligence;
			num8 += stats.Wisdom;
			num9 += stats.MaxHp;
			num10 += stats.MaxMp;
			num11 += stats.SpellPower;
			num12 += stats.MagicResist;
		}
		L1jMobInstanceStats stats2 = l1jMobInstanceStats with
		{
			Level = Math.Clamp(pet.Level, 1, 99),
			Strength = Math.Clamp(l1jMobInstanceStats.Strength + num4, 0, 127),
			Constitution = Math.Clamp(l1jMobInstanceStats.Constitution + num5, 0, 127),
			Dexterity = Math.Clamp(l1jMobInstanceStats.Dexterity + num6, 0, 127),
			Intelligence = Math.Clamp(l1jMobInstanceStats.Intelligence + num7, 0, 127),
			Wisdom = Math.Clamp(l1jMobInstanceStats.Wisdom + num8, 0, 127)
		};
		double attackIntervalSeconds = Math.Max(1.0 / 60.0, ReadDouble(jsonObject, "atkSpd", 2.0));
		return new PetDerivedStats(petDefinition.Kind, Math.Max(1, l1jMobInstanceStats.Level), stats2.Strength / 2 + stats2.Level / 16 + num2, 1.0, 1.0, 0.0, 0.0, L1jMobTemplateRules.PhysicalHit(stats2) + num, l1jMobInstanceStats.ArmorClass + num3, Math.Max(0.0, ReadDouble(jsonObject, "dr")), 0.0, l1jMobInstanceStats.MagicResistance + num12, attackIntervalSeconds, 0.0, 5, 1.0, int.MaxValue, Math.Max(1.0, pet.MaxHp + (double)num9), Math.Max(0.0, pet.MaxMp + (double)num10), Math.Max(0.0, ReadDouble(jsonObject, "hpRegen")), Math.Max(0.0, ReadDouble(jsonObject, "mpRegen")), 1.0, stats2.Strength, stats2.Constitution, stats2.Dexterity, stats2.Intelligence, stats2.Wisdom, num11, petDefinition.MobKey, Math.Max(0.0, ReadDouble(jsonObject, "moveSpd", IsometricMovementRules.BaseMoveSpeed)), mobBasicAttackProfile.Range, mobBasicAttackProfile.UsesRangedPhysicalDamage, mobBasicAttackProfile.ProjectileKind, Math.Max(0.0, ReadDouble(jsonObject, "hpRegenIntervalMs") / 1000.0), Math.Max(0.0, ReadDouble(jsonObject, "mpRegenIntervalMs") / 1000.0), pet.Lawful);
	}

	public static int HiddenCharismaBonus(string? classId)
	{
		switch (ClassKitRegistry.NormalizeClassId(classId))
		{
		case "royal":
		case "elf":
			return 12;
		case "mage":
		case "dark":
		case "dragon":
		case "illusion":
			return 6;
		default:
			return 0;
		}
	}

	public static int CompanionDeploymentCharmCost(Combatant owner, int baseCost)
	{
		return checked((int)CompanionDeploymentCharmCost(owner, (double)baseCost));
	}

	public static double CompanionDeploymentCharmCost(Combatant owner, double baseCost)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		double num = Math.Max(0.0, baseCost);
		if (!string.Equals(ClassKitRegistry.NormalizeClassId(owner.ClassId), "royal", StringComparison.Ordinal))
		{
			return num;
		}
		return Math.Ceiling(num * 0.8);
	}

	public static double MainCharmCapacity(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return Math.Max(0.0, Math.Floor(owner.D.Cha) + (double)HiddenCharismaBonus(owner.ClassId));
	}

	public static double ExperienceRequired(IGameData data, int level)
	{
		if (level < 50)
		{
			return ProgressionRules.RequiredExperience(data, level);
		}
		return double.PositiveInfinity;
	}

	public static WorldPoint FormationPoint(Combatant owner, int index, int count)
	{
		int num = Math.Max(1, count);
		int num2 = Math.Clamp(index, 0, num - 1);
		double num3 = Math.PI / 2.0 + Math.PI * 2.0 * (double)num2 / (double)num;
		double num4 = ((num <= 2) ? 72 : 92);
		return new WorldPoint(owner.Pos.X + Math.Cos(num3) * num4, owner.Pos.Y + Math.Sin(num3) * num4);
	}

	internal static void ApplyGrowth(PetInstance pet, PetDefinition definition, ICombatRandom random)
	{
		pet.MaxHp += RollGrowth(random, definition.HpGrowthMin, definition.HpGrowthMax);
		pet.MaxMp += RollGrowth(random, definition.MpGrowthMin, definition.MpGrowthMax);
		pet.Hp = pet.MaxHp;
		pet.Mp = pet.MaxMp;
	}

	private static int RollGrowth(ICombatRandom random, int minimum, int maximum)
	{
		int num = Math.Max(0, Math.Min(minimum, maximum));
		int num2 = Math.Max(num, Math.Max(minimum, maximum));
		return num + (int)Math.Floor(random.NextDouble() * (double)(num2 - num + 1));
	}

	private static double ReadDouble(JsonObject source, string field, double fallback = 0.0)
	{
		if (source[field] != null)
		{
			return CombatSkill.ReadDouble(source, field);
		}
		return fallback;
	}
}
