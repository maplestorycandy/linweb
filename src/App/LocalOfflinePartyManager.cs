using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed class OfflineCompanionRecord
{
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; } = 1;
    public string MapKey { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string PlayerBlob { get; set; } = "";
    public string AutoCast { get; set; } = "";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

public static class LocalOfflinePartyManager
{
    private static readonly Dictionary<int, OfflineCompanionRecord> _offlineSlots = new();
    private static bool _loaded = false;

    private static string FilePath => "user://offline_party.json";

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _offlineSlots.Clear();

        try
        {
            if (Godot.FileAccess.FileExists(FilePath))
            {
                using var fa = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Read);
                if (fa != null)
                {
                    string json = fa.GetAsText();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var records = JsonSerializer.Deserialize<List<OfflineCompanionRecord>>(json);
                        if (records != null)
                        {
                            foreach (var r in records)
                            {
                                _offlineSlots[r.Slot] = r;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[LocalOfflinePartyManager] 讀取離線隊友紀錄失敗: {ex.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            var list = _offlineSlots.Values.ToList();
            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            using var fa = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
            if (fa != null)
            {
                fa.StoreString(json);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[LocalOfflinePartyManager] 儲存離線隊友紀錄失敗: {ex.Message}");
        }
    }

    public static void RegisterOffline(int slot, Combatant player, string mapKey, double x, double y, string autoCast = "")
    {
        Load();
        string blob = PlayerSave.Capture(player);
        _offlineSlots[slot] = new OfflineCompanionRecord
        {
            Slot = slot,
            Name = player.Disp,
            ClassId = player.ClassId,
            Level = player.Level,
            MapKey = mapKey,
            X = x,
            Y = y,
            PlayerBlob = blob,
            AutoCast = autoCast,
            RegisteredAt = DateTime.UtcNow
        };
        Save();
    }

    public static void Unregister(int slot)
    {
        Load();
        if (_offlineSlots.Remove(slot))
        {
            Save();
        }
    }

    public static void ClearAll()
    {
        Load();
        _offlineSlots.Clear();
        Save();
    }

    public static IReadOnlyList<OfflineCompanionRecord> GetActiveCompanions(int currentSlot)
    {
        Load();
        foreach (var r in _offlineSlots.Values)
        {
            var slotInfo = SaveManager.ReadSlot(r.Slot);
            if (!slotInfo.Empty)
            {
                r.Level = slotInfo.Level;
                if (!string.IsNullOrWhiteSpace(slotInfo.CharacterName))
                {
                    r.Name = slotInfo.CharacterName;
                }
            }
            string latestBlob = SaveManager.GetPlayerBlobForSlot(r.Slot);
            if (!string.IsNullOrWhiteSpace(latestBlob))
            {
                r.PlayerBlob = latestBlob;
            }
            string latestAutoCast = SaveManager.GetAutoCastForSlot(r.Slot);
            if (!string.IsNullOrWhiteSpace(latestAutoCast))
            {
                r.AutoCast = latestAutoCast;
            }
        }
        return _offlineSlots.Values
            .Where(r => r.Slot != currentSlot && !string.IsNullOrWhiteSpace(r.PlayerBlob))
            .ToList();
    }

    public static void SyncProgressBackToSlot(int slot, Combatant ally)
    {
        try
        {
            if (ally == null || slot <= 0 || slot > SaveManager.SlotCount) return;

            if (ally.Hp <= 0 || ally.Dead)
            {
                ally.Hp = ally.MaxHp;
                ally.Dead = false;
            }

            string updatedPlayerBlob = ally.Kind == CombatantKind.Ally
                ? PlayerSave.CaptureAlly(ally, "player")
                : PlayerSave.Capture(ally);

            if (_offlineSlots.TryGetValue(slot, out var record))
            {
                record.Level = ally.Level;
                record.PlayerBlob = updatedPlayerBlob;
                Save();
            }

            SaveManager.SaveCompanionProgress(slot, ally, updatedPlayerBlob);
            GD.Print($"[LocalOfflinePartyManager] 成功同步第 {slot} 槽 ({ally.Disp}) 進度: 等級 {ally.Level}, EXP {ally.Experience:0}");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[LocalOfflinePartyManager] 同步進度回第 {slot} 槽失敗: {ex.Message}");
        }
    }
}