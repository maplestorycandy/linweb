using System;
using System.Linq;

namespace IdleLineage.Combat;

public static class L1jCookingRules
{
	public const double DurationSeconds = 900.0;

	private const string FoodPrefix = "_l1j_cooking:";

	private const string DessertPrefix = "_l1j_dessert:";

	public static bool IsCookingItemId(int itemId)
	{
		switch (itemId)
		{
		case 41277:
		case 41278:
		case 41279:
		case 41280:
		case 41281:
		case 41282:
		case 41283:
		case 41284:
		case 41285:
		case 41286:
		case 41287:
		case 41288:
		case 41289:
		case 41290:
		case 41291:
		case 41292:
		case 49049:
		case 49050:
		case 49051:
		case 49052:
		case 49053:
		case 49054:
		case 49055:
		case 49056:
		case 49057:
		case 49058:
		case 49059:
		case 49060:
		case 49061:
		case 49062:
		case 49063:
		case 49064:
		case 49244:
		case 49245:
		case 49246:
		case 49247:
		case 49248:
		case 49249:
		case 49250:
		case 49251:
		case 49252:
		case 49253:
		case 49254:
		case 49255:
		case 49256:
		case 49257:
		case 49258:
		case 49259:
			return true;
		default:
			return false;
		}
	}

	public static L1jCookingUseResult TryUse(Combatant owner, string itemUid, int itemId)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if (!TryDecode(itemId, out var type, out var special))
		{
			return new L1jCookingUseResult(Success: false, "這不是 main 料理");
		}
		bool flag = type % 8 == 7;
		if (flag && SatietyRules.Clamp(owner.Satiety) != 225.0)
		{
			return new L1jCookingUseResult(Success: false, "甜點只能在飽食度全滿時食用");
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null || itemStack.Locked || !ItemStackInventory.TryRemoveByUid(owner.InventoryStacks, itemUid, 1L, out ItemStack _))
		{
			return new L1jCookingUseResult(Success: false, "背包中找不到可食用的料理");
		}
		string prefix = (flag ? "_l1j_dessert:" : "_l1j_cooking:");
		string[] array = owner.Buffs.Keys.Where((string text) => text.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
		foreach (string key in array)
		{
			owner.Buffs.Remove(key);
		}
		owner.Buffs[$"{prefix}{type}:{(special ? 1 : 0)}"] = 900.0;
		return new L1jCookingUseResult(Success: true, special ? "食用幻想料理（900 秒；傷害減免 +5）" : "食用料理（900 秒）", itemId, type, special);
	}

	public static bool AffectsDerivedStats(string buffName)
	{
		if (!buffName.StartsWith("_l1j_cooking:", StringComparison.Ordinal))
		{
			return buffName.StartsWith("_l1j_dessert:", StringComparison.Ordinal);
		}
		return true;
	}

	public static void ApplyDerived(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		foreach (string key in actor.Buffs.Keys)
		{
			if (TryDecodeBuff(key, out var type, out var special))
			{
				ApplyType(actor, type);
				if (special)
				{
					actor.D.DamageReduction += 5.0;
				}
			}
		}
		actor.Hp = Math.Min(actor.Hp, actor.MaxHp);
		actor.Mp = Math.Min(actor.Mp, actor.MaxMp);
	}

	private static bool TryDecode(int itemId, out int type, out bool special)
	{
		int num;
		int num2;
		if (itemId >= 41277 && itemId <= 41292)
		{
			num = 0;
			num2 = itemId - 41277;
		}
		else if (itemId >= 49049 && itemId <= 49064)
		{
			num = 1;
			num2 = itemId - 49049;
		}
		else
		{
			if (itemId < 49244 || itemId > 49259)
			{
				type = -1;
				special = false;
				return false;
			}
			num = 2;
			num2 = itemId - 49244;
		}
		special = num2 >= 8;
		type = num * 8 + num2 % 8;
		return true;
	}

	private static bool TryDecodeBuff(string key, out int type, out bool special)
	{
		type = -1;
		string text = (key.StartsWith("_l1j_cooking:", StringComparison.Ordinal) ? "_l1j_cooking:" : (key.StartsWith("_l1j_dessert:", StringComparison.Ordinal) ? "_l1j_dessert:" : ""));
		if (text.Length == 0)
		{
			special = false;
			return false;
		}
		string[] array = key.Substring(text.Length).Split(':');
		if (array.Length != 2 || !int.TryParse(array[0], out type))
		{
			special = false;
			return false;
		}
		special = array[1] == "1";
		int num = type;
		if (num >= 0)
		{
			return num < 24;
		}
		return false;
	}

	private static void ApplyType(Combatant actor, int type)
	{
		switch (type)
		{
		case 0:
			actor.D.ResistFire += 10.0;
			actor.D.ResistWater += 10.0;
			actor.D.ResistWind += 10.0;
			actor.D.ResistEarth += 10.0;
			break;
		case 1:
			actor.MaxHp += 30.0;
			break;
		case 2:
			actor.D.ManaRegen += 3.0;
			break;
		case 3:
			actor.D.ArmorClass -= 1.0;
			break;
		case 4:
			actor.MaxMp += 20.0;
			break;
		case 5:
			actor.D.HealthRegenFlat += 3.0;
			break;
		case 6:
			actor.D.MagicResist += 5.0;
			break;
		case 8:
			actor.D.MeleeHit += 1.0;
			actor.D.MeleeDamage += 1.0;
			break;
		case 9:
			actor.MaxHp += 30.0;
			actor.MaxMp += 30.0;
			break;
		case 10:
			actor.D.ArmorClass -= 2.0;
			break;
		case 11:
			actor.D.RangedHit += 1.0;
			actor.D.RangedDamage += 1.0;
			break;
		case 12:
			actor.D.HealthRegenFlat += 2.0;
			actor.D.ManaRegen += 2.0;
			break;
		case 13:
			actor.D.MagicResist += 10.0;
			break;
		case 14:
			actor.D.ItemSpellPower += 1.0;
			break;
		case 16:
			actor.D.RangedHit += 1.0;
			actor.D.RangedDamage += 1.0;
			break;
		case 17:
			actor.MaxHp += 50.0;
			actor.MaxMp += 50.0;
			break;
		case 18:
			actor.D.MeleeHit += 2.0;
			actor.D.MeleeDamage += 1.0;
			break;
		case 19:
			actor.D.ArmorClass -= 3.0;
			break;
		case 20:
			actor.D.MagicResist += 15.0;
			actor.D.ResistFire += 10.0;
			actor.D.ResistWater += 10.0;
			actor.D.ResistWind += 10.0;
			actor.D.ResistEarth += 10.0;
			break;
		case 21:
			actor.D.ItemSpellPower += 2.0;
			actor.D.ManaRegen += 2.0;
			break;
		case 22:
			actor.MaxHp += 30.0;
			actor.D.HealthRegenFlat += 2.0;
			break;
		case 7:
		case 15:
			break;
		}
	}
}
