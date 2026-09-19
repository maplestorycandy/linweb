using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ElfElementRules
{
	public const string NpcKey = "npc_elion";

	public const string ElfClassId = "elf";

	public const long ChangeCost = 100000L;

	public static IReadOnlyList<(string Key, string Name)> Elements { get; } = Array.AsReadOnly(new(string, string)[4]
	{
		("fire", "火"),
		("water", "水"),
		("earth", "地"),
		("wind", "風")
	});

	public static bool IsElf(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return string.Equals(ClassKitRegistry.NormalizeClassId(actor.ClassId), "elf", StringComparison.Ordinal);
	}

	public static bool HasChosen(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return actor.ElfElement.Length > 0;
	}

	public static bool CanChoose(Combatant actor)
	{
		if (IsElf(actor))
		{
			return !HasChosen(actor);
		}
		return false;
	}

	public static string DisplayName(string elementKey)
	{
		foreach (var (a, result) in Elements)
		{
			if (string.Equals(a, elementKey, StringComparison.Ordinal))
			{
				return result;
			}
		}
		return elementKey;
	}

	public static bool TryChoose(Combatant actor, string elementKey, IGameData data, out ElfElementFailure failure)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!IsElf(actor))
		{
			failure = ElfElementFailure.NotElf;
			return false;
		}
		if (HasChosen(actor))
		{
			failure = ElfElementFailure.AlreadyChosen;
			return false;
		}
		if (!Elements.Any<(string, string)>(((string Key, string Name) element) => string.Equals(element.Key, elementKey, StringComparison.Ordinal)))
		{
			failure = ElfElementFailure.UnknownElement;
			return false;
		}
		actor.ElfElement = elementKey;
		CombatantBuilder.RefreshPlayer(actor, data);
		failure = ElfElementFailure.None;
		return true;
	}

	public static bool TryChange(Combatant actor, string elementKey, IGameData data, out ElfElementFailure failure)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!IsElf(actor))
		{
			failure = ElfElementFailure.NotElf;
			return false;
		}
		if (!HasChosen(actor))
		{
			failure = ElfElementFailure.NotChosenYet;
			return false;
		}
		if (!Elements.Any<(string, string)>(((string Key, string Name) element) => string.Equals(element.Key, elementKey, StringComparison.Ordinal)))
		{
			failure = ElfElementFailure.UnknownElement;
			return false;
		}
		if (string.Equals(actor.ElfElement, elementKey, StringComparison.Ordinal))
		{
			failure = ElfElementFailure.SameElement;
			return false;
		}
		if (!CombatWallet.TryCharge(actor, 100000L))
		{
			failure = ElfElementFailure.NotEnoughGold;
			return false;
		}
		actor.ElfElement = elementKey;
		CombatantBuilder.RefreshPlayer(actor, data);
		failure = ElfElementFailure.None;
		return true;
	}
}
