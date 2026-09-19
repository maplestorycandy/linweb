using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class CastleWarStore
{
	private const string StorePath = "user://castle_war.json";

	private const double SaveIntervalSeconds = 5.0;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	private static CastleWarBook? _book;

	private static double _saveCountdown;

	public static CastleWarBook Book
	{
		get
		{
			EnsureLoaded();
			return _book;
		}
	}

	public static string FactionIdentity(GameSession session)
	{
		ArgumentNullException.ThrowIfNull(session, "session");
		ClanBook book = ClanStore.Book;
		if (!book.Exists || (object)book.Member(session.Identity) == null)
		{
			return "player:" + session.Identity;
		}
		return "clan:" + book.LeaderKey;
	}

	public static string FactionDisplayName(GameSession session)
	{
		ArgumentNullException.ThrowIfNull(session, "session");
		ClanBook book = ClanStore.Book;
		if (book.Exists && (object)book.Member(session.Identity) != null)
		{
			return book.DisplayName;
		}
		string text = session.Player.Disp.Trim();
		if (text.Length <= 0)
		{
			return session.Build.DisplayName;
		}
		return text;
	}

	public static string? Tick(double delta)
	{
		EnsureLoaded();
		if (!_book.Tick(delta, out string failureMessage))
		{
			return failureMessage;
		}
		_saveCountdown -= delta;
		if (_saveCountdown <= 0.0)
		{
			Save();
			_saveCountdown = 5.0;
		}
		return failureMessage;
	}

	public static bool Save()
	{
		EnsureLoaded();
		try
		{
			string text = JsonSerializer.Serialize(_book.Snapshot(), JsonOptions);
			string path = "user://castle_war.json.tmp";
			using (Godot.FileAccess fileAccess = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write))
			{
				if (fileAccess == null)
				{
					GD.PushWarning($"[CastleWarStore] 無法寫入帳本（{Godot.FileAccess.GetOpenError()}）。");
					return false;
				}
				fileAccess.StoreString(text);
			}
			File.Move(ProjectSettings.GlobalizePath(path), ProjectSettings.GlobalizePath("user://castle_war.json"), overwrite: true);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushWarning("[CastleWarStore] 帳本寫入失敗，保留上一份：" + ex.Message);
			return false;
		}
	}

	public static void Reload()
	{
		_book = null;
		_saveCountdown = 0.0;
	}

	private static void EnsureLoaded()
	{
		if (_book != null)
		{
			return;
		}
		_book = new CastleWarBook();
		if (!Godot.FileAccess.FileExists("user://castle_war.json"))
		{
			return;
		}
		try
		{
			string asText;
			using (Godot.FileAccess fileAccess = Godot.FileAccess.Open("user://castle_war.json", Godot.FileAccess.ModeFlags.Read))
			{
				if (fileAccess == null)
				{
					return;
				}
				asText = fileAccess.GetAsText();
			}
			CastleWarBookSave? castleWarBookSave = JsonSerializer.Deserialize<CastleWarBookSave>(asText, JsonOptions);
			if (castleWarBookSave == null || castleWarBookSave.Version != 1)
			{
				throw new InvalidDataException("版本不相容");
			}
			_book = new CastleWarBook(castleWarBookSave);
		}
		catch (Exception ex)
		{
			GD.PushWarning("[CastleWarStore] 帳本讀取失敗：" + ex.Message);
			try
			{
				File.Move(ProjectSettings.GlobalizePath("user://castle_war.json"), ProjectSettings.GlobalizePath("user://castle_war.json.bad"), overwrite: true);
			}
			catch (Exception ex2)
			{
				GD.PushWarning("[CastleWarStore] 無法保留損壞帳本：" + ex2.Message);
			}
		}
	}
}
