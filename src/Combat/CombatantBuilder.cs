using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CombatantBuilder
{
	public const int AttributeMaximum = 35;

	public const int AttributeAllocationMaximum = 18;

	private static double DefaultPlayerMoveSpeed => IsometricMovementRules.BaseMoveSpeed;

	public static Combatant CreatePlayer(IGameData data, PlayerCombatantSpec spec)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(spec, "spec");
		ArgumentException.ThrowIfNullOrWhiteSpace(spec.Key, "spec.Key");
		ArgumentException.ThrowIfNullOrWhiteSpace(spec.ClassId, "spec.ClassId");
		string text = ClassKitRegistry.NormalizeClassId(spec.ClassId);
		if (!ClassGrowthRules.IsKnownClass(text))
		{
			throw new ArgumentOutOfRangeException("spec", spec.ClassId, "Unknown player class.");
		}
		Attributes source = ClassGrowthRules.BaseAttributes(text);
		Combatant combatant = new Combatant
		{
			Kind = CombatantKind.Player,
			Key = spec.Key,
			Disp = (string.IsNullOrWhiteSpace(spec.DisplayName) ? spec.Key : spec.DisplayName),
			ClassId = text,
			Avatar = spec.Avatar,
			Level = Math.Clamp(spec.Level, 1, 99),
			Experience = Math.Max(Math.Max(0.0, spec.CurrentExperience), ProgressionRules.ExperienceAtLevel(data, Math.Clamp(spec.Level, 1, 99))),
			Gold = Math.Max(0L, spec.CurrentGold),
			ItemUidSequence = Math.Max(0L, spec.ItemUidSequence),
			BornSeq = spec.BornSeq,
			Pos = spec.Position,
			Radius = 18.0,
			MoveSpeed = DefaultPlayerMoveSpeed,
			AttackRange = 12.0,
			AggroRange = 480.0,
			Size = "S",
			Base = Clone(source),
			Allocations = new Dictionary<string, int>(spec.Allocations, StringComparer.Ordinal),
			LevelStatBonuses = new Dictionary<string, int>(spec.LevelStatBonuses, StringComparer.Ordinal),
			ElixirBonuses = new Dictionary<string, int>(spec.ElixirBonuses, StringComparer.Ordinal),
			ElixirStatus = spec.ElixirStatus,
			UnspentElixirStatPoints = spec.UnspentElixirStatPoints
		};
		if (!L1jElixirRules.HasValidState(combatant))
		{
			throw new ArgumentException("Player elixir state is invalid.", "spec");
		}
		if (!ClassKitRegistry.Bind(combatant))
		{
			throw new InvalidOperationException("Unable to bind class kit '" + text + "'.");
		}
		if (spec.Equipment.Count > 0 && spec.EquippedItems.Count > 0)
		{
			throw new ArgumentException("Provide either legacy equipment keys or equipped item instances, not both.", "spec");
		}
		if (spec.Inventory.Count > 0 && spec.InventoryStacks.Count > 0)
		{
			throw new ArgumentException("Provide either legacy inventory counts or inventory item instances, not both.", "spec");
		}
		Dictionary<string, ItemStack> dictionary = new Dictionary<string, ItemStack>(StringComparer.Ordinal);
		string key;
		foreach (KeyValuePair<string, ItemStack> equippedItem in spec.EquippedItems)
		{
			equippedItem.Deconstruct(out key, out var value);
			string text2 = key;
			ItemStack itemStack = value;
			if (string.IsNullOrWhiteSpace(text2))
			{
				throw new ArgumentException("Equipment slots cannot be empty.", "spec");
			}
			ItemStackInventory.ValidateStack(itemStack);
			if (data.Item(itemStack.ItemKey) == null)
			{
				throw new KeyNotFoundException("Equipment item '" + itemStack.ItemKey + "' was not found.");
			}
			dictionary[text2] = itemStack.Copy();
		}
		foreach (KeyValuePair<string, string> item in spec.Equipment)
		{
			item.Deconstruct(out key, out var value2);
			string text3 = key;
			string text4 = value2;
			if (!string.IsNullOrWhiteSpace(text3) && !string.IsNullOrWhiteSpace(text4))
			{
				if (data.Item(text4) == null)
				{
					throw new KeyNotFoundException("Equipment item '" + text4 + "' was not found.");
				}
				dictionary[text3] = new ItemStack(CombatInventory.NextUid(combatant), text4, 1L);
			}
		}
		CombatEquipment.Load(combatant, dictionary);
		if (spec.InventoryStacks.Count > 0)
		{
			foreach (ItemStack inventoryStack in spec.InventoryStacks)
			{
				ItemStackInventory.ValidateStack(inventoryStack);
				if (data.Item(inventoryStack.ItemKey) == null)
				{
					throw new KeyNotFoundException("Inventory item '" + inventoryStack.ItemKey + "' was not found.");
				}
			}
			CombatInventory.LoadStacks(combatant, spec.InventoryStacks, spec.ItemUidSequence);
		}
		else
		{
			CombatInventory.LoadPlainCounts(combatant, spec.Inventory);
		}
		RefreshPlayer(combatant, data, restoreResources: true);
		combatant.Hp = Math.Clamp(spec.CurrentHp ?? combatant.MaxHp, 0.0, combatant.MaxHp);
		combatant.Mp = Math.Clamp(spec.CurrentMp ?? combatant.MaxMp, 0.0, combatant.MaxMp);
		combatant.Dead = combatant.Hp <= 0.0;
		return combatant;
	}

	public static Combatant CreateMob(IGameData data, string mobKey, string? instanceKey = null, int bornSeq = 0, WorldPoint? position = null, ICombatRandom? random = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mobKey, "mobKey");
		JsonObject definition = data.Mob(mobKey) ?? throw new KeyNotFoundException("Mob '" + mobKey + "' was not found.");
		return CreateMobFromDefinition(data, mobKey, definition, instanceKey, bornSeq, position, random);
	}

	public static Combatant CreateMobFromDefinition(IGameData data, string mobKey, JsonObject definition, string? instanceKey = null, int bornSeq = 0, WorldPoint? position = null, ICombatRandom? random = null, bool useDefinitionCombatStats = false)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mobKey, "mobKey");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		bool flag = ReadString(definition, "src") == "l1j";
		bool flag2 = flag && !useDefinitionCombatStats;
		L1jMobInstanceStats stats = L1jMobTemplateRules.Resolve(definition, flag ? random : null);
		int level = stats.Level;
		string text = ReadString(definition, "s", "S");
		double num = stats.MaximumHealth;
		double num2 = stats.MaximumMana;
		bool flag3 = ReadBool(definition, "noAttack");
		bool flag4 = ReadBool(definition, "fleeOnly");
		bool flag5 = flag3 || flag4 || ReadBool(definition, "cannotAttack");
		bool hard = ReadBool(definition, "hard");
		bool isBoss = CombatSkill.ReadSystemBossFlag(definition);
		MobBasicAttackProfile mobBasicAttackProfile = MobBasicAttackRules.Resolve(definition);
		Combatant combatant = new Combatant
		{
			Kind = CombatantKind.Mob,
			UsesMonsterTemplate = true,
			Key = (instanceKey ?? ((bornSeq > 0) ? $"{mobKey}#{bornSeq}" : mobKey)),
			Disp = ReadString(definition, "n", mobKey),
			Avatar = mobKey,
			Level = level,
			BornSeq = bornSeq,
			Pos = (position ?? WorldPoint.Zero),
			Hp = num,
			MaxHp = num,
			Mp = num2,
			MaxMp = num2,
			Radius = ((text == "L") ? 22.5 : 16.5),
			MoveSpeed = (flag3 ? 0.0 : ReadDouble(definition, "moveSpd", IsometricMovementRules.BaseMoveSpeed)),
			AttackRange = (flag5 ? 0.0 : mobBasicAttackProfile.Range),
			AggroRange = (flag5 ? 0.0 : ReadDouble(definition, "aggroRange", 576.0)),
			Element = ReadString(definition, "e", "none"),
			AttackElement = ReadString(definition, "atkEle", ReadString(definition, "e", "none")),
			Size = text,
			Race = ReadString(definition, "race"),
			IsBoss = isBoss,
			Passive = (ReadString(definition, "beh") == "被動"),
			CannotAttack = flag5,
			FleeOnly = flag4,
			Hard = hard,
			ExperienceReward = stats.ExperienceReward,
			Alignment = stats.Lawful,
			DeathTransformMob = ReadString(definition, "deathTransformMob", ReadString(definition, "death_transform_mob", "")),
			DeathTransformFx = ReadString(definition, "deathTransformFx", ReadString(definition, "death_transform_fx", "")),
			DeathTransformDelay = ReadDouble(definition, "deathTransformDelay", ReadDouble(definition, "death_transform_delay", 1.0)),
			DeathTransformChance = ReadDouble(definition, "deathTransformChance", ReadDouble(definition, "death_transform_chance", 100.0)),
			Base = new Attributes
			{
				Str = stats.Strength,
				Con = stats.Constitution,
				Dex = stats.Dexterity,
				Int = stats.Intelligence,
				Wis = stats.Wisdom
			},
			GoldMin = Math.Max(0, ReadInt(definition, "goldMin")),
			GoldMax = Math.Max(0, ReadInt(definition, "goldMax")),
			GoldChance = Math.Clamp(ReadDouble(definition, "goldChance", 1.0), 0.0, 1.0),
			DropMultiplier = Math.Max(0.0, ReadDouble(definition, "dropMult", 1.0)),
			MobHealthRegenIntervalSeconds = Math.Max(0.0, ReadDouble(definition, "hpRegenIntervalMs") / 1000.0),
			MobHealthRegenAmount = Math.Max(0.0, ReadDouble(definition, "hpRegen")),
			MobManaRegenIntervalSeconds = Math.Max(0.0, ReadDouble(definition, "mpRegenIntervalMs") / 1000.0),
			MobManaRegenAmount = Math.Max(0.0, ReadDouble(definition, "mpRegen")),
			BasicProjectileKind = (flag5 ? string.Empty : mobBasicAttackProfile.ProjectileKind),
			ProjectileSpeed = (mobBasicAttackProfile.UsesMagicDamage ? 560.0 : 640.0),
			ProjectileTurnRate = (mobBasicAttackProfile.UsesMagicDamage ? 7.0 : 5.0)
		};
		int num3 = (flag2 ? 1 : ReadArrayInt(definition["dmg"], 0, 1));
		int val = (flag2 ? level : ReadArrayInt(definition["dmg"], 1, num3));
		double num4 = (flag2 ? ((double)L1jMobTemplateRules.PhysicalHit(stats)) : ReadDouble(definition, "hit"));
		combatant.D.AttackInterval = Math.Max(1.0 / 60.0, ReadDouble(definition, "atkSpd", 2.0));
		combatant.D.Str = stats.Strength;
		combatant.D.Con = stats.Constitution;
		combatant.D.Dex = stats.Dexterity;
		combatant.D.Int = stats.Intelligence;
		combatant.D.Wis = stats.Wisdom;
		combatant.D.Hit = (combatant.D.MeleeHit = num4);
		combatant.D.RangedHit = ReadDouble(definition, "rangedHit", num4);
		combatant.D.MeleeDamage = (flag2 ? ((double)L1jMobTemplateRules.PhysicalDamageBonus(stats)) : ReadDouble(definition, "db"));
		combatant.D.RangedDamage = ReadDouble(definition, "rangedDb", combatant.D.MeleeDamage);
		combatant.D.ArmorClass = (flag ? ((double)stats.ArmorClass) : ReadDouble(definition, "ac", 10.0));
		combatant.D.MagicResist = (flag ? ((double)stats.MagicResistance) : ReadDouble(definition, "mr"));
		combatant.D.DamageReduction = ReadDouble(definition, "dr");
		combatant.D.AttackDiceSmall = Math.Max(1, num3);
		combatant.D.AttackDiceLarge = Math.Max(1, val);
		combatant.D.UsesRangedAttack = !flag5 && mobBasicAttackProfile.UsesRangedPhysicalDamage;
		return combatant;
	}

	public static void RefreshPlayer(Combatant actor, IGameData data, bool restoreResources = false)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		CombatantKind kind = actor.Kind;
		if (kind != CombatantKind.Player && kind != CombatantKind.Ally && !HostilePlayerRules.IsHostilePlayer(actor))
		{
			throw new ArgumentException("Only players, allies and hostile player NPCs use player stat recomputation.", "actor");
		}
		double hp = actor.Hp;
		double mp = actor.Mp;
		Attributes attributes = Clone(actor.Base);
		AddAllocations(attributes, actor.Allocations);
		Attributes original = Clone(attributes);
		AddElixirBonuses(attributes, actor.ElixirBonuses);
		AddLevelStatBonuses(attributes, actor.LevelStatBonuses);
		double dex = attributes.Dex;
		List<(string Slot, string ItemKey, JsonObject Definition, ItemStack Instance)> list = new List<(string Slot, string ItemKey, JsonObject Definition, ItemStack Instance)>();
		string key;
		foreach (KeyValuePair<string, ItemStack> equippedItem in actor.EquippedItems)
		{
			equippedItem.Deconstruct(out key, out var value);
			string text = key;
			ItemStack itemStack = value;
			string itemKey = itemStack.ItemKey;
			JsonObject jsonObject = data.Item(itemKey);
			if (jsonObject != null && !PolymorphRules.IsSlotSuppressed(data, actor, text))
			{
				list.Add((text, itemKey, jsonObject, itemStack));
				AddAttributes(attributes, jsonObject);
			}
		}
		if (list.Count == 0)
		{
			foreach (KeyValuePair<string, object> item5 in actor.Equip)
			{
				item5.Deconstruct(out key, out var value2);
				string item = key;
				if (value2 is string text2)
				{
					JsonObject jsonObject2 = data.Item(text2);
					if (jsonObject2 != null)
					{
						list.Add((item, text2, jsonObject2, null));
						AddAttributes(attributes, jsonObject2);
					}
				}
			}
		}
		EquipmentAffixTotals totals = EquipmentAffixRules.Aggregate(from entry in list
			where entry.Instance != null
			select entry.Instance);
		EquipmentAffixRules.ApplyAttributes(attributes, totals);
		SkillBuffRules.ApplyAttributeBonuses(attributes, actor, data);
		SetRules.ApplyEarlyAttributes(data, actor, attributes);
		PolymorphRules.ApplyAttributeBonuses(data, actor, attributes);
		CapAttributes(attributes);
		actor.D = new DerivedStats
		{
			MeleeCriticalDamage = ((actor.ClassId == "dark") ? 100 : 50),
			RangedCriticalDamage = ((actor.ClassId == "dark") ? 100 : 50),
			MagicCriticalDamage = ((actor.ClassId == "dark") ? 100 : 50)
		};
		ApplyClassGrowth(actor, attributes, dex);
		ApplyAttributeGrowth(actor, attributes);
		ApplyOriginalStatBonuses(actor, original);
		ApplyHealthAndMana(actor, attributes);
		double attackSpeedPercent = 0.0;
		double moveSpeedPercent = 0.0;
		actor.MoveSpeed = DefaultPlayerMoveSpeed;
		actor.AttackElement = "none";
		foreach (var item6 in list)
		{
			string item2 = item6.Item1;
			JsonObject item3 = item6.Item3;
			ItemStack item4 = item6.Item4;
			bool mainWeapon = item2 == "wpn";
			bool offhandWeapon = item2 == "offwpn";
			ApplyEquipmentBonuses(actor, item3, mainWeapon, offhandWeapon);
			if (item4 != null)
			{
				EquipmentEnhancementRules.Apply(actor, item3, item4, mainWeapon, offhandWeapon);
			}
			attackSpeedPercent += ReadDouble(item3, "atkSpdPct");
			moveSpeedPercent += ReadDouble(item3, "moveSpeedPct");
		}
		EquipmentAffixRules.ApplyDerived(actor, totals, ref attackSpeedPercent, ref moveSpeedPercent);
		WearerElementRules.ApplyDerivedElement(data, actor);
		SkillBuffRules.ApplyDerivedBonuses(actor, data);
		BehaviorBuffRules.ApplyResistBonuses(actor);
		actor.D.MeleeDamage += WarriorPassiveRules.CrushMeleeDamageBonus(actor);
		CollectionRules.ApplyDerivedBonuses(actor);
		attackSpeedPercent += SetRules.ApplyDerivedBonuses(data, actor);
		SpellbladeRules.ApplyDerivedBonuses(data, actor);
		attackSpeedPercent += PolymorphRules.ApplyDerivedBonuses(data, actor);
		WeaponCombatProfile.ApplyBaseTimings(actor, data);
		AmmunitionRules.ApplyAttackDice(data, actor);
		PolymorphRules.ApplyTimingOverrides(data, actor);
		if (attackSpeedPercent != 0.0)
		{
			actor.D.AttackInterval /= Math.Max(0.01, 1.0 + attackSpeedPercent / 100.0);
		}
		if (moveSpeedPercent != 0.0)
		{
			actor.MoveSpeed *= Math.Max(0.01, 1.0 + moveSpeedPercent / 100.0);
		}
		WeightRules.Apply(data, actor);
		SkillBuffRules.ApplyResourceMultipliers(actor);
		BehaviorBuffRules.ApplyResourceMultipliers(actor);
		L1jCookingRules.ApplyDerived(actor);
		if (actor.HasStatus("guardian_soul") || CombatInventory.Count(actor, "item_guardian_soul") > 0)
		{
			actor.MaxHp += GameRateConfig.GuardianSoulHpBonus;
			actor.MaxMp += GameRateConfig.GuardianSoulMpBonus;
		}
		actor.AttackRange = CombatRangeRules.WeaponRange(actor.MainWeaponId, data);
		actor.D.Hit = (actor.D.UsesRangedAttack ? actor.D.RangedHit : actor.D.MeleeHit);
		actor.Hp = (restoreResources ? actor.MaxHp : Math.Clamp(hp, 0.0, actor.MaxHp));
		actor.Mp = (restoreResources ? actor.MaxMp : Math.Clamp(mp, 0.0, actor.MaxMp));
		actor.Dead = actor.Hp <= 0.0;
	}

	private static void ApplyClassGrowth(Combatant actor, Attributes stats, double baseDex)
	{
		DerivedStats d = actor.D;
		string classId = actor.ClassId;
		int level = actor.Level;
		int num = ClassGrowthRules.LevelHit(classId, level);
		d.MeleeHit += num;
		d.RangedHit += num;
		d.MeleeDamage += ClassGrowthRules.LevelMeleeDamage(classId, level);
		d.RangedDamage += ClassGrowthRules.LevelRangedDamage(classId, level);
		d.EvasionRating += ClassGrowthRules.LevelEvasion(classId, level) + ((int)Math.Floor(stats.Dex) - 8) / 2;
		d.ArmorClass = ClassGrowthRules.BaseArmorClass(classId, level, baseDex);
		d.MagicResist = ClassGrowthRules.BaseMagicResist(classId, level, stats.Wis);
	}

	private static void ApplyAttributeGrowth(Combatant actor, Attributes attributes)
	{
		DerivedStats d = actor.D;
		d.Str = attributes.Str;
		d.Dex = attributes.Dex;
		d.Con = attributes.Con;
		d.Int = attributes.Int;
		d.Wis = attributes.Wis;
		d.Cha = attributes.Cha;
		d.MeleeDamage += L1jAttackTables.StrDmg(attributes.Str);
		d.RangedDamage += L1jAttackTables.DexDmg(attributes.Dex);
		int num = L1jAttackTables.StrHit(attributes.Str) + L1jAttackTables.DexHit(attributes.Dex);
		d.MeleeHit += num;
		d.RangedHit += num;
		if (actor.ClassId == "dark")
		{
			d.MeleeCritical += 3.0;
			d.RangedCritical += 3.0;
		}
		d.IntelligenceSpellPower = Math.Max(0.0, Math.Floor(attributes.Int) - 12.0);
		int num2 = (int)Math.Floor(attributes.Con);
		d.HealthRegenMaximum = ((actor.Level <= 11 || num2 < 14) ? 1 : ((num2 > 25) ? 14 : (num2 - 12)));
		int num3 = (int)Math.Floor(attributes.Wis);
		d.ManaRegen = ((num3 >= 17) ? 3 : ((num3 < 15) ? 1 : 2));
	}

	private static void ApplyOriginalStatBonuses(Combatant actor, Attributes original)
	{
		DerivedStats d = actor.D;
		string classId = actor.ClassId;
		int originalStr = (int)Math.Floor(original.Str);
		int originalDex = (int)Math.Floor(original.Dex);
		int originalCon = (int)Math.Floor(original.Con);
		int originalInt = (int)Math.Floor(original.Int);
		int originalWis = (int)Math.Floor(original.Wis);
		d.MeleeHit += OriginalStatBonusRules.HitUp(classId, originalStr);
		d.RangedHit += OriginalStatBonusRules.BowHitUp(classId, originalDex);
		d.MeleeDamage += OriginalStatBonusRules.DmgUp(classId, originalStr);
		d.RangedDamage += OriginalStatBonusRules.BowDmgUp(classId, originalDex);
		d.ArmorClass -= OriginalStatBonusRules.Ac(classId, originalDex);
		d.EvasionRating += OriginalStatBonusRules.Er(classId, originalDex);
		d.MagicResist += OriginalStatBonusRules.Mr(classId, originalWis);
		d.OriginalMagicHit = OriginalStatBonusRules.MagicHit(classId, originalInt);
		d.OriginalMagicCritical = OriginalStatBonusRules.MagicCritical(classId, originalInt);
		d.OriginalMagicDamage = OriginalStatBonusRules.MagicDamage(classId, originalInt);
		d.OriginalManaCostReduction = OriginalStatBonusRules.MagicConsumeReduction(classId, originalInt);
		d.OriginalHealthRegen = OriginalStatBonusRules.Hpr(classId, originalCon);
		d.OriginalManaRegen = OriginalStatBonusRules.Mpr(classId, originalWis);
		d.OriginalWeightReduction = OriginalStatBonusRules.StrWeightReduction(classId, originalStr) + OriginalStatBonusRules.ConWeightReduction(classId, originalCon);
	}

	private static string GrowthIdentity(Combatant actor)
	{
		if (!string.IsNullOrWhiteSpace(actor.Disp))
		{
			return actor.Disp;
		}
		return actor.Key;
	}

	private static void ApplyHealthAndMana(Combatant actor, Attributes stats)
	{
		ClassGrowthRules.ClassGrowthProfile classGrowthProfile = ClassGrowthRules.Profile(actor.ClassId);
		actor.MaxHp = Math.Min(classGrowthProfile.MaxHpCap, (double)classGrowthProfile.InitHp + HealthGrowthRules.RolledHealth(GrowthIdentity(actor), actor.ClassId, actor.Level, stats.Con));
		actor.MaxMp = Math.Min(classGrowthProfile.MaxMpCap, (double)ClassGrowthRules.InitialMana(actor.ClassId, stats.Wis) + ManaGrowthRules.RolledMana(GrowthIdentity(actor), actor.ClassId, actor.Level, stats.Wis));
	}

	private static void ApplyEquipmentBonuses(Combatant actor, JsonObject item, bool mainWeapon, bool offhandWeapon)
	{
		DerivedStats d = actor.D;
		bool flag = mainWeapon && ReadBool(item, "ranged");
		if (mainWeapon)
		{
			d.AttackDiceSmall = Math.Max(1, ReadInt(item, "dmgS", 1));
			d.AttackDiceLarge = Math.Max(1, ReadInt(item, "dmgL", d.AttackDiceSmall));
			if (flag)
			{
				d.RangedDamage += ReadDouble(item, "dmgBonus");
				d.RangedHit += ReadDouble(item, "hit");
			}
			else
			{
				d.MeleeDamage += ReadDouble(item, "dmgBonus");
				d.MeleeHit += ReadDouble(item, "hit");
			}
			double num = ReadDouble(item, "lvDmgDiv");
			if (num > 0.0)
			{
				double num2 = Math.Floor((double)Math.Max(1, actor.Level) / num);
				if (flag)
				{
					d.RangedDamage += num2;
				}
				else
				{
					d.MeleeDamage += num2;
				}
			}
			double num3 = ReadDouble(item, "lvHitDiv");
			if (num3 > 0.0)
			{
				double num4 = Math.Floor((double)Math.Max(1, actor.Level) / num3);
				if (flag)
				{
					d.RangedHit += num4;
				}
				else
				{
					d.MeleeHit += num4;
				}
			}
			actor.AttackElement = ReadString(item, "e", actor.AttackElement);
		}
		else if (offhandWeapon)
		{
			d.MeleeDamage += ReadDouble(item, "dmgBonus");
			d.MeleeHit += ReadDouble(item, "hit");
			d.ArmorClass -= ReadDouble(item, "ac");
		}
		else
		{
			d.ArmorClass -= ReadDouble(item, "ac");
		}
		d.MagicResist += ReadDouble(item, "mr");
		d.DamageReduction += ReadDouble(item, "dr");
		d.EvasionRating += ReadDouble(item, "er");
		d.MeleeEvasion += ReadDouble(item, "meleeEvasion");
		d.MeleeDamage += ReadDouble(item, "meleeDmg");
		d.MeleeHit += ReadDouble(item, "meleeHit");
		d.RangedDamage += ReadDouble(item, "rangedDmg");
		d.RangedHit += ReadDouble(item, "rangedHit");
		d.MagicDamage += ReadDouble(item, "mdmg");
		d.MagicHit += ReadDouble(item, "magicHit");
		d.ExtraDamage += ReadDouble(item, "extraDmg");
		d.ExtraHit += ReadDouble(item, "extraHit");
		d.MeleeCritical += ReadDouble(item, "mcrit");
		d.RangedCritical += ReadDouble(item, "rcrit");
		d.MagicCritical += ReadDouble(item, "magicCrit");
		d.MeleeCriticalDamage += ReadDouble(item, "mcritDmg");
		d.RangedCriticalDamage += ReadDouble(item, "rcritDmg");
		d.MagicCriticalDamage += ReadDouble(item, "magicCritDmg");
		d.ResistFire += ReadDouble(item, "resFire");
		d.ResistWater += ReadDouble(item, "resWater");
		d.ResistWind += ReadDouble(item, "resWind");
		d.ResistEarth += ReadDouble(item, "resEarth");
		d.HealthRegenFlat += ReadDouble(item, "hpR");
		d.ManaRegen += ReadDouble(item, "mpR");
		d.HealthRegenIntervalReductionSeconds += ReadDouble(item, "hpRegenFaster");
		d.LowManaRegenBonus += ReadDouble(item, "lowMpRegenBonus");
		d.ItemSpellPower += ReadDouble(item, "extraMp");
		actor.MaxHp += ReadDouble(item, "mhp");
		actor.MaxMp += ReadDouble(item, "mmp");
	}

	private static void AddAllocations(Attributes target, IReadOnlyDictionary<string, int> allocations)
	{
		target.Str = Allocate(target.Str, allocations, "str");
		target.Dex = Allocate(target.Dex, allocations, "dex");
		target.Con = Allocate(target.Con, allocations, "con");
		target.Int = Allocate(target.Int, allocations, "int");
		target.Wis = Allocate(target.Wis, allocations, "wis");
		target.Cha = Allocate(target.Cha, allocations, "cha");
	}

	private static void AddElixirBonuses(Attributes target, IReadOnlyDictionary<string, int> bonuses)
	{
		target.Str = AddElixir(target.Str, bonuses, "str");
		target.Dex = AddElixir(target.Dex, bonuses, "dex");
		target.Con = AddElixir(target.Con, bonuses, "con");
		target.Int = AddElixir(target.Int, bonuses, "int");
		target.Wis = AddElixir(target.Wis, bonuses, "wis");
		target.Cha = AddElixir(target.Cha, bonuses, "cha");
	}

	private static void AddLevelStatBonuses(Attributes target, IReadOnlyDictionary<string, int> bonuses)
	{
		target.Str = AddPermanentBonus(target.Str, bonuses, "str");
		target.Dex = AddPermanentBonus(target.Dex, bonuses, "dex");
		target.Con = AddPermanentBonus(target.Con, bonuses, "con");
		target.Int = AddPermanentBonus(target.Int, bonuses, "int");
		target.Wis = AddPermanentBonus(target.Wis, bonuses, "wis");
		target.Cha = AddPermanentBonus(target.Cha, bonuses, "cha");
	}

	private static double AddPermanentBonus(double current, IReadOnlyDictionary<string, int> bonuses, string key)
	{
		return Math.Min(35.0, current + (double)Math.Max(0, bonuses.GetValueOrDefault(key)));
	}

	private static double AddElixir(double current, IReadOnlyDictionary<string, int> bonuses, string key)
	{
		return Math.Min(35.0, current + (double)Math.Max(0, bonuses.GetValueOrDefault(key)));
	}

	private static double Allocate(double current, IReadOnlyDictionary<string, int> allocations, string key)
	{
		return Math.Max(current, Math.Min(18.0, current + (double)Math.Max(0, allocations.GetValueOrDefault(key))));
	}

	private static void AddAttributes(Attributes target, JsonObject item)
	{
		target.Str += ReadDouble(item, "str");
		target.Dex += ReadDouble(item, "dex");
		target.Con += ReadDouble(item, "con");
		target.Int += ReadDouble(item, "int");
		target.Wis += ReadDouble(item, "wis");
		target.Cha += ReadDouble(item, "cha");
	}

	private static void CapAttributes(Attributes stats)
	{
		stats.Str = Math.Min(35.0, stats.Str);
		stats.Dex = Math.Min(35.0, stats.Dex);
		stats.Con = Math.Min(35.0, stats.Con);
		stats.Int = Math.Min(35.0, stats.Int);
		stats.Wis = Math.Min(35.0, stats.Wis);
		stats.Cha = Math.Min(35.0, stats.Cha);
	}

	private static Attributes Attr(double str, double dex, double con, double intelligence, double wis, double cha)
	{
		return new Attributes
		{
			Str = str,
			Dex = dex,
			Con = con,
			Int = intelligence,
			Wis = wis,
			Cha = cha
		};
	}

	private static Attributes Clone(Attributes source)
	{
		return Attr(source.Str, source.Dex, source.Con, source.Int, source.Wis, source.Cha);
	}

	private static bool ReadBool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static string ReadString(JsonObject source, string name, string fallback = "")
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrEmpty(value))
		{
			return fallback;
		}
		return value;
	}

	private static int ReadInt(JsonObject source, string name, int fallback = 0)
	{
		if (!TryReadDouble(source[name], out var value))
		{
			return fallback;
		}
		return (int)Math.Floor(value);
	}

	private static double ReadDouble(JsonObject source, string name, double fallback = 0.0)
	{
		if (!TryReadDouble(source[name], out var value))
		{
			return fallback;
		}
		return value;
	}

	private static int ReadArrayInt(JsonNode? node, int index, int fallback)
	{
		if (!(node is JsonArray jsonArray) || index < 0 || index >= jsonArray.Count || !TryReadDouble(jsonArray[index], out var value))
		{
			return fallback;
		}
		return (int)Math.Floor(value);
	}

	private static bool TryReadDouble(JsonNode? node, out double value)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<double>(out value) && double.IsFinite(value))
		{
			return true;
		}
		value = 0.0;
		return false;
	}
}
