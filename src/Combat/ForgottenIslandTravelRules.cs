using System;

namespace IdleLineage.Combat;

public static class ForgottenIslandTravelRules
{
	public const int IsbaNpcId = 70086;

	public const int MinimumLevel = 45;

	public const string TicketItemKey = "l1j_item_40300";

	public const string PortServiceKey = "main_forgotten_island_ticket";

	public static bool TryBoard(Combatant player, out ForgottenIslandTravelFailure failure)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (player.Kind != CombatantKind.Player)
		{
			failure = ForgottenIslandTravelFailure.InvalidPlayer;
			return false;
		}
		if (player.IsHardControlled)
		{
			failure = ForgottenIslandTravelFailure.Incapacitated;
			return false;
		}
		if (player.Level < 45)
		{
			failure = ForgottenIslandTravelFailure.LevelTooLow;
			return false;
		}
		if (!CombatInventory.TryRemove(player, "l1j_item_40300", 1L))
		{
			failure = ForgottenIslandTravelFailure.MissingTicket;
			return false;
		}
		failure = ForgottenIslandTravelFailure.None;
		return true;
	}
}
