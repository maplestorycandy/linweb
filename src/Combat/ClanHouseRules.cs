namespace IdleLineage.Combat;

public static class ClanHouseRules
{
	public const int MinimumLeaderLevel = 15;

	public const long InitialAuctionPrice = 100000L;

	public const long MaximumPrice = 2000000000L;

	public const long TaxAdena = 2000L;

	public const long BasementAdena = 5000000L;

	public const long AuctionMilliseconds = 432000000L;

	public const long TaxIntervalMilliseconds = 864000000L;

	public const int SellerPaymentPercent = 90;

	public static string FailureText(ClanHouseFailure failure)
	{
		return failure switch
		{
			ClanHouseFailure.NoClan => "尚未建立血盟。", 
			ClanHouseFailure.NotLeader => "只有血盟盟主可以執行此操作。", 
			ClanHouseFailure.NotRoyal => "只有王族盟主可以執行此操作。", 
			ClanHouseFailure.LevelTooLow => "王族盟主必須達到 15 級。", 
			ClanHouseFailure.AlreadyOwnsHouse => "血盟已擁有一間小屋。", 
			ClanHouseFailure.BidAlreadyPending => "同一時間只能競標一間小屋。", 
			ClanHouseFailure.InvalidHouse => "這不是 L1J-TW-main 可運作的血盟小屋。", 
			ClanHouseFailure.InvalidAmount => "金額不符合原版拍賣範圍。", 
			ClanHouseFailure.InsufficientGold => "金幣不足。", 
			ClanHouseFailure.NoHouse => "血盟目前沒有小屋。", 
			ClanHouseFailure.HouseAlreadyOnSale => "這間小屋已經委託出售。", 
			ClanHouseFailure.BasementUnavailable => "這間小屋沒有原版地下盟屋。", 
			ClanHouseFailure.BasementAlreadyPurchased => "地下盟屋已經購買。", 
			_ => "血盟小屋操作失敗。", 
		};
	}
}
