using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class BalrogRoomRules
{
	public static string MapKey => "balrog_room";

	public static string RequiredItemKey => "mat_desire_element_ruler";

	public static int RequiredItemCount => 1;

	public static bool TryLoadCatalog(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (data.Item(RequiredItemKey) != null && data.Maps.ContainsKey(MapKey))
		{
			JsonObject jsonObject = data.Mob("l1j_45752");
			if (jsonObject != null)
			{
				return CombatSkill.ReadSystemBossFlag(jsonObject);
			}
		}
		return false;
	}

	public static bool TryEnter(IGameData data, Combatant player, out BalrogRoomEntryFailure failure)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(player, "player");
		if (!TryLoadCatalog(data))
		{
			failure = BalrogRoomEntryFailure.InvalidCatalog;
			return false;
		}
		if (player.Kind != CombatantKind.Player)
		{
			failure = BalrogRoomEntryFailure.InvalidPlayer;
			return false;
		}
		if (CombatInventory.Count(player, RequiredItemKey) < RequiredItemCount)
		{
			failure = BalrogRoomEntryFailure.MissingPass;
			return false;
		}
		failure = BalrogRoomEntryFailure.None;
		return true;
	}
}
