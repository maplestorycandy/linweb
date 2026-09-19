namespace IdleLineage.Combat;

public static class ClanRules
{
	public const long CreateGoldCost = 30000L;

	public const int NameMaxLength = 20;

	public const int WarehouseCapacity = 200;

	public const int EmblemWidth = 16;

	public const int EmblemHeight = 12;

	public const int EmblemByteLength = 384;

	public const string WarehouseKey = "clan";

	public static string NormalizeName(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return string.Empty;
		}
		string text = raw.Trim();
		if (text.Length <= 20)
		{
			return text;
		}
		return string.Empty;
	}

	public static string RankName(ClanRank rank)
	{
		return rank switch
		{
			ClanRank.Probation => "見習", 
			ClanRank.Public => "一般", 
			ClanRank.Guardian => "守護騎士", 
			ClanRank.Prince => "聯盟君主", 
			_ => "未知", 
		};
	}

	public static string RankFailureText(ClanRankFailure failure)
	{
		return failure switch
		{
			ClanRankFailure.NoClan => "尚未建立血盟。", 
			ClanRankFailure.NotLeader => "只有聯盟君主可以調整階級。", 
			ClanRankFailure.MemberNotFound => "找不到這位血盟成員。", 
			ClanRankFailure.InvalidRank => "只能指派見習、一般或守護騎士。", 
			ClanRankFailure.CannotChangeLeader => "聯盟君主的階級固定為聯盟君主。", 
			_ => "血盟階級調整失敗。", 
		};
	}

	public static string FailureText(ClanFailure failure)
	{
		return failure switch
		{
			ClanFailure.NotRoyal => "只有王族可以創立血盟。", 
			ClanFailure.InvalidName => $"血盟名稱需為 1 至 {20} 個字。", 
			ClanFailure.AlreadyExists => "這個帳號已經創立血盟。", 
			ClanFailure.NoClan => "你尚未加入血盟。", 
			ClanFailure.NotLeader => "只有創立血盟的盟主可以執行。", 
			ClanFailure.InsufficientGold => "金幣不足。", 
			ClanFailure.InvalidIdentity => "角色識別無效。", 
			_ => "血盟操作失敗。", 
		};
	}
}
