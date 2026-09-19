using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class SetRules
{
	public static SetSnapshot Evaluate(IGameData data, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		L1jArmorSetCatalog l1jArmorSetCatalog = L1jArmorSetCatalog.Load(data);
		HashSet<int> hashSet = new HashSet<int>();
		foreach (var item2 in Equipped(actor))
		{
			ItemStack item = item2.Stack;
			JsonObject jsonObject = data.Item(item.ItemKey);
			int num = ((jsonObject != null) ? ReadInt(jsonObject, "l1jItemId") : 0);
			if (num > 0)
			{
				hashSet.Add(num);
			}
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		List<SetTierState> list = new List<SetTierState>(l1jArmorSetCatalog.Sets.Count);
		foreach (L1jArmorSetDefinition set in l1jArmorSetCatalog.Sets)
		{
			int num2 = set.ItemIds.Count(hashSet.Contains);
			if (num2 > 0)
			{
				dictionary.Add(set.Code, num2);
			}
			list.Add(new SetTierState(set.Code, set.Name, set.ItemIds.Count, num2, set.Description, num2 == set.ItemIds.Count));
		}
		return new SetSnapshot(new ReadOnlyDictionary<string, int>(dictionary), new ReadOnlyCollection<SetTierState>(list));
	}

	public static void ApplyEarlyAttributes(IGameData data, Combatant actor, Attributes attributes)
	{
		ArgumentNullException.ThrowIfNull(attributes, "attributes");
		SetSnapshot snapshot = Evaluate(data, actor);
		foreach (L1jArmorSetDefinition item in ActiveSets(data, snapshot))
		{
			L1jArmorSetBonus bonus = item.Bonus;
			attributes.Str += bonus.Strength;
			attributes.Dex += bonus.Dexterity;
			attributes.Con += bonus.Constitution;
			attributes.Wis += bonus.Wisdom;
			attributes.Cha += bonus.Charisma;
			attributes.Int += bonus.Intelligence;
		}
	}

	public static double ApplyDerivedBonuses(IGameData data, Combatant actor)
	{
		SetSnapshot snapshot = Evaluate(data, actor);
		foreach (L1jArmorSetDefinition item in ActiveSets(data, snapshot))
		{
			L1jArmorSetBonus bonus = item.Bonus;
			actor.D.ArmorClass += bonus.ArmorClass;
			actor.MaxHp += bonus.MaxHp;
			actor.MaxMp += bonus.MaxMp;
			actor.D.HealthRegenFlat += bonus.HealthRegen;
			actor.D.ManaRegen += bonus.ManaRegen;
			actor.D.MagicResist += bonus.MagicResist;
			actor.D.ResistWater += bonus.ResistWater;
			actor.D.ResistWind += bonus.ResistWind;
			actor.D.ResistFire += bonus.ResistFire;
			actor.D.ResistEarth += bonus.ResistEarth;
		}
		return 0.0;
	}

	public static string MorphId(IGameData data, Combatant actor)
	{
		SetSnapshot snapshot = Evaluate(data, actor);
		return (from definition in ActiveSets(data, snapshot)
			select definition.MorphName).FirstOrDefault((string name) => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;
	}

	private static IEnumerable<L1jArmorSetDefinition> ActiveSets(IGameData data, SetSnapshot snapshot)
	{
		return L1jArmorSetCatalog.Load(data).Sets.Where((L1jArmorSetDefinition definition) => snapshot.Active(definition.Code, definition.ItemIds.Count));
	}

	private static IEnumerable<(string Slot, ItemStack Stack)> Equipped(Combatant actor)
	{
		string key;
		if (actor.EquippedItems.Count > 0)
		{
			foreach (KeyValuePair<string, ItemStack> equippedItem in actor.EquippedItems)
			{
				equippedItem.Deconstruct(out key, out var value);
				string item = key;
				ItemStack item2 = value;
				yield return (Slot: item, Stack: item2);
			}
			yield break;
		}
		foreach (KeyValuePair<string, object> item3 in actor.Equip)
		{
			item3.Deconstruct(out key, out var value2);
			string text = key;
			if (value2 is string { Length: >0 } text2)
			{
				yield return (Slot: text, Stack: new ItemStack("set:" + text, text2, 1L));
			}
		}
	}

	private static int ReadInt(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}
}
