using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class ClanBookSave
{
	public const int CurrentVersion = 5;

	public int Version { get; set; } = 5;

	public string Name { get; set; } = string.Empty;

	public string LeaderKey { get; set; } = string.Empty;

	public int HouseId { get; set; }

	public long HouseTaxDeadlineUnixMilliseconds { get; set; }

	public bool HouseBasementPurchased { get; set; }

	public long HouseSalePrice { get; set; }

	public long HouseSaleDeadlineUnixMilliseconds { get; set; }

	public int PendingHouseId { get; set; }

	public long PendingHouseBid { get; set; }

	public long PendingHouseAuctionDeadlineUnixMilliseconds { get; set; }

	public long CreatedAtUnixMilliseconds { get; set; }

	public string Warehouse { get; set; } = string.Empty;

	public byte[] Emblem { get; set; } = Array.Empty<byte>();

	public List<ClanMemberSave> Members { get; set; } = new List<ClanMemberSave>();
}
