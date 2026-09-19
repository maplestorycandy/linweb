using System;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class HarborFerryRules
{
	public const int MinimumLevel = 5;

	public const long Fare = 100L;

	public static bool TryTravel(Combatant player, string currentTownKey, string npcId, out HarborFerryRoute? destination, out HarborFerryFailure failure)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		ArgumentException.ThrowIfNullOrWhiteSpace(currentTownKey, "currentTownKey");
		ArgumentException.ThrowIfNullOrWhiteSpace(npcId, "npcId");
		destination = HarborFerryCatalog.FindByNpc(npcId);
		if ((object)destination == null)
		{
			failure = HarborFerryFailure.UnknownRoute;
			return false;
		}
		if (!string.Equals(currentTownKey, destination.OriginTownKey, StringComparison.Ordinal))
		{
			failure = HarborFerryFailure.WrongDepartureTown;
			return false;
		}
		if (player.Kind != CombatantKind.Player)
		{
			failure = HarborFerryFailure.InvalidPlayer;
			return false;
		}
		if (player.Level < 5)
		{
			failure = HarborFerryFailure.LevelTooLow;
			return false;
		}
		if (player.IsHardControlled)
		{
			failure = HarborFerryFailure.Incapacitated;
			return false;
		}
		if (!CombatWallet.TrySpend(player, 100L))
		{
			failure = HarborFerryFailure.InsufficientGold;
			return false;
		}
		failure = HarborFerryFailure.None;
		return true;
	}
}
