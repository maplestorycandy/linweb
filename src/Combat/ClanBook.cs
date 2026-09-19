using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class ClanBook
{
	private readonly Dictionary<string, ClanRank> _members = new Dictionary<string, ClanRank>(StringComparer.Ordinal);

	private byte[] _emblem = Array.Empty<byte>();

	public string Name { get; private set; } = string.Empty;

	public string LeaderKey { get; private set; } = string.Empty;

	public int HouseId { get; private set; }

	public long HouseTaxDeadlineUnixMilliseconds { get; private set; }

	public bool HouseBasementPurchased { get; private set; }

	public long HouseSalePrice { get; private set; }

	public long HouseSaleDeadlineUnixMilliseconds { get; private set; }

	public int PendingHouseId { get; private set; }

	public long PendingHouseBid { get; private set; }

	public long PendingHouseAuctionDeadlineUnixMilliseconds { get; private set; }

	public long CreatedAtUnixMilliseconds { get; private set; }

	public WarehouseState Warehouse { get; private set; } = new WarehouseState("clan", 200);

	public bool Exists => Name.Length > 0;

	public bool OwnsHouse => HouseId != 0;

	public bool HouseOnSale
	{
		get
		{
			if (OwnsHouse)
			{
				return HouseSaleDeadlineUnixMilliseconds > 0;
			}
			return false;
		}
	}

	public bool HouseBidPending => PendingHouseId != 0;

	public int MemberCount => _members.Count;

	public ReadOnlyMemory<byte> Emblem => _emblem;

	public string DisplayName => Name;

	public bool IsLeader(string identity)
	{
		if (Exists && LeaderKey.Length > 0)
		{
			return string.Equals(LeaderKey, Normalize(identity), StringComparison.Ordinal);
		}
		return false;
	}

	public IReadOnlyList<ClanMemberInfo> Members()
	{
		List<ClanMemberInfo> list = new List<ClanMemberInfo>(_members.Count);
		foreach (var (identity, rank) in _members)
		{
			list.Add(new ClanMemberInfo(identity, rank));
		}
		list.Sort((ClanMemberInfo a, ClanMemberInfo b) => string.CompareOrdinal(a.Identity, b.Identity));
		return list;
	}

	public ClanMemberInfo? Member(string identity)
	{
		string text = Normalize(identity);
		if (text.Length != 0 && _members.TryGetValue(text, out var value))
		{
			return new ClanMemberInfo(text, value);
		}
		return null;
	}

	public bool Sync(string identity)
	{
		string text = Normalize(identity);
		if (!Exists || text.Length == 0 || _members.ContainsKey(text))
		{
			return false;
		}
		_members.Add(text, IsLeader(text) ? ClanRank.Prince : ClanRank.Public);
		return true;
	}

	public ClanCreateResult TryCreate(Combatant founder, string identity, string name, long nowUnixMilliseconds)
	{
		ArgumentNullException.ThrowIfNull(founder, "founder");
		string text = Normalize(identity);
		if (text.Length == 0)
		{
			return ClanCreateResult.Failed(ClanFailure.InvalidIdentity);
		}
		if (!string.Equals(founder.ClassId, "royal", StringComparison.Ordinal))
		{
			return ClanCreateResult.Failed(ClanFailure.NotRoyal);
		}
		if (Exists)
		{
			return ClanCreateResult.Failed(ClanFailure.AlreadyExists);
		}
		string text2 = ClanRules.NormalizeName(name);
		if (text2.Length == 0)
		{
			return ClanCreateResult.Failed(ClanFailure.InvalidName);
		}
		if (founder.Gold < 30000)
		{
			return ClanCreateResult.Failed(ClanFailure.InsufficientGold);
		}
		founder.Gold -= 30000L;
		Name = text2;
		LeaderKey = text;
		ClearOwnedHouse();
		ClearPendingBid();
		CreatedAtUnixMilliseconds = nowUnixMilliseconds;
		_members.Clear();
		_members.Add(text, ClanRank.Prince);
		_emblem = Array.Empty<byte>();
		Warehouse = new WarehouseState("clan", 200);
		return new ClanCreateResult(Success: true, ClanFailure.None, text2, 30000L);
	}

	public ClanRankResult TrySetRank(string actorIdentity, string memberIdentity, ClanRank rank)
	{
		if (!Exists)
		{
			return ClanRankResult.Failed(ClanRankFailure.NoClan);
		}
		if (!IsLeader(actorIdentity))
		{
			return ClanRankResult.Failed(ClanRankFailure.NotLeader);
		}
		string text = Normalize(memberIdentity);
		if (!_members.ContainsKey(text))
		{
			return ClanRankResult.Failed(ClanRankFailure.MemberNotFound);
		}
		if (IsLeader(text))
		{
			return ClanRankResult.Failed(ClanRankFailure.CannotChangeLeader);
		}
		if ((rank < ClanRank.Probation || rank > ClanRank.Guardian) ? true : false)
		{
			return ClanRankResult.Failed(ClanRankFailure.InvalidRank);
		}
		_members[text] = rank;
		return new ClanRankResult(Success: true, ClanRankFailure.None);
	}

	public bool TrySetEmblem(string memberIdentity, ReadOnlySpan<byte> bytes)
	{
		string key = Normalize(memberIdentity);
		if (!Exists || !_members.ContainsKey(key) || bytes.Length != 384)
		{
			return false;
		}
		_emblem = bytes.ToArray();
		return true;
	}

	public ClanHouseResult TryBidHouse(Combatant bidder, string identity, int houseId, long amount, long minimumBid, long nowUnixMilliseconds)
	{
		ArgumentNullException.ThrowIfNull(bidder, "bidder");
		if (!Exists)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoClan);
		}
		if (!IsLeader(identity))
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NotLeader);
		}
		if (!string.Equals(bidder.ClassId, "royal", StringComparison.Ordinal))
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NotRoyal);
		}
		if (bidder.Level < 15)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.LevelTooLow);
		}
		if (OwnsHouse)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.AlreadyOwnsHouse);
		}
		if (HouseBidPending)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.BidAlreadyPending);
		}
		if (houseId <= 0)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.InvalidHouse);
		}
		if (amount < minimumBid || amount > 2000000000)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.InvalidAmount);
		}
		if (bidder.Gold < amount)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.InsufficientGold);
		}
		bidder.Gold -= amount;
		PendingHouseId = houseId;
		PendingHouseBid = amount;
		PendingHouseAuctionDeadlineUnixMilliseconds = checked(nowUnixMilliseconds + 432000000);
		return new ClanHouseResult(Success: true, ClanHouseFailure.None, houseId, amount, PendingHouseAuctionDeadlineUnixMilliseconds);
	}

	public ClanHouseResult TryPayHouseTax(Combatant payer, string identity, long nowUnixMilliseconds)
	{
		ArgumentNullException.ThrowIfNull(payer, "payer");
		if (!Exists)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoClan);
		}
		if ((object)Member(identity) == null)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoClan);
		}
		if (!OwnsHouse)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoHouse);
		}
		if (payer.Gold < 2000)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.InsufficientGold);
		}
		payer.Gold -= 2000L;
		HouseTaxDeadlineUnixMilliseconds = checked(nowUnixMilliseconds + 864000000);
		return new ClanHouseResult(Success: true, ClanHouseFailure.None, HouseId, 2000L, HouseTaxDeadlineUnixMilliseconds);
	}

	public ClanHouseResult TryListHouseForSale(string identity, long askingPrice, long nowUnixMilliseconds)
	{
		if (!Exists)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoClan);
		}
		if (!IsLeader(identity))
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NotLeader);
		}
		if (!OwnsHouse)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoHouse);
		}
		if (HouseOnSale)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.HouseAlreadyOnSale);
		}
		if (askingPrice < 100000 || askingPrice > 2000000000)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.InvalidAmount);
		}
		HouseSalePrice = askingPrice;
		HouseSaleDeadlineUnixMilliseconds = checked(nowUnixMilliseconds + 432000000);
		return new ClanHouseResult(Success: true, ClanHouseFailure.None, HouseId, 0L, HouseSaleDeadlineUnixMilliseconds);
	}

	public ClanHouseResult TryPurchaseBasement(Combatant buyer, string identity, bool basementAvailable)
	{
		ArgumentNullException.ThrowIfNull(buyer, "buyer");
		if (!Exists)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoClan);
		}
		if (!IsLeader(identity))
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NotLeader);
		}
		if (!string.Equals(buyer.ClassId, "royal", StringComparison.Ordinal))
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NotRoyal);
		}
		if (!OwnsHouse)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.NoHouse);
		}
		if (!basementAvailable)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.BasementUnavailable);
		}
		if (HouseBasementPurchased)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.BasementAlreadyPurchased);
		}
		if (buyer.Gold < 5000000)
		{
			return ClanHouseResult.Failed(ClanHouseFailure.InsufficientGold);
		}
		buyer.Gold -= 5000000L;
		HouseBasementPurchased = true;
		return new ClanHouseResult(Success: true, ClanHouseFailure.None, HouseId, 5000000L, HouseTaxDeadlineUnixMilliseconds);
	}

	public ClanHouseSettleResult SettleHouse(long nowUnixMilliseconds)
	{
		bool changed = false;
		bool acquiredHouse = false;
		bool lostForUnpaidTax = false;
		bool saleExpired = false;
		int houseId = 0;
		checked
		{
			if (HouseBidPending && PendingHouseAuctionDeadlineUnixMilliseconds <= nowUnixMilliseconds)
			{
				houseId = PendingHouseId;
				HouseId = PendingHouseId;
				HouseTaxDeadlineUnixMilliseconds = nowUnixMilliseconds + 864000000;
				HouseBasementPurchased = false;
				ClearPendingBid();
				changed = true;
				acquiredHouse = true;
			}
			if (HouseOnSale && HouseSaleDeadlineUnixMilliseconds <= nowUnixMilliseconds)
			{
				houseId = HouseId;
				HouseSalePrice = 0L;
				HouseSaleDeadlineUnixMilliseconds = 0L;
				HouseTaxDeadlineUnixMilliseconds = nowUnixMilliseconds + 864000000;
				changed = true;
				saleExpired = true;
			}
			if (OwnsHouse && !HouseOnSale && HouseTaxDeadlineUnixMilliseconds > 0 && HouseTaxDeadlineUnixMilliseconds <= nowUnixMilliseconds)
			{
				houseId = HouseId;
				ClearOwnedHouse();
				changed = true;
				lostForUnpaidTax = true;
			}
			return new ClanHouseSettleResult(changed, acquiredHouse, lostForUnpaidTax, saleExpired, houseId);
		}
	}

	private void ClearPendingBid()
	{
		PendingHouseId = 0;
		PendingHouseBid = 0L;
		PendingHouseAuctionDeadlineUnixMilliseconds = 0L;
	}

	private void ClearOwnedHouse()
	{
		HouseId = 0;
		HouseTaxDeadlineUnixMilliseconds = 0L;
		HouseBasementPurchased = false;
		HouseSalePrice = 0L;
		HouseSaleDeadlineUnixMilliseconds = 0L;
	}

	public bool Forget(string identity)
	{
		string text = Normalize(identity);
		if (text.Length == 0 || !Exists)
		{
			return false;
		}
		if (IsLeader(text))
		{
			Dissolve();
			return true;
		}
		return _members.Remove(text);
	}

	public void Dissolve()
	{
		Name = string.Empty;
		LeaderKey = string.Empty;
		ClearOwnedHouse();
		ClearPendingBid();
		CreatedAtUnixMilliseconds = 0L;
		_members.Clear();
		_emblem = Array.Empty<byte>();
		Warehouse = new WarehouseState("clan", 200);
	}

	public ClanBookSave Capture()
	{
		ClanBookSave clanBookSave = new ClanBookSave
		{
			Name = Name,
			LeaderKey = LeaderKey,
			HouseId = HouseId,
			HouseTaxDeadlineUnixMilliseconds = HouseTaxDeadlineUnixMilliseconds,
			HouseBasementPurchased = HouseBasementPurchased,
			HouseSalePrice = HouseSalePrice,
			HouseSaleDeadlineUnixMilliseconds = HouseSaleDeadlineUnixMilliseconds,
			PendingHouseId = PendingHouseId,
			PendingHouseBid = PendingHouseBid,
			PendingHouseAuctionDeadlineUnixMilliseconds = PendingHouseAuctionDeadlineUnixMilliseconds,
			CreatedAtUnixMilliseconds = CreatedAtUnixMilliseconds,
			Warehouse = WarehouseSave.Capture(Warehouse),
			Emblem = _emblem.ToArray()
		};
		foreach (var (identity, rank) in _members)
		{
			clanBookSave.Members.Add(new ClanMemberSave
			{
				Identity = identity,
				Rank = rank
			});
		}
		return clanBookSave;
	}

	public static bool TryRestore(IGameData data, ClanBookSave? save, out ClanBook book)
	{
		book = new ClanBook();
		if (save == null)
		{
			return false;
		}
		if (save.Version != 5)
		{
			return false;
		}
		string text = ClanRules.NormalizeName(save.Name);
		if (text.Length == 0)
		{
			if (save.LeaderKey.Length == 0 && save.HouseId == 0 && save.HouseTaxDeadlineUnixMilliseconds == 0L && !save.HouseBasementPurchased && save.HouseSalePrice == 0L && save.HouseSaleDeadlineUnixMilliseconds == 0L && save.PendingHouseId == 0 && save.PendingHouseBid == 0L && save.PendingHouseAuctionDeadlineUnixMilliseconds == 0L && save.CreatedAtUnixMilliseconds == 0L)
			{
				byte[] emblem = save.Emblem;
				if (emblem != null && emblem.Length == 0)
				{
					return save.Members.Count == 0;
				}
			}
			return false;
		}
		book.Name = text;
		book.LeaderKey = Normalize(save.LeaderKey);
		bool num = save.HouseId >= 0 && save.HouseTaxDeadlineUnixMilliseconds >= 0 && save.HouseSalePrice >= 0 && save.HouseSaleDeadlineUnixMilliseconds >= 0 && (save.HouseId != 0 || (save.HouseTaxDeadlineUnixMilliseconds == 0L && !save.HouseBasementPurchased && save.HouseSalePrice == 0L && save.HouseSaleDeadlineUnixMilliseconds == 0L)) && save.HouseSalePrice == 0 == (save.HouseSaleDeadlineUnixMilliseconds == 0);
		bool flag = save.PendingHouseId >= 0 && save.PendingHouseBid >= 0 && save.PendingHouseAuctionDeadlineUnixMilliseconds >= 0 && ((save.PendingHouseId == 0 && save.PendingHouseBid == 0L && save.PendingHouseAuctionDeadlineUnixMilliseconds == 0L) || (save.PendingHouseId > 0 && save.PendingHouseBid >= 100000 && save.PendingHouseBid <= 2000000000 && save.PendingHouseAuctionDeadlineUnixMilliseconds > 0));
		if (!num || !flag)
		{
			return false;
		}
		book.HouseId = save.HouseId;
		book.HouseTaxDeadlineUnixMilliseconds = save.HouseTaxDeadlineUnixMilliseconds;
		book.HouseBasementPurchased = save.HouseBasementPurchased;
		book.HouseSalePrice = save.HouseSalePrice;
		book.HouseSaleDeadlineUnixMilliseconds = save.HouseSaleDeadlineUnixMilliseconds;
		book.PendingHouseId = save.PendingHouseId;
		book.PendingHouseBid = save.PendingHouseBid;
		book.PendingHouseAuctionDeadlineUnixMilliseconds = save.PendingHouseAuctionDeadlineUnixMilliseconds;
		book.CreatedAtUnixMilliseconds = Math.Max(0L, save.CreatedAtUnixMilliseconds);
		bool flag2 = save.Emblem == null;
		if (!flag2)
		{
			int num2 = save.Emblem.Length;
			bool flag3 = ((num2 == 0 || num2 == 384) ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			return false;
		}
		book._emblem = save.Emblem.ToArray();
		foreach (ClanMemberSave member in save.Members)
		{
			string text2 = Normalize(member.Identity);
			if (text2.Length == 0 || !Enum.IsDefined(member.Rank) || !book._members.TryAdd(text2, member.Rank))
			{
				return false;
			}
		}
		string restoredLeaderKey = book.LeaderKey;
		if (restoredLeaderKey.Length == 0 || !book._members.TryGetValue(restoredLeaderKey, out var value) || value != ClanRank.Prince || book._members.Any<KeyValuePair<string, ClanRank>>((KeyValuePair<string, ClanRank> pair) => !string.Equals(pair.Key, restoredLeaderKey, StringComparison.Ordinal) && pair.Value == ClanRank.Prince))
		{
			return false;
		}
		book.Warehouse = (string.IsNullOrWhiteSpace(save.Warehouse) ? new WarehouseState("clan", 200) : WarehouseSave.Restore(data, save.Warehouse));
		return true;
	}

	private static string Normalize(string? identity)
	{
		if (!string.IsNullOrWhiteSpace(identity))
		{
			return identity.Trim();
		}
		return string.Empty;
	}
}
