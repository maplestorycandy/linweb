using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PolymorphRules
{
	public const string BuffKey = "poly";

	public const string ScrollEffect = "poly";

	public const string ControlRingItemKey = "acc_117";

	public const string RaccoonLeafItemKey = "relic_raccoon_leaf";

	public const string OrcEmissaryFormName = "妖魔密使";

	private static readonly PolymorphForm OrcEmissaryForm = new PolymorphForm("妖魔密使", 1, "text-yellow-400", ControlOnly: true, KeepClassAppearance: false, ClassMorph: false, Shanna: false, TrueShanna: false, 26.0, null, null, null, 26.0, 13.0, 16.0, null, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 6984, 0, 4095, 4095, 26.0, null);

	private static readonly IReadOnlyDictionary<WeaponFamily, int> WeaponBits = new Dictionary<WeaponFamily, int>
	{
		[WeaponFamily.Dagger] = 1,
		[WeaponFamily.OneHandSword] = 2,
		[WeaponFamily.TwoHandSword] = 4,
		[WeaponFamily.OneHandBlunt] = 8,
		[WeaponFamily.TwoHandBlunt] = 8,
		[WeaponFamily.DualAxes] = 8,
		[WeaponFamily.OneHandSpear] = 16,
		[WeaponFamily.TwoHandSpear] = 16,
		[WeaponFamily.ChainSword] = 16,
		[WeaponFamily.Wand] = 32,
		[WeaponFamily.DualBlades] = 64,
		[WeaponFamily.Claw] = 128,
		[WeaponFamily.Kiringku] = 128,
		[WeaponFamily.Bow] = 256,
		[WeaponFamily.Crossbow] = 256
	};

	private static readonly IReadOnlyDictionary<string, int> ArmorBits = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		["helm"] = 1,
		["amulet"] = 2,
		["ear1"] = 4,
		["ear2"] = 4,
		["tshirt"] = 8,
		["armor"] = 16,
		["cloak"] = 32,
		["belt"] = 64,
		["shield"] = 128,
		["gloves"] = 256,
		["ring1"] = 512,
		["ring2"] = 512,
		["ring3"] = 512,
		["ring4"] = 512,
		["boots"] = 1024
	};

	private static readonly IReadOnlyDictionary<WeaponFamily, string> FamilyNames = new Dictionary<WeaponFamily, string>
	{
		[WeaponFamily.OneHandSword] = "單手劍",
		[WeaponFamily.OneHandBlunt] = "單手鈍器",
		[WeaponFamily.Bow] = "弓",
		[WeaponFamily.Crossbow] = "十字弓",
		[WeaponFamily.OneHandSpear] = "單手矛",
		[WeaponFamily.TwoHandSpear] = "雙手矛",
		[WeaponFamily.Wand] = "魔杖",
		[WeaponFamily.Dagger] = "匕首",
		[WeaponFamily.TwoHandSword] = "雙手劍",
		[WeaponFamily.TwoHandBlunt] = "雙手鈍器",
		[WeaponFamily.DualBlades] = "雙刀",
		[WeaponFamily.Claw] = "鋼爪",
		[WeaponFamily.ChainSword] = "鎖鏈劍",
		[WeaponFamily.DualAxes] = "雙斧",
		[WeaponFamily.Kiringku] = "奇古獸"
	};

	private const int AllWeapons = 4095;

	private const int AllArmor = 4095;

	private static IReadOnlyList<PolymorphForm>? _cachedAllForms;
	private static Dictionary<string, PolymorphForm>? _cachedFormsByName;
	private static readonly object _cacheLock = new object();

	private static void EnsureFormsCache(IGameData data)
	{
		if (_cachedAllForms != null && _cachedFormsByName != null)
		{
			return;
		}
		lock (_cacheLock)
		{
			if (_cachedAllForms != null && _cachedFormsByName != null)
			{
				return;
			}
			List<PolymorphForm> list = new List<PolymorphForm>();
			Dictionary<string, PolymorphForm> byName = new Dictionary<string, PolymorphForm>(StringComparer.Ordinal);
			if (data.Table("POLY_TIERS") is JsonArray jsonArray)
			{
				foreach (JsonNode item in jsonArray)
				{
					if (!(item is JsonObject jsonObject))
					{
						continue;
					}
					string color = Str(jsonObject, "color");
					if (!(jsonObject["forms"] is JsonArray jsonArray2))
					{
						continue;
					}
					foreach (JsonNode item2 in jsonArray2)
					{
						if (item2 is JsonObject form)
						{
							PolymorphForm parsed = Parse(form, color);
							list.Add(parsed);
							if (!string.IsNullOrEmpty(parsed.Name))
							{
								byName[parsed.Name] = parsed;
							}
						}
					}
				}
			}
			if (data.Table("CONTROL_ONLY_POLY_FORMS") is JsonArray jsonArray3)
			{
				foreach (JsonNode item3 in jsonArray3)
				{
					if (item3 is JsonObject jsonObject2)
					{
						PolymorphForm parsed2 = Parse(jsonObject2, Str(jsonObject2, "c", "text-yellow-400"));
						list.Add(parsed2);
						if (!string.IsNullOrEmpty(parsed2.Name))
						{
							byName[parsed2.Name] = parsed2;
						}
					}
				}
			}
			_cachedAllForms = list;
			_cachedFormsByName = byName;
		}
	}

	public static void InvalidateCache()
	{
		lock (_cacheLock)
		{
			_cachedAllForms = null;
			_cachedFormsByName = null;
		}
	}

	public static IReadOnlyList<PolymorphForm> AllForms(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		EnsureFormsCache(data);
		return _cachedAllForms!;
	}

	public static PolymorphForm? Find(IGameData data, string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		if (string.Equals(name, "妖魔密使", StringComparison.Ordinal))
		{
			return OrcEmissaryForm;
		}
		EnsureFormsCache(data);
		return _cachedFormsByName!.TryGetValue(name, out PolymorphForm? form) ? form : null;
	}

	private static PolymorphForm Parse(JsonObject form, string color)
	{
		return new PolymorphForm(Str(form, "n"), (int)(Num(form, "lv") ?? 1.0), color, Bool(form, "controlOnly"), Bool(form, "keepClassAppearance"), Bool(form, "classMorph"), Bool(form, "shanna"), Bool(form, "trueShanna"), Num(form, "atk"), Num(form, "atkApm"), Num(form, "castApm"), Num(form, "supportCastApm"), Num(form, "cast"), Num(form, "stun"), Num(form, "wlk"), ParseApmTable(form), Num(form, "md").GetValueOrDefault(), Num(form, "mh").GetValueOrDefault(), Num(form, "rd").GetValueOrDefault(), Num(form, "rh").GetValueOrDefault(), Num(form, "ed").GetValueOrDefault(), Num(form, "eh").GetValueOrDefault(), Num(form, "mgd").GetValueOrDefault(), Num(form, "sp").GetValueOrDefault(), Num(form, "mpr").GetValueOrDefault(), Num(form, "ac").GetValueOrDefault(), Num(form, "er").GetValueOrDefault(), Num(form, "mr").GetValueOrDefault(), (int)Num(form, "gfx").GetValueOrDefault(), (int)Num(form, "minLv").GetValueOrDefault(), (int)(Num(form, "wep") ?? 4095.0), (int)(Num(form, "arm") ?? 4095.0), Num(form, "castNoDir"), ParseFrameTable(form, "atkW"));
	}

	private static IReadOnlyDictionary<string, double>? ParseApmTable(JsonObject form)
	{
		return ParseFrameTable(form, "apm");
	}

	private static IReadOnlyDictionary<string, double>? ParseFrameTable(JsonObject form, string key)
	{
		if (!(form[key] is JsonObject jsonObject))
		{
			return null;
		}
		Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.Ordinal);
		foreach (var (key2, jsonNode2) in jsonObject)
		{
			if (jsonNode2 is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var value))
			{
				dictionary[key2] = value;
			}
		}
		if (dictionary.Count <= 0)
		{
			return null;
		}
		return dictionary;
	}

	public static bool HasRangedPolyWeapon(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (owner.EquippedItems.TryGetValue("wpn", out ItemStack value) && value != null)
		{
			JsonObject jsonObject = data.Item(value.ItemKey);
			if (jsonObject != null)
			{
				if (!Bool(jsonObject, "ranged"))
				{
					return Bool(jsonObject, "isBow");
				}
				return true;
			}
		}
		return false;
	}

	public static bool AllowsWeapon(PolymorphForm form, WeaponFamily? family)
	{
		ArgumentNullException.ThrowIfNull(form, "form");
		if (form.KeepClassAppearance || form.ClassMorph)
		{
			return true;
		}
		if (family.HasValue)
		{
			WeaponFamily valueOrDefault = family.GetValueOrDefault();
			if (WeaponBits.TryGetValue(valueOrDefault, out var value))
			{
				return (form.WeaponMask & value) != 0;
			}
			return true;
		}
		return true;
	}

	public static bool MatchesWeapon(IGameData data, Combatant owner, PolymorphForm form)
	{
		ArgumentNullException.ThrowIfNull(form, "form");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return AllowsWeapon(form, WeaponCombatProfile.ResolveFamily(owner.MainWeaponId, data));
	}

	public static bool IsSlotSuppressed(IGameData data, Combatant owner, string slot)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (string.IsNullOrEmpty(slot))
		{
			return false;
		}
		PolymorphForm polymorphForm = CurrentForm(data, owner);
		if ((object)polymorphForm == null)
		{
			return false;
		}
		if (polymorphForm.KeepClassAppearance || polymorphForm.ClassMorph)
		{
			return false;
		}
		ItemStack value;
		if ((slot == "wpn" || slot == "offwpn") ? true : false)
		{
			return !AllowsWeapon(polymorphForm, WeaponCombatProfile.ResolveFamily((owner.EquippedItems.TryGetValue(slot, out value) && value != null) ? value.ItemKey : "", data));
		}
		if (ArmorBits.TryGetValue(slot, out var value2))
		{
			return (polymorphForm.ArmorMask & value2) == 0;
		}
		return false;
	}

	public static IReadOnlyList<string> SuppressedSlots(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if ((object)CurrentForm(data, owner) == null)
		{
			return Array.Empty<string>();
		}
		return owner.EquippedItems.Keys.Where((string slot) => IsSlotSuppressed(data, owner, slot)).OrderBy<string, string>((string slot) => slot, StringComparer.Ordinal).ToList();
	}

	public static bool HasControlItem(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (!owner.EquippedItems.Values.Any((ItemStack item) => item != null && (item.ItemKey == "relic_raccoon_leaf" || item.ItemKey == "acc_117")))
		{
			return owner.InventoryStacks.Any((ItemStack item) => item.ItemKey == "acc_117" && item.Quantity > 0);
		}
		return true;
	}

	public static bool IsShannaScroll(IGameData data, string itemKey, out int level)
	{
		level = 0;
		if (string.IsNullOrEmpty(itemKey)) return false;
		JsonObject? it = data.Item(itemKey);
		string name = it?["n"]?.GetValue<string>() ?? "";
		if (!name.Contains("夏納") && !itemKey.StartsWith("l1j_item_4915", StringComparison.Ordinal) && itemKey != "l1j_item_49149")
		{
			return false;
		}
		if (name.Contains("85") || itemKey == "l1j_item_49159") level = 85;
		else if (name.Contains("80") || itemKey == "l1j_item_49157") level = 80;
		else if (name.Contains("75") || itemKey == "l1j_item_49156") level = 75;
		else if (name.Contains("70") || itemKey == "l1j_item_49155") level = 70;
		else if (name.Contains("65") || itemKey == "l1j_item_49154") level = 65;
		else if (name.Contains("60") || itemKey == "l1j_item_49153") level = 60;
		else if (name.Contains("55") || itemKey == "l1j_item_49152") level = 55;
		else if (name.Contains("52") || itemKey == "l1j_item_49151") level = 52;
		else if (name.Contains("40") || itemKey == "l1j_item_49150") level = 40;
		else if (name.Contains("30") || itemKey == "l1j_item_49149") level = 30;
		else level = 70;
		return true;
	}

	public static IReadOnlyList<PolymorphForm> RandomCandidates(IGameData data, Combatant owner, int? maxLevelOverride = null)
	{
		int effectiveLevel = Math.Max(owner.Level, maxLevelOverride.GetValueOrDefault());
		List<PolymorphForm> list = (from form in AllForms(data)
			where !form.ControlOnly
			where MatchesWeapon(data, owner, form)
			select form).ToList();
		List<PolymorphForm> list2 = list.Where((PolymorphForm form) => form.RequiredLevel <= Math.Max(1, effectiveLevel)).ToList();
		if (list2.Count > 0)
		{
			return list2;
		}
		if (list.Count == 0)
		{
			return list;
		}
		int firstLevel = list.Min((PolymorphForm form) => form.RequiredLevel);
		return list.Where((PolymorphForm form) => form.RequiredLevel == firstLevel).ToList();
	}

	public static IReadOnlyList<PolymorphForm> SelectableForms(IGameData data, Combatant owner, int? maxLevelOverride = null)
	{
		int effectiveLevel = Math.Max(owner.Level, maxLevelOverride.GetValueOrDefault());
		bool isShanna = maxLevelOverride.HasValue;
		return (from form in AllForms(data)
			where effectiveLevel >= form.RequiredLevel
			where !form.ControlOnly || HasControlItem(owner) || (isShanna && (form.Shanna || form.TrueShanna))
			where MatchesWeapon(data, owner, form)
			orderby form.RequiredLevel descending, form.Atk.GetValueOrDefault()
			select form).ThenBy<PolymorphForm, string>((PolymorphForm form) => form.Name, StringComparer.Ordinal).ToList();
	}

	public static PolymorphResult TryUseScroll(IGameData data, Combatant owner, string scrollUid, ICombatRandom random, string? requestedForm = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(random, "random");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == scrollUid);
		if (itemStack == null)
		{
			return PolymorphResult.Fail(PolymorphFailure.ItemNotFound);
		}
		JsonObject jsonObject = data.Item(itemStack.ItemKey);
		bool isShanna = IsShannaScroll(data, itemStack.ItemKey, out int shannaLevel);
		if (jsonObject == null || (Str(jsonObject, "eff") != "poly" && !isShanna))
		{
			return PolymorphResult.Fail(PolymorphFailure.NotAPolymorphScroll);
		}
		if (itemStack.Locked)
		{
			return PolymorphResult.Fail(PolymorphFailure.ItemLocked);
		}
		double value = Num(jsonObject, "dur") ?? 1800.0;
		int effectiveLevel = isShanna ? Math.Max(owner.Level, shannaLevel) : owner.Level;
		PolymorphForm polymorphForm2;
		if (requestedForm != null)
		{
			if (!HasControlItem(owner) && !isShanna)
			{
				return PolymorphResult.Fail(PolymorphFailure.RequiresControlItem);
			}
			PolymorphForm polymorphForm = Find(data, requestedForm);
			if ((object)polymorphForm == null)
			{
				return PolymorphResult.Fail(PolymorphFailure.FormNotFound);
			}
			if (effectiveLevel < polymorphForm.RequiredLevel)
			{
				return PolymorphResult.Fail(PolymorphFailure.LevelTooLow);
			}
			if (!MatchesWeapon(data, owner, polymorphForm))
			{
				return PolymorphResult.Fail(PolymorphFailure.WeaponMismatch);
			}
			polymorphForm2 = polymorphForm;
		}
		else
		{
			IReadOnlyList<PolymorphForm> readOnlyList = RandomCandidates(data, owner, isShanna ? shannaLevel : null);
			if (readOnlyList.Count == 0)
			{
				return PolymorphResult.Fail(PolymorphFailure.NoCandidates);
			}
			polymorphForm2 = readOnlyList[Math.Clamp((int)(random.NextDouble() * (double)readOnlyList.Count), 0, readOnlyList.Count - 1)];
		}
		owner.PolymorphForm = polymorphForm2.Name;
		owner.Buffs["poly"] = value;
		itemStack.Quantity--;
		if (itemStack.Quantity <= 0)
		{
			owner.InventoryStacks.Remove(itemStack);
		}
		CombatInventory.SyncLegacyView(owner);
		return new PolymorphResult(Success: true, polymorphForm2.Name, PolymorphFailure.None);
	}

	public static PolymorphForm? ForcedForm(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		string text = SetRules.MorphId(data, owner);
		if (text.Length <= 0)
		{
			return null;
		}
		return FindSetPolyForm(data, text);
	}

	private static PolymorphForm? FindSetPolyForm(IGameData data, string formName)
	{
		if (!(data.Table("SET_POLY_FORMS") is JsonObject jsonObject))
		{
			return null;
		}
		foreach (KeyValuePair<string, JsonNode> item in jsonObject)
		{
			item.Deconstruct(out var _, out var value);
			if (value is JsonObject jsonObject2 && Str(jsonObject2, "n") == formName)
			{
				return Parse(jsonObject2, Str(jsonObject2, "c", "text-yellow-400"));
			}
		}
		return null;
	}

	public static PolymorphForm? CurrentForm(IGameData data, Combatant owner)
	{
		return ForcedForm(data, owner) ?? ActiveForm(data, owner);
	}

	public static PolymorphForm? ActiveForm(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (owner.Buffs.GetValueOrDefault("poly") <= 0.0)
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(owner.PolymorphForm))
		{
			return null;
		}
		return Find(data, owner.PolymorphForm);
	}

	public static void ApplyTimingOverrides(IGameData data, Combatant owner)
	{
		PolymorphForm polymorphForm = CurrentForm(data, owner);
		owner.PolymorphGait = 16.0;
		if ((object)polymorphForm == null)
		{
			return;
		}
		DerivedStats d = owner.D;
		double? atkApm = polymorphForm.AtkApm;
		if (atkApm.HasValue)
		{
			double valueOrDefault = atkApm.GetValueOrDefault();
			d.AttackInterval = 60.0 / Math.Max(1.0, valueOrDefault);
			atkApm = polymorphForm.CastApm;
			if (atkApm.HasValue)
			{
				double valueOrDefault2 = atkApm.GetValueOrDefault();
				d.CastLockTicks = RoundTicks(600.0 / Math.Max(1.0, valueOrDefault2));
			}
			else
			{
				atkApm = polymorphForm.Cast;
				if (atkApm.HasValue)
				{
					double valueOrDefault3 = atkApm.GetValueOrDefault();
					d.CastLockTicks = RoundTicks(valueOrDefault3);
				}
			}
			atkApm = polymorphForm.Stun;
			if (atkApm.HasValue)
			{
				double valueOrDefault4 = atkApm.GetValueOrDefault();
				d.HitstunTicks = RoundTicks(valueOrDefault4);
			}
		}
		else
		{
			if (polymorphForm.Shanna || polymorphForm.TrueShanna)
			{
				var (num, num2, polymorphGait) = ShannaSpeed(data, owner, polymorphForm.TrueShanna);
				if (num.HasValue)
				{
					double valueOrDefault5 = num.GetValueOrDefault();
					d.AttackInterval = 60.0 / Math.Max(1.0, valueOrDefault5);
				}
				if (num2.HasValue)
				{
					double valueOrDefault6 = num2.GetValueOrDefault();
					d.HitstunTicks = RoundTicks(valueOrDefault6);
				}
				d.CastLockTicks = RoundTicks(600.0 / Math.Max(1.0, polymorphForm.CastApm ?? 80.0));
				owner.PolymorphGait = polymorphGait;
				return;
			}
			IReadOnlyDictionary<string, double> apmByFamily = polymorphForm.ApmByFamily;
			if (apmByFamily != null)
			{
				WeaponFamily? weaponFamily = WeaponCombatProfile.ResolveFamily(owner.MainWeaponId, data);
				object obj;
				if (weaponFamily.HasValue)
				{
					WeaponFamily valueOrDefault7 = weaponFamily.GetValueOrDefault();
					obj = FamilyNames.GetValueOrDefault(valueOrDefault7) ?? "";
				}
				else
				{
					obj = "";
				}
				string text = (string)obj;
				double num3 = ((text.Length > 0) ? apmByFamily.GetValueOrDefault(text) : 0.0);
				if (num3 <= 0.0)
				{
					num3 = apmByFamily.GetValueOrDefault("單手劍", 60.0);
				}
				d.AttackInterval = Math.Round(6000.0 / Math.Max(1.0, num3), MidpointRounding.AwayFromZero) / 100.0;
				atkApm = polymorphForm.Cast;
				if (atkApm.HasValue)
				{
					double valueOrDefault8 = atkApm.GetValueOrDefault();
					d.CastLockTicks = RoundTicks(valueOrDefault8);
				}
				atkApm = polymorphForm.Stun;
				if (atkApm.HasValue)
				{
					double valueOrDefault9 = atkApm.GetValueOrDefault();
					d.HitstunTicks = RoundTicks(valueOrDefault9);
				}
			}
			else
			{
				atkApm = AttackFrames(data, owner, polymorphForm);
				if (atkApm.HasValue)
				{
					double valueOrDefault10 = atkApm.GetValueOrDefault();
					d.AttackInterval = Math.Round(6000.0 / ActionFrameRules.AttacksPerMinuteForFrames(valueOrDefault10), MidpointRounding.AwayFromZero) / 100.0;
					atkApm = polymorphForm.Cast;
					if (atkApm.HasValue)
					{
						double valueOrDefault11 = atkApm.GetValueOrDefault();
						d.CastLockTicks = RoundTicks(valueOrDefault11);
					}
					atkApm = polymorphForm.Stun;
					if (atkApm.HasValue)
					{
						double valueOrDefault12 = atkApm.GetValueOrDefault();
						d.HitstunTicks = RoundTicks(valueOrDefault12);
					}
				}
				else
				{
					atkApm = polymorphForm.Cast;
					if (atkApm.HasValue)
					{
						double valueOrDefault13 = atkApm.GetValueOrDefault();
						d.CastLockTicks = RoundTicks(valueOrDefault13);
					}
					atkApm = polymorphForm.Stun;
					if (atkApm.HasValue)
					{
						double valueOrDefault14 = atkApm.GetValueOrDefault();
						d.HitstunTicks = RoundTicks(valueOrDefault14);
					}
				}
			}
		}
		atkApm = polymorphForm.Wlk;
		if (atkApm.HasValue)
		{
			double valueOrDefault15 = atkApm.GetValueOrDefault();
			if (valueOrDefault15 > 0.0)
			{
				owner.PolymorphGait = valueOrDefault15;
			}
		}
	}

	public static double? AttackFrames(IGameData data, Combatant owner, PolymorphForm form)
	{
		ArgumentNullException.ThrowIfNull(form, "form");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		IReadOnlyDictionary<string, double> atkByWeapon = form.AtkByWeapon;
		if (atkByWeapon == null)
		{
			return form.Atk;
		}
		WeaponFamily? weaponFamily = DualWieldCombatRules.ResolveAttackFamily(owner, data, WeaponCombatProfile.ResolveFamily(owner.MainWeaponId, data));
		if (weaponFamily.HasValue)
		{
			WeaponFamily valueOrDefault = weaponFamily.GetValueOrDefault();
			if (AllowsWeapon(form, valueOrDefault))
			{
				string valueOrDefault2 = FamilyNames.GetValueOrDefault(valueOrDefault);
				if (valueOrDefault2 == null || valueOrDefault2.Length <= 0 || !atkByWeapon.TryGetValue(valueOrDefault2, out var value))
				{
					return form.Atk;
				}
				return value;
			}
		}
		return form.Atk;
	}

	private static int RoundTicks(double value)
	{
		return (int)Math.Round(value, MidpointRounding.AwayFromZero);
	}

	public static void ApplyAttributeBonuses(IGameData data, Combatant owner, Attributes attributes)
	{
		ArgumentNullException.ThrowIfNull(attributes, "attributes");
		if ((object)CurrentForm(data, owner) == null)
		{
			return;
		}
		double num = 0.0;
		foreach (ItemStack value in owner.EquippedItems.Values)
		{
			if (value != null)
			{
				JsonObject jsonObject = data.Item(value.ItemKey);
				if (jsonObject != null)
				{
					num += Num(jsonObject, "polyAllStats").GetValueOrDefault();
				}
			}
		}
		if (num != 0.0)
		{
			attributes.Str += num;
			attributes.Dex += num;
			attributes.Con += num;
			attributes.Int += num;
			attributes.Wis += num;
			attributes.Cha += num;
		}
	}

	public static double ApplyDerivedBonuses(IGameData data, Combatant owner)
	{
		PolymorphForm polymorphForm = CurrentForm(data, owner);
		if ((object)polymorphForm == null)
		{
			return 0.0;
		}
		DerivedStats d = owner.D;
		d.MeleeDamage += polymorphForm.Md;
		d.MeleeHit += polymorphForm.Mh;
		d.RangedDamage += polymorphForm.Rd;
		d.RangedHit += polymorphForm.Rh;
		d.ExtraDamage += polymorphForm.Ed;
		d.ExtraHit += polymorphForm.Eh;
		d.MagicDamage += polymorphForm.Mgd;
		d.ManaRegen += polymorphForm.Mpr;
		d.ArmorClass += polymorphForm.Ac;
		d.EvasionRating += polymorphForm.Er;
		d.MagicResist += polymorphForm.Mr;
		owner.MaxMp += polymorphForm.Sp;
		double num = 0.0;
		foreach (ItemStack value in owner.EquippedItems.Values)
		{
			if (value != null)
			{
				JsonObject jsonObject = data.Item(value.ItemKey);
				if (jsonObject != null)
				{
					num += Num(jsonObject, "polyAtkSpdPct").GetValueOrDefault();
				}
			}
		}
		return num;
	}

	private static (double? Apm, double? Hitstun, double Wlk) ShannaSpeed(IGameData data, Combatant owner, bool trueShanna)
	{
		string avatar = owner.Avatar;
		string value;
		string propertyName = (((data.Table("SHANNA_PROFILE_BY_AVATAR") as JsonObject)?[avatar] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? (value ?? "full") : "full");
		JsonObject jsonObject = ((data.Table("SHANNA_APM_PROFILES") as JsonObject)?[propertyName] as JsonObject) ?? ((data.Table("SHANNA_APM_PROFILES") as JsonObject)?["full"] as JsonObject);
		string text = null;
		WeaponFamily? weaponFamily = DualWieldCombatRules.ResolveAttackFamily(owner, data, WeaponCombatProfile.ResolveFamily(owner.MainWeaponId, data));
		if (weaponFamily.HasValue)
		{
			WeaponFamily valueOrDefault = weaponFamily.GetValueOrDefault();
			text = FamilyNames.GetValueOrDefault(valueOrDefault);
		}
		double? item = null;
		if (trueShanna)
		{
			if (data.Table("TRUE_SHANNA_APM") is JsonObject trueApmObj)
			{
				string key = text ?? "單手劍";
				if (trueApmObj[key] is JsonValue jv && jv.TryGetValue<double>(out var v))
				{
					item = v;
				}
				else if (trueApmObj["單手劍"] is JsonValue jvFallback && jvFallback.TryGetValue<double>(out var vFallback))
				{
					item = vFallback;
				}
			}
		}
		else
		{
			if (text != null && jsonObject?[text] is JsonValue jsonValue2 && jsonValue2.TryGetValue<double>(out var value2))
			{
				item = value2;
			}
			else if (data.Table("SHANNA_APM_PROFILES") is JsonObject allProfiles && allProfiles["full"] is JsonObject fullProfile)
			{
				string key = text ?? "單手劍";
				if (fullProfile[key] is JsonValue jv && jv.TryGetValue<double>(out var v))
				{
					item = v;
				}
				else if (fullProfile["單手劍"] is JsonValue jvFallback && jvFallback.TryGetValue<double>(out var vFallback))
				{
					item = vFallback;
				}
			}
		}
		bool flag = text == "匕首" && data.Table("SHANNA_DAGGER_LONG_HITSTUN") is JsonArray source && source.Any((JsonNode node) => node is JsonValue jsonValue4 && jsonValue4.TryGetValue<string>(out string value4) && value4 == avatar);
		double? item2 = ((!item.HasValue) ? ((double?)null) : new double?((!trueShanna) ? (flag ? 4.2 : 3.3) : (flag ? 2.6 : 2.1)));
		double item3 = trueShanna ? 11.0 : 12.0;
		return (Apm: item, Hitstun: item2, Wlk: item3);
	}

	private static string Str(JsonObject source, string name, string fallback = "")
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return fallback;
		}
		return value ?? fallback;
	}

	private static bool Bool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static double? Num(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return null;
		}
		return value;
	}
}
