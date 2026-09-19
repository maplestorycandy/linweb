using System;

namespace IdleLineage.Combat;

public static class CombatWallet
{
	public const string GoldItemKey = "gold";

	public static ItemStack VirtualStack(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return new ItemStack(owner.Key + ":wallet:gold", "gold", Balance(owner))
		{
			Locked = true
		};
	}

	public static long Balance(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return Math.Max(0L, owner.Gold);
	}

	public static long Add(Combatant owner, long amount)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		long num = Balance(owner);
		if (amount <= 0)
		{
			owner.Gold = num;
			return num;
		}
		owner.Gold = ((num > long.MaxValue - amount) ? long.MaxValue : (num + amount));
		return owner.Gold;
	}

	public static bool TrySpend(Combatant owner, long amount)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (amount <= 0)
		{
			throw new ArgumentOutOfRangeException("amount");
		}
		long num = Balance(owner);
		if (num < amount)
		{
			return false;
		}
		owner.Gold = num - amount;
		return true;
	}

	public static bool TryCharge(Combatant owner, long amount)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException("amount");
		}
		if (amount != 0L)
		{
			return TrySpend(owner, amount);
		}
		return true;
	}

	public static bool TryTransfer(Combatant source, Combatant destination, long amount)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(destination, "destination");
		if (amount <= 0)
		{
			throw new ArgumentOutOfRangeException("amount");
		}
		long num = Balance(source);
		if (num < amount)
		{
			return false;
		}
		if (source == destination)
		{
			return true;
		}
		long num2 = Balance(destination);
		if (num2 > long.MaxValue - amount)
		{
			return false;
		}
		source.Gold = num - amount;
		destination.Gold = num2 + amount;
		return true;
	}
}
