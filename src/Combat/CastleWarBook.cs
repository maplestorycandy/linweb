using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class CastleWarBook
{
	private readonly Dictionary<int, CastleWarCastleSave> _castles;

	public CastleWarAttemptSave? Active { get; private set; }

	public IReadOnlyDictionary<int, CastleWarCastleSave> Castles => _castles;

	public CastleWarBook(CastleWarBookSave? save = null)
	{
		_castles = CastleWarRules.Castles.ToDictionary((CastleWarDefinition definition) => definition.Id, (CastleWarDefinition definition) => Sanitize(save?.Castles.FirstOrDefault((CastleWarCastleSave row) => row.CastleId == definition.Id) ?? new CastleWarCastleSave
		{
			CastleId = definition.Id
		}));
		Active = Sanitize(save?.Active);
		if (Active != null && !_castles.ContainsKey(Active.CastleId))
		{
			Active = null;
		}
	}

	public CastleWarCastleSave State(int castleId)
	{
		return _castles[castleId];
	}

	public string OwnerName(int castleId)
	{
		CastleWarCastleSave castleWarCastleSave = State(castleId);
		object obj;
		if (castleWarCastleSave.OwnerDisplayName.Length <= 0)
		{
			obj = CastleWarRules.Find(castleId)?.DefaultOwnerName;
			if (obj == null)
			{
				return "守備軍";
			}
		}
		else
		{
			obj = castleWarCastleSave.OwnerDisplayName;
		}
		return (string)obj;
	}

	public bool IsOwner(int castleId, string identity)
	{
		if (identity.Length > 0)
		{
			return string.Equals(State(castleId).OwnerIdentity, identity, StringComparison.Ordinal);
		}
		return false;
	}

	public bool TryStart(int castleId, int playerLevel, string attackerIdentity, string attackerDisplayName, out string message)
	{
		if (!_castles.TryGetValue(castleId, out CastleWarCastleSave value))
		{
			message = "找不到城堡資料。";
			return false;
		}
		if (Active != null)
		{
			message = "已有攻城戰正在進行。";
			return false;
		}
		if (playerLevel < 25)
		{
			message = $"角色必須達到 {25} 級。";
			return false;
		}
		if (attackerIdentity.Length == 0)
		{
			message = "角色身分無效。";
			return false;
		}
		if (IsOwner(castleId, attackerIdentity))
		{
			message = "這座城已由你持有。";
			return false;
		}
		if (_castles.Values.Any((CastleWarCastleSave row) => string.Equals(row.OwnerIdentity, attackerIdentity, StringComparison.Ordinal)))
		{
			message = "同一個角色或血盟只能持有一座城。";
			return false;
		}
		if (value.RetryCooldownSeconds > 0.0)
		{
			message = "敗戰整備尚餘 " + FormatDuration(value.RetryCooldownSeconds) + "。";
			return false;
		}
		if (value.ProtectionSeconds > 0.0 && value.OwnerIdentity.Length > 0)
		{
			message = "城堡保護期尚餘 " + FormatDuration(value.ProtectionSeconds) + "。";
			return false;
		}
		Active = new CastleWarAttemptSave
		{
			CastleId = castleId,
			AttackerIdentity = attackerIdentity,
			AttackerDisplayName = ((attackerDisplayName.Trim().Length > 0) ? attackerDisplayName.Trim() : attackerIdentity),
			RemainingSeconds = 1800.0
		};
		message = CastleWarRules.Find(castleId).Name + "攻城開始，限時 30 分鐘。";
		return true;
	}

	public bool Tick(double delta, out string? failureMessage)
	{
		failureMessage = null;
		if (!double.IsFinite(delta) || delta <= 0.0)
		{
			return false;
		}
		bool result = false;
		foreach (CastleWarCastleSave value in _castles.Values)
		{
			double num = Math.Max(0.0, value.RetryCooldownSeconds - delta);
			double num2 = Math.Max(0.0, value.ProtectionSeconds - delta);
			if (num != value.RetryCooldownSeconds || num2 != value.ProtectionSeconds)
			{
				result = true;
			}
			value.RetryCooldownSeconds = num;
			value.ProtectionSeconds = num2;
			if (value.OwnerIdentity.Length != 0 && value.Treasury < 100000)
			{
				value.IncomeSeconds += delta;
				result = true;
				while (value.IncomeSeconds >= 14400.0 && value.Treasury < 100000)
				{
					value.IncomeSeconds -= 14400.0;
					value.Treasury = Math.Min(100000L, value.Treasury + 3000);
					result = true;
				}
			}
		}
		if (Active == null)
		{
			return result;
		}
		Active.RemainingSeconds = Math.Max(0.0, Active.RemainingSeconds - delta);
		result = true;
		if (Active.RemainingSeconds <= 0.0)
		{
			int castleId = Active.CastleId;
			Fail("攻城時間結束，攻城失敗。", out failureMessage);
			_castles[castleId].RetryCooldownSeconds = 14400.0;
		}
		return result;
	}

	public bool Fail(string reason, out string message)
	{
		if (Active == null)
		{
			message = "目前沒有進行中的攻城戰。";
			return false;
		}
		State(Active.CastleId).RetryCooldownSeconds = 14400.0;
		Active = null;
		message = reason;
		return true;
	}

	public bool IsActive(int castleId, string attackerIdentity = "")
	{
		if (Active != null && Active.CastleId == castleId)
		{
			if (attackerIdentity.Length != 0)
			{
				return string.Equals(Active.AttackerIdentity, attackerIdentity, StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	public bool IsDestroyed(string objectKey)
	{
		return Active?.DestroyedObjects.Contains(objectKey) ?? false;
	}

	public void SetHealth(string objectKey, double hp)
	{
		if (Active != null && objectKey.Length != 0 && double.IsFinite(hp))
		{
			Active.ObjectHealth[objectKey] = Math.Max(0.0, hp);
		}
	}

	public double RestoreHealth(string objectKey, double maximum)
	{
		if (Active == null || !Active.ObjectHealth.TryGetValue(objectKey, out var value) || !double.IsFinite(value))
		{
			return Math.Max(1.0, maximum);
		}
		return Math.Clamp(value, 0.0, Math.Max(1.0, maximum));
	}

	public void Destroy(string objectKey)
	{
		if (Active != null && objectKey.Length != 0)
		{
			Active.ObjectHealth.Remove(objectKey);
			Active.DestroyedObjects.Add(objectKey);
		}
	}

	public int DestroyedAdenSubTowers()
	{
		return Active?.DestroyedObjects.Count((string key) => key.StartsWith("SubTower:", StringComparison.Ordinal)) ?? 0;
	}

	public bool MainTowerAttackable(int castleId)
	{
		if (castleId == 7)
		{
			return DestroyedAdenSubTowers() >= 3;
		}
		return true;
	}

	public bool TryCapture(int castleId, string attackerIdentity, out string message)
	{
		CastleWarDefinition castleWarDefinition = CastleWarRules.Find(castleId);
		if ((object)castleWarDefinition == null || Active == null || Active.CastleId != castleId || !string.Equals(Active.AttackerIdentity, attackerIdentity, StringComparison.Ordinal))
		{
			message = "目前不能占領這座城。";
			return false;
		}
		string mainPrefix = "MainTower:";
		if (!Active.DestroyedObjects.Any((string key) => key.StartsWith(mainPrefix, StringComparison.Ordinal)))
		{
			message = "必須先摧毀守護者之塔。";
			return false;
		}
		CastleWarCastleSave castleWarCastleSave = State(castleId);
		castleWarCastleSave.OwnerIdentity = Active.AttackerIdentity;
		castleWarCastleSave.OwnerDisplayName = Active.AttackerDisplayName;
		castleWarCastleSave.RetryCooldownSeconds = 0.0;
		castleWarCastleSave.ProtectionSeconds = 100800.0;
		castleWarCastleSave.IncomeSeconds = 0.0;
		Active = null;
		message = "已占領" + castleWarDefinition.Name + "。";
		return true;
	}

	public long Withdraw(int castleId, string identity)
	{
		CastleWarCastleSave castleWarCastleSave = State(castleId);
		if (!IsOwner(castleId, identity))
		{
			return 0L;
		}
		long treasury = castleWarCastleSave.Treasury;
		castleWarCastleSave.Treasury = 0L;
		return treasury;
	}

	public CastleWarBookSave Snapshot()
	{
		return new CastleWarBookSave
		{
			Version = 1,
			Castles = _castles.Values.OrderBy((CastleWarCastleSave row) => row.CastleId).ToList(),
			Active = Active
		};
	}

	public static string FormatDuration(double seconds)
	{
		int num = Math.Max(0, (int)Math.Ceiling(seconds));
		int num2 = num / 3600;
		int value = num % 3600 / 60;
		int value2 = num % 60;
		if (num2 > 0)
		{
			return $"{num2}小時{value}分";
		}
		return $"{value}分{value2}秒";
	}

	private static CastleWarCastleSave Sanitize(CastleWarCastleSave state)
	{
		CastleWarCastleSave castleWarCastleSave = state;
		if (castleWarCastleSave.OwnerIdentity == null)
		{
			string text = (castleWarCastleSave.OwnerIdentity = "");
		}
		castleWarCastleSave = state;
		if (castleWarCastleSave.OwnerDisplayName == null)
		{
			string text = (castleWarCastleSave.OwnerDisplayName = "");
		}
		state.RetryCooldownSeconds = SafeSeconds(state.RetryCooldownSeconds);
		state.ProtectionSeconds = SafeSeconds(state.ProtectionSeconds);
		state.IncomeSeconds = SafeSeconds(state.IncomeSeconds) % 14400.0;
		state.Treasury = Math.Clamp(state.Treasury, 0L, 100000L);
		return state;
	}

	private static CastleWarAttemptSave? Sanitize(CastleWarAttemptSave? active)
	{
		if (active == null || active.AttackerIdentity == null || active.AttackerIdentity.Length == 0)
		{
			return null;
		}
		if (active.AttackerDisplayName == null)
		{
			string text = (active.AttackerDisplayName = active.AttackerIdentity);
		}
		active.RemainingSeconds = Math.Clamp(SafeSeconds(active.RemainingSeconds), 0.0, 1800.0);
		if (active.RemainingSeconds <= 0.0)
		{
			return null;
		}
		active.DestroyedObjects = ((active.DestroyedObjects == null) ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(active.DestroyedObjects, StringComparer.Ordinal));
		active.ObjectHealth = ((active.ObjectHealth == null) ? new Dictionary<string, double>(StringComparer.Ordinal) : active.ObjectHealth.Where<KeyValuePair<string, double>>((KeyValuePair<string, double> row) => row.Key.Length > 0 && double.IsFinite(row.Value) && row.Value > 0.0).ToDictionary<KeyValuePair<string, double>, string, double>((KeyValuePair<string, double> row) => row.Key, (KeyValuePair<string, double> row) => row.Value, StringComparer.Ordinal));
		return active;
	}

	private static double SafeSeconds(double value)
	{
		if (!double.IsFinite(value) || !(value > 0.0))
		{
			return 0.0;
		}
		return value;
	}
}
