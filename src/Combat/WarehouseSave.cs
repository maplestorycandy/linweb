using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WarehouseSave
{
	private sealed class WarehouseSaveData
	{
		public int Version { get; set; }

		public string Key { get; set; } = "";

		public int Capacity { get; set; }

		public long Gold { get; set; }

		public long ItemUidSequence { get; set; }

		public ItemStackSaveData[] Items { get; set; } = Array.Empty<ItemStackSaveData>();
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

		public bool Junk { get; set; }

		public double? OilPercent { get; set; }

		public int MonsterCardLevel { get; set; }

		public double MonsterCardExperience { get; set; }

		public long MonsterCardReadyAtUnixMilliseconds { get; set; }

		public long ItemDelayReadyAtUnixMilliseconds { get; set; }

		public int AttrEnchantKind { get; set; }

		public int AttrEnchantLevel { get; set; }

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
				AttrEnchantLevel = item.AttrEnchantLevel
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
				AttrEnchantLevel = AttrEnchantLevel
			};
		}
	}

	public const int CurrentVersion = 4;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static string Capture(WarehouseState warehouse)
	{
		ArgumentNullException.ThrowIfNull(warehouse, "warehouse");
		ValidateState(warehouse);
		return JsonSerializer.Serialize(new WarehouseSaveData
		{
			Version = 4,
			Key = warehouse.Key,
			Capacity = warehouse.Capacity,
			Gold = warehouse.Gold,
			ItemUidSequence = warehouse.ItemUidSequence,
			Items = warehouse.Items.Select(ItemStackSaveData.Capture).ToArray()
		}, JsonOptions);
	}

	public static WarehouseState Restore(IGameData data, string blob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(blob, "blob");
		WarehouseSaveData warehouseSaveData;
		try
		{
			warehouseSaveData = JsonSerializer.Deserialize<WarehouseSaveData>(blob, JsonOptions) ?? throw new InvalidDataException("Warehouse save is empty.");
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("Warehouse save is not valid JSON.", innerException);
		}
		Validate(data, warehouseSaveData);
		WarehouseState warehouseState = new WarehouseState(warehouseSaveData.Key, warehouseSaveData.Capacity);
		warehouseState.Gold = warehouseSaveData.Gold;
		warehouseState.ReplaceItems(warehouseSaveData.Items.Select((ItemStackSaveData item) => item.Restore()), warehouseSaveData.ItemUidSequence);
		return warehouseState;
	}

	private static void ValidateState(WarehouseState warehouse)
	{
		if (warehouse.Gold < 0)
		{
			throw new InvalidOperationException("Warehouse gold cannot be negative.");
		}
		if (warehouse.ItemUidSequence < 0)
		{
			throw new InvalidOperationException("Warehouse item UID sequence cannot be negative.");
		}
		if (warehouse.Items.Count > warehouse.Capacity)
		{
			throw new InvalidOperationException("Warehouse item count exceeds capacity.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemStack item in warehouse.Items)
		{
			try
			{
				ItemStackInventory.ValidateStack(item);
			}
			catch (ArgumentException innerException)
			{
				throw new InvalidOperationException("Warehouse contains invalid item-instance state.", innerException);
			}
			if (!hashSet.Add(item.Uid))
			{
				throw new InvalidOperationException("Warehouse item UID '" + item.Uid + "' appears more than once.");
			}
		}
	}

	private static void Validate(IGameData data, WarehouseSaveData save)
	{
		int version = save.Version;
		if ((uint)(version - 3) > 1u)
		{
			throw new InvalidDataException($"Unsupported warehouse save version {save.Version}; expected 3 or {4}.");
		}
		if (string.IsNullOrWhiteSpace(save.Key))
		{
			throw new InvalidDataException("Warehouse key is required.");
		}
		if (save.Capacity <= 0)
		{
			throw new InvalidDataException("Warehouse capacity must be positive.");
		}
		if (save.Gold < 0)
		{
			throw new InvalidDataException("Warehouse gold cannot be negative.");
		}
		if (save.ItemUidSequence < 0)
		{
			throw new InvalidDataException("Warehouse item UID sequence cannot be negative.");
		}
		if (save.Items == null)
		{
			throw new InvalidDataException("Warehouse save is missing item-instance data.");
		}
		if (save.Items.Length > save.Capacity)
		{
			throw new InvalidDataException("Warehouse item count exceeds capacity.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < save.Items.Length; i++)
		{
			ItemStackSaveData itemStackSaveData = save.Items[i] ?? throw new InvalidDataException($"Warehouse item {i} cannot be null.");
			string text = $"items[{i}]";
			if (string.IsNullOrWhiteSpace(itemStackSaveData.Uid) || string.IsNullOrWhiteSpace(itemStackSaveData.ItemKey))
			{
				throw new InvalidDataException("Warehouse save " + text + " is missing identity data.");
			}
			if (data.Item(itemStackSaveData.ItemKey) == null)
			{
				throw new InvalidDataException($"Warehouse save {text} references missing item '{itemStackSaveData.ItemKey}'.");
			}
			if (!hashSet.Add(itemStackSaveData.Uid))
			{
				throw new InvalidDataException("Warehouse item UID '" + itemStackSaveData.Uid + "' appears more than once.");
			}
			if (itemStackSaveData.PetUid == null || (itemStackSaveData.PetUid.Length > 0 && (!PetCollarRules.IsCollar(data, itemStackSaveData.ItemKey) || string.IsNullOrWhiteSpace(itemStackSaveData.PetUid))))
			{
				throw new InvalidDataException("Warehouse save " + text + " has invalid pet-collar metadata.");
			}
			if (!Enum.IsDefined(typeof(ItemBlessing), itemStackSaveData.Blessing) || itemStackSaveData.BrokenBladeStacks < 0 || itemStackSaveData.ChargeCount < 0 || itemStackSaveData.MonsterCardLevel < 0 || !double.IsFinite(itemStackSaveData.MonsterCardExperience) || itemStackSaveData.MonsterCardExperience < 0.0 || itemStackSaveData.MonsterCardReadyAtUnixMilliseconds < 0 || itemStackSaveData.ItemDelayReadyAtUnixMilliseconds < 0)
			{
				throw new InvalidDataException("Warehouse save " + text + " contains invalid item-instance metadata.");
			}
			try
			{
				ItemStackInventory.ValidateStack(itemStackSaveData.Restore());
			}
			catch (ArgumentException innerException)
			{
				throw new InvalidDataException("Warehouse save " + text + " contains invalid item-instance state.", innerException);
			}
		}
	}
}
