using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class ClanStore
{
	private static readonly JsonSerializerOptions CurrentJsonOptions = new JsonSerializerOptions
	{
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	private const string StorePath = "user://clan.json";

	private const long SettleIntervalMilliseconds = 60000L;

	private const long LeaderCacheMilliseconds = 3000L;

	private static ClanBook? _book;

	private static long _lastSettleUnixMs;

	private static long _leaderCacheAtMs;

	private static bool _leaderCacheValue;

	private static bool _leaderCacheValid;

	public static ClanBook Book
	{
		get
		{
			EnsureLoaded();
			return _book;
		}
	}

	public static bool Save()
	{
		EnsureLoaded();
		_leaderCacheValid = false;
		try
		{
			string text = JsonSerializer.Serialize(_book.Capture(), CurrentJsonOptions);
			string path = "user://clan.json.tmp";
			using (Godot.FileAccess fileAccess = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write))
			{
				if (fileAccess == null)
				{
					GD.PushWarning($"[ClanStore] 無法開檔寫入（{Godot.FileAccess.GetOpenError()}）");
					return false;
				}
				fileAccess.StoreString(text);
			}
			File.Move(ProjectSettings.GlobalizePath(path), ProjectSettings.GlobalizePath("user://clan.json"), overwrite: true);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushWarning("[ClanStore] 帳本寫入失敗（保留上一份）：" + ex.Message);
			return false;
		}
	}

	public static bool Forget(string identity)
	{
		if (string.IsNullOrWhiteSpace(identity))
		{
			return false;
		}
		EnsureLoaded();
		bool result = _book.IsLeader(identity);
		if (_book.Forget(identity))
		{
			Save();
		}
		return result;
	}

	public static void Reload()
	{
		_book = null;
		_lastSettleUnixMs = 0L;
		_leaderCacheValid = false;
	}

	public static long NowUnixMilliseconds()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	public static void Sync(GameSession? session)
	{
		if (session != null)
		{
			EnsureLoaded();
			if (_book.Sync(session.Identity))
			{
				Save();
			}
		}
	}

	public static string? Tick(GameSession? session)
	{
		if (session == null)
		{
			return null;
		}
		EnsureLoaded();
		if (!_book.Exists)
		{
			return null;
		}
		long num = NowUnixMilliseconds();
		if (_lastSettleUnixMs != 0L && num - _lastSettleUnixMs < 60000)
		{
			return null;
		}
		_lastSettleUnixMs = num;
		ClanHouseSettleResult clanHouseSettleResult = _book.SettleHouse(num);
		if (!clanHouseSettleResult.Changed)
		{
			return null;
		}
		Save();
		if (clanHouseSettleResult.AcquiredHouse)
		{
			return "血盟小屋競標期結束，血盟已取得小屋。";
		}
		if (clanHouseSettleResult.LostForUnpaidTax)
		{
			return "血盟小屋因逾期未繳稅而被收回拍賣。";
		}
		if (clanHouseSettleResult.SaleExpired)
		{
			return "血盟小屋出售期結束；無人投標，小屋仍歸血盟所有。";
		}
		return null;
	}

	public static bool LeaderPresent(GameSession? session)
	{
		EnsureLoaded();
		if (!_book.Exists || _book.LeaderKey.Length == 0)
		{
			return false;
		}
		if (session != null && _book.IsLeader(session.Identity) && string.Equals(session.Player.ClassId, "royal", StringComparison.Ordinal))
		{
			return true;
		}
		long ticksMsec = (long)Time.GetTicksMsec();
		if (_leaderCacheValid && ticksMsec - _leaderCacheAtMs < 3000)
		{
			return _leaderCacheValue;
		}
		bool flag = false;
		SaveManager.SlotInfo[] array = SaveManager.ReadAllSlots();
		for (int i = 0; i < array.Length; i++)
		{
			SaveManager.SlotInfo slotInfo = array[i];
			if (!slotInfo.Empty && string.Equals(slotInfo.Identity, _book.LeaderKey, StringComparison.Ordinal))
			{
				flag = string.Equals(slotInfo.ClassName, "王族", StringComparison.Ordinal);
				break;
			}
		}
		_leaderCacheAtMs = ticksMsec;
		_leaderCacheValue = flag;
		_leaderCacheValid = true;
		return flag;
	}

	public static double LeaderCharisma(GameSession? session)
	{
		EnsureLoaded();
		if (!_book.Exists || _book.LeaderKey.Length == 0)
		{
			return 0.0;
		}
		if (session != null && _book.IsLeader(session.Identity))
		{
			return Math.Max(0.0, session.Player.D.Cha);
		}
		SaveManager.SlotInfo[] array = SaveManager.ReadAllSlots();
		for (int i = 0; i < array.Length; i++)
		{
			SaveManager.SlotInfo slotInfo = array[i];
			if (!slotInfo.Empty && string.Equals(slotInfo.Identity, _book.LeaderKey, StringComparison.Ordinal))
			{
				return Math.Max(0.0, slotInfo.Cha);
			}
		}
		return 0.0;
	}

	public static string LeaderDisplayName(GameSession? session)
	{
		EnsureLoaded();
		if (!_book.Exists)
		{
			return string.Empty;
		}
		if (session != null && _book.IsLeader(session.Identity))
		{
			string text = session.Player.Disp.Trim();
			if (text.Length > 0)
			{
				return text;
			}
		}
		SaveManager.SlotInfo[] array = SaveManager.ReadAllSlots();
		for (int i = 0; i < array.Length; i++)
		{
			SaveManager.SlotInfo slotInfo = array[i];
			if (!slotInfo.Empty && string.Equals(slotInfo.Identity, _book.LeaderKey, StringComparison.Ordinal))
			{
				break;
			}
		}
		return "君主";
	}

	public static string MemberDisplayName(GameSession? session, string identity)
	{
		if (session != null && string.Equals(session.Identity, identity, StringComparison.Ordinal))
		{
			string text = session.Player.Disp.Trim();
			if (text.Length > 0)
			{
				return text;
			}
		}
		SaveManager.SlotInfo[] array = SaveManager.ReadAllSlots();
		for (int i = 0; i < array.Length; i++)
		{
			SaveManager.SlotInfo slotInfo = array[i];
			if (!slotInfo.Empty && string.Equals(slotInfo.Identity, identity, StringComparison.Ordinal))
			{
				return slotInfo.DisplayName;
			}
		}
		if (identity.Length > 8)
		{
			return identity.Substring(0, 8);
		}
		return identity;
	}

	public static ClanHouseSettleResult SettleHouse()
	{
		EnsureLoaded();
		ClanHouseSettleResult result = _book.SettleHouse(NowUnixMilliseconds());
		if (result.Changed)
		{
			Save();
		}
		return result;
	}

	private static void EnsureLoaded()
	{
		if (_book != null)
		{
			return;
		}
		_book = new ClanBook();
		if (!Godot.FileAccess.FileExists("user://clan.json"))
		{
			return;
		}
		try
		{
			string asText;
			using (Godot.FileAccess fileAccess = Godot.FileAccess.Open("user://clan.json", Godot.FileAccess.ModeFlags.Read))
			{
				if (fileAccess == null)
				{
					return;
				}
				asText = fileAccess.GetAsText();
			}
			if (!string.IsNullOrWhiteSpace(asText))
			{
				ClanBookSave save = JsonSerializer.Deserialize<ClanBookSave>(asText, CurrentJsonOptions);
				if (ClanBook.TryRestore(GameDataProvider.Shared, save, out ClanBook book) && HouseReferencesAreCanonical(book))
				{
					_book = book;
					return;
				}
				GD.PushWarning("[ClanStore] 血盟帳本無效");
				Quarantine();
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning("[ClanStore] 帳本讀取失敗：" + ex.Message);
			Quarantine();
		}
	}

	private static bool HouseReferencesAreCanonical(ClanBook book)
	{
		L1jHouseCatalog l1jHouseCatalog = L1jHouseCatalog.Load(GameDataProvider.Shared);
		L1jHouseDefinition value;
		bool num = book.HouseId == 0 || (l1jHouseCatalog.ById.TryGetValue(book.HouseId, out value) && (value?.Operational ?? false));
		L1jHouseDefinition value2;
		bool flag = book.PendingHouseId == 0 || (l1jHouseCatalog.ById.TryGetValue(book.PendingHouseId, out value2) && (value2?.Operational ?? false));
		return num && flag;
	}

	private static void Quarantine()
	{
		try
		{
			string text = "user://clan.json.bad";
			File.Move(ProjectSettings.GlobalizePath("user://clan.json"), ProjectSettings.GlobalizePath(text), overwrite: true);
			GD.PushWarning("[ClanStore] 原檔已保留為 " + text + "（以未創盟開局）");
		}
		catch (Exception ex)
		{
			GD.PushWarning("[ClanStore] 原檔保留失敗：" + ex.Message);
		}
	}
}
