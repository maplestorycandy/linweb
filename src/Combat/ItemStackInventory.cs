using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ItemStackInventory
{
	public static bool CanStack(ItemStack left, ItemStack right)
	{
		ArgumentNullException.ThrowIfNull(left, "left");
		ArgumentNullException.ThrowIfNull(right, "right");
		if (!left.HasUniqueState && !right.HasUniqueState && left.ItemDelayReadyAtUnixMilliseconds == right.ItemDelayReadyAtUnixMilliseconds)
		{
			return left.Signature == right.Signature;
		}
		return false;
	}

	public static bool CanStack(IGameData data, ItemStack left, ItemStack right)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (CanStack(left, right))
		{
			return true;
		}
		if (!TemplateIsStackable(data, left.ItemKey) || !string.Equals(left.ItemKey, right.ItemKey, StringComparison.Ordinal))
		{
			return false;
		}
		ItemStack itemStack = right.Copy();
		itemStack.IsIdentified = left.IsIdentified;
		return CanStack(left, itemStack);
	}

	public static bool TryAddOrStack(IList<ItemStack> inventory, ItemStack incoming, out ItemStack stored)
	{
		return TryAddOrStackCore(inventory, incoming, (ItemStack candidate) => CanStack(candidate, incoming), out stored);
	}

	public static bool TryAddOrStack(IGameData data, IList<ItemStack> inventory, ItemStack incoming, out ItemStack stored)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return TryAddOrStackCore(inventory, incoming, (ItemStack candidate) => CanStack(data, candidate, incoming), out stored);
	}

	private static bool TryAddOrStackCore(IList<ItemStack> inventory, ItemStack incoming, Func<ItemStack, bool> canStack, out ItemStack stored)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentNullException.ThrowIfNull(canStack, "canStack");
		ValidateStack(incoming);
		ItemStack itemStack = (incoming.HasUniqueState ? null : inventory.FirstOrDefault(canStack));
		if (itemStack != null)
		{
			if (itemStack.Quantity > long.MaxValue - incoming.Quantity)
			{
				stored = itemStack;
				return false;
			}
			itemStack.Quantity += incoming.Quantity;
			ApplyProtection(itemStack, incoming);
			stored = itemStack;
			return true;
		}
		if (IndexOfUid(inventory, incoming.Uid) >= 0)
		{
			stored = incoming;
			return false;
		}
		ItemStack itemStack2 = incoming.Copy();
		inventory.Add(itemStack2);
		stored = itemStack2;
		return true;
	}

	private static bool TemplateIsStackable(IGameData data, string itemKey)
	{
		bool value = default(bool);
		return data.Item(itemKey)?["stackable"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	public static bool TryDetachOne(IList<ItemStack> inventory, string uid, Func<string> uidFactory, out ItemStack? detached)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		ArgumentNullException.ThrowIfNull(uidFactory, "uidFactory");
		int num = IndexOfUid(inventory, uid);
		if (num < 0)
		{
			detached = null;
			return false;
		}
		if (inventory[num].Quantity == 1)
		{
			detached = inventory[num];
			return true;
		}
		return TryRemove(inventory, uid, 1L, uidFactory, out detached);
	}

	public static bool TryRemove(IList<ItemStack> inventory, string uid, long quantity, Func<string> uidFactory, out ItemStack? removed)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		ArgumentNullException.ThrowIfNull(uidFactory, "uidFactory");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		int num = IndexOfUid(inventory, uid);
		if (num < 0 || inventory[num].Quantity < quantity)
		{
			removed = null;
			return false;
		}
		ItemStack itemStack = inventory[num];
		if (itemStack.Quantity == quantity)
		{
			inventory.RemoveAt(num);
			removed = itemStack.Copy();
			return true;
		}
		string uid2 = RequiredUid(uidFactory);
		itemStack.Quantity -= quantity;
		removed = itemStack.Copy(uid2, quantity);
		return true;
	}

	public static bool TryTransfer(IList<ItemStack> source, IList<ItemStack> destination, string uid, long quantity, Func<string> uidFactory)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(destination, "destination");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		ArgumentNullException.ThrowIfNull(uidFactory, "uidFactory");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		int num = IndexOfUid(source, uid);
		if (num < 0)
		{
			return false;
		}
		ItemStack itemStack = source[num];
		ValidateStack(itemStack);
		if (itemStack.Quantity < quantity)
		{
			return false;
		}
		if (source == destination)
		{
			return true;
		}
		ItemStack itemStack2 = FindStack(destination, itemStack);
		if (itemStack2 != null && itemStack2.Quantity > long.MaxValue - quantity)
		{
			return false;
		}
		string uid2 = ((itemStack.Quantity == quantity) ? itemStack.Uid : RequiredUid(uidFactory));
		if (itemStack2 == null && IndexOfUid(destination, uid2) >= 0)
		{
			return false;
		}
		ItemStack itemStack3 = itemStack.Copy(uid2, quantity);
		if (itemStack.Quantity == quantity)
		{
			source.RemoveAt(num);
		}
		else
		{
			itemStack.Quantity -= quantity;
		}
		if (itemStack2 != null)
		{
			itemStack2.Quantity += quantity;
			ApplyProtection(itemStack2, itemStack3);
		}
		else
		{
			destination.Add(itemStack3);
		}
		return true;
	}

	public static long CountByItemKey(IEnumerable<ItemStack> inventory, string itemKey, bool includeLocked = true)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return 0L;
		}
		long num = 0L;
		foreach (ItemStack item in inventory)
		{
			ValidateStack(item);
			if (!(item.ItemKey != itemKey) && (includeLocked || !item.Locked))
			{
				if (num > long.MaxValue - item.Quantity)
				{
					return long.MaxValue;
				}
				num += item.Quantity;
			}
		}
		return num;
	}

	public static long CountByItemKeyAndBlessing(IEnumerable<ItemStack> inventory, string itemKey, ItemBlessing blessing, bool includeLocked = true)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return 0L;
		}
		long num = 0L;
		foreach (ItemStack item in inventory)
		{
			ValidateStack(item);
			if (!(item.ItemKey != itemKey) && item.Blessing == blessing && (includeLocked || !item.Locked))
			{
				if (num > long.MaxValue - item.Quantity)
				{
					return long.MaxValue;
				}
				num += item.Quantity;
			}
		}
		return num;
	}

	public static bool TryRemoveByItemKey(IList<ItemStack> inventory, string itemKey, long quantity, bool includeLocked = false)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		if (CountByItemKey(inventory, itemKey, includeLocked) < quantity)
		{
			return false;
		}
		long num = quantity;
		int num2 = 0;
		while (num2 < inventory.Count && num > 0)
		{
			ItemStack itemStack = inventory[num2];
			if (itemStack.ItemKey != itemKey || (!includeLocked && itemStack.Locked))
			{
				num2++;
				continue;
			}
			long num3 = Math.Min(itemStack.Quantity, num);
			itemStack.Quantity -= num3;
			num -= num3;
			if (itemStack.Quantity == 0L)
			{
				inventory.RemoveAt(num2);
			}
			else
			{
				num2++;
			}
		}
		return true;
	}

	public static bool TryRemoveByItemKeyAndBlessing(IList<ItemStack> inventory, string itemKey, ItemBlessing blessing, long quantity, bool includeLocked = false)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		if (CountByItemKeyAndBlessing(inventory, itemKey, blessing, includeLocked) < quantity)
		{
			return false;
		}
		long num = quantity;
		int num2 = 0;
		while (num2 < inventory.Count && num > 0)
		{
			ItemStack itemStack = inventory[num2];
			if (itemStack.ItemKey != itemKey || itemStack.Blessing != blessing || (!includeLocked && itemStack.Locked))
			{
				num2++;
				continue;
			}
			long num3 = Math.Min(itemStack.Quantity, num);
			itemStack.Quantity -= num3;
			num -= num3;
			if (itemStack.Quantity == 0L)
			{
				inventory.RemoveAt(num2);
			}
			else
			{
				num2++;
			}
		}
		return true;
	}

	public static bool TryRemoveByUid(IList<ItemStack> inventory, string uid, long quantity, out ItemStack? removed, bool includeLocked = false)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		int num = IndexOfUid(inventory, uid);
		if (num < 0 || inventory[num].Quantity < quantity || (!includeLocked && inventory[num].Locked))
		{
			removed = null;
			return false;
		}
		ItemStack itemStack = inventory[num];
		removed = itemStack.Copy(itemStack.Uid, quantity);
		if (itemStack.Quantity == quantity)
		{
			inventory.RemoveAt(num);
		}
		else
		{
			itemStack.Quantity -= quantity;
		}
		return true;
	}

	public static IReadOnlyList<ItemStack> Consolidate(IEnumerable<ItemStack> stacks)
	{
		ArgumentNullException.ThrowIfNull(stacks, "stacks");
		List<ItemStack> list = new List<ItemStack>();
		foreach (ItemStack stack in stacks)
		{
			if (!TryAddOrStack(list, stack, out ItemStack _))
			{
				throw new OverflowException("Consolidating item '" + stack.ItemKey + "' exceeds Int64.");
			}
		}
		return new ReadOnlyCollection<ItemStack>(list);
	}

	public static IReadOnlyList<ItemStack> Consolidate(IGameData data, IEnumerable<ItemStack> stacks)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(stacks, "stacks");
		List<ItemStack> list = new List<ItemStack>();
		foreach (ItemStack stack in stacks)
		{
			if (!TryAddOrStack(data, list, stack, out ItemStack _))
			{
				throw new OverflowException("Consolidating item '" + stack.ItemKey + "' exceeds Int64.");
			}
		}
		return new ReadOnlyCollection<ItemStack>(list);
	}

	public static IReadOnlyList<ItemStack> FromPlainCounts(IReadOnlyDictionary<string, long> counts, Func<string> uidFactory)
	{
		ArgumentNullException.ThrowIfNull(counts, "counts");
		ArgumentNullException.ThrowIfNull(uidFactory, "uidFactory");
		List<ItemStack> list = new List<ItemStack>(counts.Count);
		foreach (var (text2, num2) in counts)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(text2, "itemKey");
			if (num2 < 0)
			{
				throw new ArgumentOutOfRangeException("counts", "Inventory quantity for '" + text2 + "' cannot be negative.");
			}
			if (num2 != 0L)
			{
				list.Add(new ItemStack(RequiredUid(uidFactory), text2, num2));
			}
		}
		return new ReadOnlyCollection<ItemStack>(list);
	}

	public static IReadOnlyDictionary<string, long> ToPlainCounts(IEnumerable<ItemStack> stacks)
	{
		ArgumentNullException.ThrowIfNull(stacks, "stacks");
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (ItemStack stack in stacks)
		{
			ValidateStack(stack);
			long valueOrDefault = dictionary.GetValueOrDefault(stack.ItemKey);
			if (valueOrDefault > long.MaxValue - stack.Quantity)
			{
				throw new OverflowException("Plain item count for '" + stack.ItemKey + "' exceeds Int64.");
			}
			dictionary[stack.ItemKey] = valueOrDefault + stack.Quantity;
		}
		return new ReadOnlyDictionary<string, long>(dictionary);
	}

	public static IReadOnlyList<ItemStack> CopyAll(IEnumerable<ItemStack> stacks)
	{
		ArgumentNullException.ThrowIfNull(stacks, "stacks");
		List<ItemStack> list = new List<ItemStack>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemStack stack in stacks)
		{
			ValidateStack(stack);
			if (!hashSet.Add(stack.Uid))
			{
				throw new InvalidDataException("Item UID '" + stack.Uid + "' appears more than once.");
			}
			list.Add(stack.Copy());
		}
		return new ReadOnlyCollection<ItemStack>(list);
	}

	private static ItemStack? FindStack(IEnumerable<ItemStack> inventory, ItemStack incoming)
	{
		if (!incoming.HasUniqueState)
		{
			return inventory.FirstOrDefault((ItemStack stack) => CanStack(stack, incoming));
		}
		return null;
	}

	private static int IndexOfUid(IList<ItemStack> inventory, string uid)
	{
		for (int i = 0; i < inventory.Count; i++)
		{
			if (string.Equals(inventory[i].Uid, uid, StringComparison.Ordinal))
			{
				return i;
			}
		}
		return -1;
	}

	private static void ApplyProtection(ItemStack target, ItemStack source)
	{
		if (source.Locked)
		{
			target.Locked = true;
		}
	}

	public static void ValidateStack(ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		ArgumentException.ThrowIfNullOrWhiteSpace(stack.Uid, "stack.Uid");
		ArgumentException.ThrowIfNullOrWhiteSpace(stack.ItemKey, "stack.ItemKey");
		if (stack.Quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("stack", "Item stack quantity must be positive.");
		}
		if (stack.BrokenBladeStacks < 0)
		{
			throw new ArgumentOutOfRangeException("stack", "Broken-blade stacks cannot be negative.");
		}
		if (stack.PetUid.Length > 0 && string.IsNullOrWhiteSpace(stack.PetUid))
		{
			throw new ArgumentException("A bound pet collar must carry a non-blank pet id.", "stack");
		}
		if (stack.MonsterCardLevel < 0)
		{
			throw new ArgumentOutOfRangeException("stack", "Monster-card level cannot be negative.");
		}
		if (!double.IsFinite(stack.MonsterCardExperience) || stack.MonsterCardExperience < 0.0)
		{
			throw new ArgumentOutOfRangeException("stack", "Monster-card experience must be finite and non-negative.");
		}
		if (stack.MonsterCardReadyAtUnixMilliseconds < 0)
		{
			throw new ArgumentOutOfRangeException("stack", "Monster-card cooldown timestamp cannot be negative.");
		}
		if (stack.ItemDelayReadyAtUnixMilliseconds < 0)
		{
			throw new ArgumentOutOfRangeException("stack", "Reusable-item cooldown timestamp cannot be negative.");
		}
		int itemLevel = stack.ItemLevel;
		if ((itemLevel < 0 || itemLevel > 99) ? true : false)
		{
			throw new ArgumentOutOfRangeException("stack", "Equipment affix item level must be between 0 and 99.");
		}
		if (stack.Affixes == null || stack.Affixes.Count > 4 || (stack.Affixes.Count > 0 && stack.ItemLevel == 0) || stack.Affixes.Any((EquipmentAffixRoll affix) => !EquipmentAffixRules.IsValid(affix)) || stack.Affixes.Select((EquipmentAffixRoll affix) => affix.AffixId).Distinct<string>(StringComparer.Ordinal).Count() != stack.Affixes.Count)
		{
			throw new ArgumentException("Equipment affix metadata is invalid.", "stack");
		}
		bool flag;
		switch (stack.AttrEnchantKind)
		{
		case 0:
		case 1:
		case 2:
		case 4:
		case 8:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new ArgumentOutOfRangeException("stack", "Weapon attribute kind must be 0/1/2/4/8 (main's attr_enchant_kind).");
		}
		itemLevel = stack.AttrEnchantLevel;
		if ((itemLevel < 0 || itemLevel > 3) ? true : false)
		{
			throw new ArgumentOutOfRangeException("stack", "Weapon attribute level must be between 0 and 3.");
		}
		if (stack.AttrEnchantKind == 0 != (stack.AttrEnchantLevel == 0))
		{
			throw new ArgumentException("Weapon attribute kind and level must both be set or both be zero.", "stack");
		}
		if (stack.HasUniqueState && stack.Quantity != 1)
		{
			throw new ArgumentException("Items with unique per-instance state cannot have a quantity above one.", "stack");
		}
	}

	private static string RequiredUid(Func<string> uidFactory)
	{
		string text = uidFactory();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new InvalidOperationException("The item UID factory returned an empty value.");
		}
		return text;
	}
}
