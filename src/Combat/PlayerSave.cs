using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleLineage.Core;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PlayerSave
{
	private sealed class PlayerSaveData
	{
		public int Version { get; set; }

		public string Key { get; set; } = "";

		public string DisplayName { get; set; } = "";

		public string Title { get; set; } = "";

		public string ClassId { get; set; } = "";

		public string Avatar { get; set; } = "";

		public int Level { get; set; }

		public double Experience { get; set; }

		public long Gold { get; set; }

		public double Alignment { get; set; }

		public double Satiety { get; set; }

		public double Hp { get; set; }

		public double Mp { get; set; }

		public long ItemUidSequence { get; set; }

		public SortedDictionary<string, int> Allocations { get; set; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public SortedDictionary<string, int> LevelStatBonuses { get; set; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public SortedDictionary<string, int> ElixirBonuses { get; set; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public int ElixirStatus { get; set; }

		public int UnspentElixirStatPoints { get; set; }

		public SortedDictionary<string, ItemStackSaveData>? EquippedItems { get; set; }

		public ItemStackSaveData[]? InventoryStacks { get; set; }

		public string[] LearnedSkills { get; set; } = Array.Empty<string>();

		public string[] GrantedSkills { get; set; } = Array.Empty<string>();

		public string ElfElement { get; set; } = "";

		public string PolymorphForm { get; set; } = "";

		public CharacterProgressSaveData? Progress { get; set; }

		public SortedDictionary<string, BuffSaveData>? Buffs { get; set; }
	}

	private sealed class BuffSaveData
	{
		public double Seconds { get; set; }

		public bool Permanent { get; set; }

		public static BuffSaveData Capture(double durationSeconds)
		{
			if (!double.IsPositiveInfinity(durationSeconds))
			{
				return new BuffSaveData
				{
					Seconds = durationSeconds
				};
			}
			return new BuffSaveData
			{
				Permanent = true
			};
		}

		public double Restore()
		{
			if (!Permanent)
			{
				return Seconds;
			}
			return double.PositiveInfinity;
		}

		public void Validate(string buffName)
		{
			if (string.IsNullOrWhiteSpace(buffName) || buffName.StartsWith("_", StringComparison.Ordinal))
			{
				throw new InvalidDataException("Player save contains invalid persistent buff '" + buffName + "'.");
			}
			if (Permanent)
			{
				if (Seconds != 0.0)
				{
					throw new InvalidDataException("Permanent player buff '" + buffName + "' cannot also have a duration.");
				}
			}
			else if (!double.IsFinite(Seconds) || Seconds <= 0.0)
			{
				throw new InvalidDataException("Player buff '" + buffName + "' must have a positive finite duration.");
			}
		}
	}

	private sealed class CharacterProgressSaveData
	{
		public long ItemGainAttemptSequence { get; set; }

		public SortedDictionary<string, int> QuestSteps { get; set; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public SortedDictionary<string, int> QuestKillCounts { get; set; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public string[] QuestFlags { get; set; } = Array.Empty<string>();

		public string[] DefeatedBossKeys { get; set; } = Array.Empty<string>();

		public long TeleportMemorySequence { get; set; }

		public TeleportMemorySaveData[] TeleportMemories { get; set; } = Array.Empty<TeleportMemorySaveData>();

		public static CharacterProgressSaveData Capture(CharacterProgress progress)
		{
			return new CharacterProgressSaveData
			{
				ItemGainAttemptSequence = progress.ItemGainAttemptSequence,
				QuestSteps = new SortedDictionary<string, int>(progress.QuestSteps, StringComparer.Ordinal),
				QuestKillCounts = new SortedDictionary<string, int>(progress.QuestKillCounts, StringComparer.Ordinal),
				QuestFlags = progress.QuestFlags.Order<string>(StringComparer.Ordinal).ToArray(),
				DefeatedBossKeys = progress.DefeatedBossKeys.Order<string>(StringComparer.Ordinal).ToArray(),
				TeleportMemorySequence = progress.TeleportMemorySequence,
				TeleportMemories = progress.TeleportMemories.Select(TeleportMemorySaveData.Capture).ToArray()
			};
		}

		public CharacterProgress Restore()
		{
			return new CharacterProgress
			{
				ItemGainAttemptSequence = ItemGainAttemptSequence,
				QuestSteps = new Dictionary<string, int>(QuestSteps, StringComparer.Ordinal),
				QuestKillCounts = new Dictionary<string, int>(QuestKillCounts, StringComparer.Ordinal),
				QuestFlags = new HashSet<string>(QuestFlags, StringComparer.Ordinal),
				DefeatedBossKeys = new HashSet<string>(DefeatedBossKeys, StringComparer.Ordinal),
				TeleportMemorySequence = TeleportMemorySequence,
				TeleportMemories = TeleportMemories.Select((TeleportMemorySaveData location) => location.Restore()).ToList()
			};
		}

		public void Validate()
		{
			if (QuestSteps == null || QuestKillCounts == null || QuestFlags == null || DefeatedBossKeys == null || TeleportMemories == null)
			{
				throw new InvalidDataException("Character progress flags cannot be null.");
			}
			CharacterProgress characterProgress = Restore();
			characterProgress.Validate();
			if (characterProgress.QuestFlags.Count != QuestFlags.Length || characterProgress.DefeatedBossKeys.Count != DefeatedBossKeys.Length)
			{
				throw new InvalidDataException("Character progress flags cannot contain duplicates.");
			}
		}
	}

	private sealed class TeleportMemorySaveData
	{
		public string Id { get; set; } = "";

		public string Name { get; set; } = "";

		public string MapKey { get; set; } = "";

		public double WorldX { get; set; }

		public double WorldY { get; set; }

		public static TeleportMemorySaveData Capture(TeleportMemoryLocation location)
		{
			return new TeleportMemorySaveData
			{
				Id = location.Id,
				Name = location.Name,
				MapKey = location.MapKey,
				WorldX = location.WorldX,
				WorldY = location.WorldY
			};
		}

		public TeleportMemoryLocation Restore()
		{
			return new TeleportMemoryLocation(Id, Name, MapKey, WorldX, WorldY);
		}
	}

	private sealed class ItemStackSaveData
	{
		public string Uid { get; set; } = "";

		public string ItemKey { get; set; } = "";

		public long Quantity { get; set; }

		public int Enhancement { get; set; }

		public ItemBlessing Blessing { get; set; }

		public int BrokenBladeStacks { get; set; }

		public int ChargeCount { get; set; }

		public bool Locked { get; set; }

		public bool IsIdentified { get; set; } = true;

		public int ItemLevel { get; set; }

		public EquipmentAffixRoll[] Affixes { get; set; } = Array.Empty<EquipmentAffixRoll>();

		public string PetUid { get; set; } = "";

		public double? OilPercent { get; set; }

		public int MonsterCardLevel { get; set; }

		public double MonsterCardExperience { get; set; }

		public long MonsterCardReadyAtUnixMilliseconds { get; set; }

		public long ItemDelayReadyAtUnixMilliseconds { get; set; }

		public int AttrEnchantKind { get; set; }

		public int AttrEnchantLevel { get; set; }

		public double? RemainingUseSeconds { get; set; }

		public bool Sealed { get; set; }

		public static ItemStackSaveData Capture(ItemStack item)
		{
			return new ItemStackSaveData
			{
				Uid = item.Uid,
				ItemKey = item.ItemKey,
				Quantity = item.Quantity,
				Enhancement = item.Enhancement,
				Blessing = item.Blessing,
				BrokenBladeStacks = item.BrokenBladeStacks,
				ChargeCount = item.ChargeCount,
				Locked = item.Locked,
				IsIdentified = item.IsIdentified,
				ItemLevel = item.ItemLevel,
				Affixes = item.Affixes.ToArray(),
				PetUid = item.PetUid,
				OilPercent = item.OilPercent,
				MonsterCardLevel = item.MonsterCardLevel,
				MonsterCardExperience = item.MonsterCardExperience,
				MonsterCardReadyAtUnixMilliseconds = item.MonsterCardReadyAtUnixMilliseconds,
				ItemDelayReadyAtUnixMilliseconds = item.ItemDelayReadyAtUnixMilliseconds,
				AttrEnchantKind = item.AttrEnchantKind,
				AttrEnchantLevel = item.AttrEnchantLevel,
				RemainingUseSeconds = item.RemainingUseSeconds,
				Sealed = item.Sealed
			};
		}

		public ItemStack Restore()
		{
			return new ItemStack(Uid, ItemKey, Quantity)
			{
				Enhancement = Enhancement,
				Blessing = Blessing,
				BrokenBladeStacks = BrokenBladeStacks,
				ChargeCount = ChargeCount,
				Locked = Locked,
				IsIdentified = IsIdentified,
				ItemLevel = ItemLevel,
				Affixes = (Affixes?.ToArray() ?? Array.Empty<EquipmentAffixRoll>()),
				PetUid = PetUid,
				OilPercent = OilPercent,
				MonsterCardLevel = MonsterCardLevel,
				MonsterCardExperience = MonsterCardExperience,
				MonsterCardReadyAtUnixMilliseconds = MonsterCardReadyAtUnixMilliseconds,
				ItemDelayReadyAtUnixMilliseconds = ItemDelayReadyAtUnixMilliseconds,
				AttrEnchantKind = AttrEnchantKind,
				AttrEnchantLevel = AttrEnchantLevel,
				RemainingUseSeconds = RemainingUseSeconds,
				Sealed = Sealed
			};
		}
	}

	public const int CurrentVersion = 14;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static string Capture(Combatant player)
	{
		return CaptureCharacter(player, CombatantKind.Player, null);
	}

	public static string CaptureAlly(Combatant ally)
	{
		return CaptureCharacter(ally, CombatantKind.Ally, null);
	}

	public static string CaptureAlly(Combatant ally, string persistedCharacterKey)
	{
		return CaptureCharacter(ally, CombatantKind.Ally, Required(persistedCharacterKey, "persistedCharacterKey"));
	}

	private static string CaptureCharacter(Combatant player, CombatantKind expectedKind, string? persistedCharacterKey)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (player.Kind != expectedKind)
		{
			throw new ArgumentException($"Expected a {expectedKind} character.", "player");
		}
		ValidateFiniteNonNegative(player.Experience, "Experience");
		ValidateFiniteNonNegative(player.Hp, "Hp");
		ValidateFiniteNonNegative(player.Mp, "Mp");
		ValidateAlignment(player.Alignment);
		ValidateSatiety(player.Satiety);
		if (!L1jElixirRules.HasValidState(player))
		{
			throw new InvalidOperationException("Player elixir state is invalid.");
		}
		if (player.Gold < 0)
		{
			throw new ArgumentOutOfRangeException("player", "Player gold cannot be negative.");
		}
		if (player.Progress == null)
		{
			throw new InvalidOperationException("Player character progress cannot be null.");
		}
		try
		{
			player.Progress.Validate();
		}
		catch (InvalidDataException innerException)
		{
			throw new InvalidOperationException("Player character progress is invalid.", innerException);
		}
		SortedDictionary<string, ItemStackSaveData> sortedDictionary = new SortedDictionary<string, ItemStackSaveData>(StringComparer.Ordinal);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		string key;
		foreach (KeyValuePair<string, ItemStack> equippedItem in player.EquippedItems)
		{
			equippedItem.Deconstruct(out key, out var value);
			string text = key;
			ItemStack itemStack = value;
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Player equipment contains an empty slot.");
			}
			ValidateStack(itemStack, "equipment slot '" + text + "'");
			if (itemStack.Quantity != 1 && text != "arrow")
			{
				throw new InvalidOperationException("Equipment slot '" + text + "' must contain exactly one item unless it is ammunition.");
			}
			if (!hashSet.Add(itemStack.Uid))
			{
				throw new InvalidOperationException("Player item UID '" + itemStack.Uid + "' appears more than once.");
			}
			sortedDictionary[text] = ItemStackSaveData.Capture(itemStack);
		}
		List<ItemStackSaveData> list = new List<ItemStackSaveData>(player.InventoryStacks.Count);
		foreach (ItemStack inventoryStack in player.InventoryStacks)
		{
			ValidateStack(inventoryStack, "inventory");
			if (!hashSet.Add(inventoryStack.Uid))
			{
				throw new InvalidOperationException("Player item UID '" + inventoryStack.Uid + "' appears more than once.");
			}
			list.Add(ItemStackSaveData.Capture(inventoryStack));
		}
		SortedDictionary<string, int> sortedDictionary2 = new SortedDictionary<string, int>(StringComparer.Ordinal);
		int value2;
		foreach (KeyValuePair<string, int> allocation in player.Allocations)
		{
			allocation.Deconstruct(out key, out value2);
			string text2 = key;
			int num = value2;
			if (string.IsNullOrWhiteSpace(text2) || num < 0)
			{
				throw new InvalidOperationException("Player allocations contain an invalid attribute or point count.");
			}
			sortedDictionary2[text2] = num;
		}
		if (expectedKind == CombatantKind.Player && !L1jLevelStatRules.HasValidState(player))
		{
			throw new InvalidOperationException("Player level-stat allocation state is invalid.");
		}
		SortedDictionary<string, int> sortedDictionary3 = new SortedDictionary<string, int>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, int> levelStatBonuse in player.LevelStatBonuses)
		{
			levelStatBonuse.Deconstruct(out key, out value2);
			string key2 = key;
			int value3 = value2;
			sortedDictionary3[key2] = value3;
		}
		SortedDictionary<string, int> sortedDictionary4 = new SortedDictionary<string, int>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, int> elixirBonuse in player.ElixirBonuses)
		{
			elixirBonuse.Deconstruct(out key, out value2);
			string key3 = key;
			int value4 = value2;
			sortedDictionary4[key3] = value4;
		}
		SortedDictionary<string, BuffSaveData> sortedDictionary5 = new SortedDictionary<string, BuffSaveData>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, double> buff in player.Buffs)
		{
			buff.Deconstruct(out key, out var value5);
			string text3 = key;
			double num2 = value5;
			if (string.IsNullOrWhiteSpace(text3))
			{
				throw new InvalidOperationException("Player buffs contain an empty name.");
			}
			if (!text3.StartsWith("_", StringComparison.Ordinal))
			{
				if (double.IsNaN(num2) || double.IsNegativeInfinity(num2))
				{
					throw new InvalidOperationException("Player buff '" + text3 + "' has an invalid duration.");
				}
				if (!(num2 <= 0.0))
				{
					sortedDictionary5[text3] = BuffSaveData.Capture(num2);
				}
			}
		}
		return JsonSerializer.Serialize(new PlayerSaveData
		{
			Version = 14,
			Key = (persistedCharacterKey ?? Required(player.Key, "Key")),
			DisplayName = Required(player.Disp, "Disp"),
			Title = (player.Title ?? ""),
			ClassId = Required(player.ClassId, "ClassId"),
			Avatar = (player.Avatar ?? ""),
			Level = player.Level,
			Experience = player.Experience,
			Gold = player.Gold,
			Alignment = player.Alignment,
			Satiety = player.Satiety,
			Hp = player.Hp,
			Mp = player.Mp,
			Allocations = sortedDictionary2,
			LevelStatBonuses = sortedDictionary3,
			ElixirBonuses = sortedDictionary4,
			ElixirStatus = player.ElixirStatus,
			UnspentElixirStatPoints = player.UnspentElixirStatPoints,
			ItemUidSequence = player.ItemUidSequence,
			EquippedItems = sortedDictionary,
			InventoryStacks = list.ToArray(),
			LearnedSkills = player.LearnedSkills.Order<string>(StringComparer.Ordinal).ToArray(),
			GrantedSkills = player.GrantedSkills.Order<string>(StringComparer.Ordinal).ToArray(),
			ElfElement = (player.ElfElement ?? ""),
			PolymorphForm = (player.PolymorphForm ?? ""),
			Progress = CharacterProgressSaveData.Capture(player.Progress),
			Buffs = sortedDictionary5
		}, JsonOptions);
	}

	public static Combatant Restore(IGameData data, string blob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(blob, "blob");
		PlayerSaveData playerSaveData;
		try
		{
			playerSaveData = JsonSerializer.Deserialize<PlayerSaveData>(blob, JsonOptions) ?? throw new InvalidDataException("Player save is empty.");
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("Player save is not valid JSON.", innerException);
		}
		if (playerSaveData.Version != 14)
		{
			throw new InvalidDataException($"Unsupported player save version {playerSaveData.Version}; expected {14}.");
		}
		if (MonsterCompanionRules.IsCompanionSave(playerSaveData.ClassId))
		{
			return RestoreCompanion(data, playerSaveData);
		}
		Validate(data, playerSaveData);
		PlayerCombatantSpec playerCombatantSpec = new PlayerCombatantSpec(playerSaveData.Key, playerSaveData.DisplayName, playerSaveData.ClassId, playerSaveData.Level)
		{
			Avatar = playerSaveData.Avatar,
			Allocations = playerSaveData.Allocations,
			LevelStatBonuses = playerSaveData.LevelStatBonuses,
			ElixirBonuses = playerSaveData.ElixirBonuses,
			ElixirStatus = playerSaveData.ElixirStatus,
			UnspentElixirStatPoints = playerSaveData.UnspentElixirStatPoints,
			CurrentExperience = playerSaveData.Experience,
			CurrentGold = playerSaveData.Gold,
			CurrentHp = playerSaveData.Hp,
			CurrentMp = playerSaveData.Mp,
			ItemUidSequence = playerSaveData.ItemUidSequence
		};
		playerCombatantSpec = playerCombatantSpec with
		{
			EquippedItems = playerSaveData.EquippedItems.ToDictionary<KeyValuePair<string, ItemStackSaveData>, string, ItemStack>((KeyValuePair<string, ItemStackSaveData> pair) => pair.Key, (KeyValuePair<string, ItemStackSaveData> pair) => pair.Value.Restore(), StringComparer.Ordinal),
			InventoryStacks = ItemStackInventory.Consolidate(data, playerSaveData.InventoryStacks.Select((ItemStackSaveData item) => item.Restore())).ToArray()
		};
		Combatant combatant = CombatantBuilder.CreatePlayer(data, playerCombatantSpec);
		combatant.Title = playerSaveData.Title;
		combatant.Progress = playerSaveData.Progress.Restore();
		combatant.LearnedSkills.Import(playerSaveData.LearnedSkills);
		combatant.GrantedSkills.UnionWith(playerSaveData.GrantedSkills);
		combatant.ElfElement = playerSaveData.ElfElement;
		combatant.Alignment = playerSaveData.Alignment;
		combatant.Satiety = playerSaveData.Satiety;
		combatant.PolymorphForm = playerSaveData.PolymorphForm ?? "";
		foreach (var (key, buffSaveData2) in playerSaveData.Buffs)
		{
			combatant.Buffs[key] = buffSaveData2.Restore();
		}
		CombatantBuilder.RefreshPlayer(combatant, data);
		return combatant;
	}

	private static Combatant RestoreCompanion(IGameData data, PlayerSaveData save)
	{
		string avatar = save.Avatar;
		if (string.IsNullOrWhiteSpace(avatar) || data.Mob(avatar) == null)
		{
			throw new InvalidDataException("Monster companion save points at unknown mob '" + avatar + "'.");
		}
		Combatant combatant = MonsterCompanionRules.Create(data, avatar, Math.Max(1, save.Level), save.Key);
		combatant.Experience = Math.Max(0.0, save.Experience);
		combatant.Hp = Math.Clamp(save.Hp, 0.0, combatant.MaxHp);
		combatant.Mp = Math.Clamp(save.Mp, 0.0, combatant.MaxMp);
		combatant.Dead = combatant.Hp <= 0.0;
		return combatant;
	}

	public static Combatant RestoreAsAlly(IGameData data, string blob, bool restoreResources = true)
	{
		Combatant combatant = Restore(data, blob);
		combatant.Kind = CombatantKind.Ally;
		combatant.MoveTarget = null;
		combatant.VelX = 0.0;
		combatant.VelY = 0.0;
		combatant.Statuses.Clear();
		combatant.PeriodicEffects.Clear();
		combatant.Bleeds.Clear();
		if (restoreResources)
		{
			combatant.Hp = combatant.MaxHp;
			combatant.Mp = combatant.MaxMp;
			combatant.Dead = false;
		}
		else
		{
			combatant.Dead = combatant.Hp <= 0.0;
		}
		return combatant;
	}

	private static void Validate(IGameData data, PlayerSaveData save)
	{
		if (save.Version != 14)
		{
			throw new InvalidDataException($"Unsupported player save version {save.Version}; expected {14}.");
		}
		Required(save.Key, "Key");
		Required(save.DisplayName, "DisplayName");
		Required(save.ClassId, "ClassId");
		if (save.Title == null || save.Title.Length > 35)
		{
			throw new InvalidDataException("Player title is invalid.");
		}
		if (save.Level < 1 || save.Level > 99)
		{
			throw new InvalidDataException("Player level is outside the supported range.");
		}
		ValidateFiniteNonNegative(save.Experience, "Experience");
		ValidateFiniteNonNegative(save.Hp, "Hp");
		ValidateFiniteNonNegative(save.Mp, "Mp");
		if (save.Gold < 0)
		{
			throw new InvalidDataException("Player gold cannot be negative.");
		}
		ValidateAlignment(save.Alignment);
		ValidateSatiety(save.Satiety);
		if (save.ItemUidSequence < 0)
		{
			throw new InvalidDataException("Player item UID sequence cannot be negative.");
		}
		if (save.Allocations == null || save.LevelStatBonuses == null || save.ElixirBonuses == null || save.LearnedSkills == null || save.GrantedSkills == null)
		{
			throw new InvalidDataException("Player save is missing allocation or skill data.");
		}
		ValidateAllocations(save.Allocations);
		ValidateLevelStatBonuses(save.ClassId, save.Allocations, save.LevelStatBonuses, save.ElixirBonuses);
		ValidateElixirs(save.ElixirBonuses, save.ElixirStatus, save.UnspentElixirStatPoints);
		ValidateLearnedSkills(data, save.LearnedSkills);
		ValidateSkills(data, save.GrantedSkills, "granted");
		if (save.Progress == null)
		{
			throw new InvalidDataException("Player save is missing character progress.");
		}
		save.Progress.Validate();
		if (save.Buffs == null)
		{
			throw new InvalidDataException("Player save is missing buff data.");
		}
		string key;
		foreach (KeyValuePair<string, BuffSaveData> buff in save.Buffs)
		{
			buff.Deconstruct(out key, out var value);
			string text = key;
			(value ?? throw new InvalidDataException("Player buff '" + text + "' cannot be null.")).Validate(text);
		}
		if (save.InventoryStacks == null || save.EquippedItems == null)
		{
			throw new InvalidDataException("Player save is missing item-instance data.");
		}
		HashSet<string> usedUids = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < save.InventoryStacks.Length; i++)
		{
			ItemStackSaveData item = save.InventoryStacks[i] ?? throw new InvalidDataException($"Inventory stack {i} cannot be null.");
			ValidateSavedStack(data, item, $"inventoryStacks[{i}]", usedUids, null);
		}
		foreach (KeyValuePair<string, ItemStackSaveData> equippedItem in save.EquippedItems)
		{
			equippedItem.Deconstruct(out key, out var value2);
			string text2 = key;
			ItemStackSaveData itemStackSaveData = value2;
			if (string.IsNullOrWhiteSpace(text2))
			{
				throw new InvalidDataException("Equipment slot cannot be empty.");
			}
			if (itemStackSaveData == null)
			{
				throw new InvalidDataException("Equipment slot '" + text2 + "' cannot be null.");
			}
			ValidateSavedStack(data, itemStackSaveData, "equippedItems['" + text2 + "']", usedUids, text2);
		}
	}

	private static void ValidateSavedStack(IGameData data, ItemStackSaveData item, string location, ISet<string> usedUids, string? equippedSlot)
	{
		Required(item.Uid, location + ".uid");
		Required(item.ItemKey, location + ".itemKey");
		if (data.Item(item.ItemKey) == null)
		{
			throw new InvalidDataException($"Player save {location} references missing item '{item.ItemKey}'.");
		}
		if (!usedUids.Add(item.Uid))
		{
			throw new InvalidDataException("Player item UID '" + item.Uid + "' appears more than once.");
		}
		if (item.Quantity <= 0 || (equippedSlot != null && equippedSlot != "arrow" && item.Quantity != 1))
		{
			throw new InvalidDataException("Player save " + location + " has an invalid quantity.");
		}
		if (!Enum.IsDefined(typeof(ItemBlessing), item.Blessing))
		{
			throw new InvalidDataException("Player save " + location + " has an invalid blessing.");
		}
		if (item.BrokenBladeStacks < 0)
		{
			throw new InvalidDataException("Player save " + location + " has a negative broken-blade stack count.");
		}
		if (item.ChargeCount < 0)
		{
			throw new InvalidDataException("Player save " + location + " has a negative charge count.");
		}
		if (item.PetUid == null || (item.PetUid.Length > 0 && (!PetCollarRules.IsCollar(data, item.ItemKey) || string.IsNullOrWhiteSpace(item.PetUid))))
		{
			throw new InvalidDataException("Player save " + location + " has invalid pet-collar metadata.");
		}
		if (item.MonsterCardLevel < 0 || !double.IsFinite(item.MonsterCardExperience) || item.MonsterCardExperience < 0.0 || item.MonsterCardReadyAtUnixMilliseconds < 0)
		{
			throw new InvalidDataException("Player save " + location + " has invalid monster-card metadata.");
		}
		if (item.ItemDelayReadyAtUnixMilliseconds < 0)
		{
			throw new InvalidDataException("Player save " + location + " has invalid reusable-item cooldown metadata.");
		}
		ItemStack stack = item.Restore();
		try
		{
			ItemStackInventory.ValidateStack(stack);
		}
		catch (ArgumentException innerException)
		{
			throw new InvalidDataException("Player save " + location + " contains invalid item-instance state.", innerException);
		}
	}

	private static void ValidateStack(ItemStack item, string location)
	{
		try
		{
			ItemStackInventory.ValidateStack(item);
		}
		catch (ArgumentException innerException)
		{
			throw new InvalidOperationException("Player " + location + " contains invalid item-instance state.", innerException);
		}
	}

	private static void ValidateAllocations(IReadOnlyDictionary<string, int> allocations)
	{
		foreach (var (text2, num2) in allocations)
		{
			bool flag;
			switch (text2)
			{
			case "str":
			case "dex":
			case "con":
			case "int":
			case "wis":
			case "cha":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag || num2 < 0)
			{
				throw new InvalidDataException("Invalid allocated attribute '" + text2 + "'.");
			}
		}
	}

	private static void ValidateLevelStatBonuses(string classId, IReadOnlyDictionary<string, int> allocations, IReadOnlyDictionary<string, int> bonuses, IReadOnlyDictionary<string, int> elixirBonuses)
	{
		ValidateAllocations(bonuses);
		if (!ClassGrowthRules.IsKnownClass(classId))
		{
			throw new InvalidDataException("Unknown player class '" + classId + "'.");
		}
		if (allocations.Values.Sum() > ClassGrowthRules.Profile(classId).FreePoints)
		{
			throw new InvalidDataException("Player creation allocations exceed the class allowance.");
		}
		if (bonuses.Values.Sum() > L1jLevelStatRules.EarnedLevelPoints(99))
		{
			throw new InvalidDataException("Player level-stat allocations exceed the lifetime maximum.");
		}
		Attributes attributes = ClassGrowthRules.BaseAttributes(classId);
		foreach (string attributeKey in L1jLevelStatRules.AttributeKeys)
		{
			double num = attributeKey switch
			{
				"str" => attributes.Str, 
				"dex" => attributes.Dex, 
				"con" => attributes.Con, 
				"int" => attributes.Int, 
				"wis" => attributes.Wis, 
				"cha" => attributes.Cha, 
				_ => 0.0, 
			};
			double num2 = Math.Min(18.0, num + (double)allocations.GetValueOrDefault(attributeKey));
			if (num + (double)allocations.GetValueOrDefault(attributeKey) > 18.0)
			{
				throw new InvalidDataException("Player creation attribute '" + attributeKey + "' exceeds the maximum.");
			}
			if (num2 + (double)bonuses.GetValueOrDefault(attributeKey) + (double)elixirBonuses.GetValueOrDefault(attributeKey) > 35.0)
			{
				throw new InvalidDataException("Player base attribute '" + attributeKey + "' exceeds the maximum.");
			}
		}
	}

	private static void ValidateElixirs(IReadOnlyDictionary<string, int> bonuses, int elixirStatus, int unspentPoints)
	{
		if ((elixirStatus < 0 || elixirStatus > 5) ? true : false)
		{
			throw new InvalidDataException("Player elixir status is outside the supported range.");
		}
		if ((unspentPoints < 0 || unspentPoints > 5) ? true : false)
		{
			throw new InvalidDataException("Player unspent elixir points are outside the supported range.");
		}
		int num = 0;
		foreach (var (text2, num3) in bonuses)
		{
			if (!L1jElixirRules.IsAttributeKey(text2) || num3 < 0 || num3 > 5)
			{
				throw new InvalidDataException("Invalid elixir attribute '" + text2 + "'.");
			}
			num += num3;
		}
		if (num + unspentPoints != elixirStatus)
		{
			throw new InvalidDataException("Player elixir status does not match assigned and unspent elixir points.");
		}
	}

	private static void ValidateSkills(IGameData data, IEnumerable<string> skillKeys, string source)
	{
		foreach (string skillKey in skillKeys)
		{
			if (string.IsNullOrWhiteSpace(skillKey) || data.Skill(skillKey) == null)
			{
				Godot.GD.PushWarning($"Player save {source} skills reference missing skill '{skillKey}'; continuing.");
			}
		}
	}

	private static void ValidateLearnedSkills(IGameData data, IEnumerable<string> skills)
	{
		foreach (string skill in skills)
		{
			if (string.IsNullOrWhiteSpace(skill) || data.Skill(skill) == null)
			{
				Godot.GD.PushWarning("Player save learned skills reference missing skill '" + skill + "'; continuing.");
			}
		}
	}

	private static string Required(string? value, string field)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		throw new InvalidDataException("Player save field '" + field + "' is required.");
	}

	private static void ValidateAlignment(double value)
	{
		if (!double.IsFinite(value) || CombatCurveMath.ClampAlignment(value) != value)
		{
			throw new InvalidDataException("Player alignment must be an integer from -32767 through 32767.");
		}
	}

	private static void ValidateSatiety(double value)
	{
		if (!double.IsFinite(value) || SatietyRules.Clamp(value) != value)
		{
			throw new InvalidDataException($"Player satiety must be between 0 and {225.0}.");
		}
	}

	private static void ValidateFiniteNonNegative(double value, string field)
	{
		if (!double.IsFinite(value) || value < 0.0)
		{
			throw new InvalidDataException("Player save field '" + field + "' must be finite and non-negative.");
		}
	}
}
