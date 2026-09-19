using System;

namespace IdleLineage.Combat;

/// <summary>
/// 遊戲全域倍率設定中心（可隨時調整）
/// </summary>
public static class GameRateConfig
{
    /// <summary>
    /// 全域經驗值倍率（例如 10.0 = 10倍經驗，100.0 = 100倍經驗）
    /// </summary>
    public static double GlobalExpRate = 100.0;

    /// <summary>
    /// 全域金幣（金幣量）掉落倍率（例如 10.0 = 每次掉落金額 x10）
    /// </summary>
    public static double GlobalGoldAmountRate = 10.0;

    /// <summary>
    /// 全域金幣掉落機率倍率（例如 5.0 = 掉落機率提高 5 倍，最高 100% 必掉）
    /// </summary>
    public static double GlobalGoldChanceRate = 5.0;

    /// <summary>
    /// 全域道具/裝備掉寶機率倍率（例如 3.0 = 掉寶率提高 3 倍）
    /// </summary>
    public static double GlobalItemDropRate = 3.0;

    /// <summary>
    /// 是否關閉負重限制（true = 無限負重，負重永遠為 0%，自然回血施法不受限）
    /// </summary>
    public static bool DisableWeightPenalty = true;

    /// <summary>
    /// 是否全地圖永晝/視野全開（true = 關閉黑夜遮罩，全螢幕明亮清晰；false = 原版天堂日夜交替與燈籠系統）
    /// </summary>
    public static bool AlwaysDaylight = false;

    /// <summary>
    /// 全域三段加速攻速倍率（預設 1.15，數值愈大攻速愈快）
    /// </summary>
    public static double ThirdHasteAttackFactor = 1.15;

    /// <summary>
    /// 全域三段加速施法倍率（預設 1.15，數值愈大冷卻縮短愈多）
    /// </summary>
    public static double ThirdHasteCastFactor = 1.15;

    /// <summary>
    /// 全域三段加速移速倍率（預設 1.15，數值愈大移動愈快）
    /// </summary>
    public static double ThirdHasteMoveFactor = 1.15;

    /// <summary>
    /// 是否啟用守護者的靈魂掉落
    /// </summary>
    public static bool GuardianSoulEnabled = true;

    /// <summary>
    /// 守護者的靈魂掉落機率 (0.0 ~ 1.0，預設 0.30)
    /// </summary>
    public static double GuardianSoulDropChance = 0.30;

    /// <summary>
    /// 守護者的靈魂持續時間 (秒，預設 3600.0)
    /// </summary>
    public static double GuardianSoulDurationSeconds = 3600.0;

    /// <summary>
    /// 守護者的靈魂 HP 加成 (預設 500)
    /// </summary>
    public static double GuardianSoulHpBonus = 500.0;

    /// <summary>
    /// 守護者的靈魂 MP 加成 (預設 200)
    /// </summary>
    public static double GuardianSoulMpBonus = 200.0;

    /// <summary>
    /// 守護者的靈魂傷害減免百分比 (預設 5.0)
    /// </summary>
    public static double GuardianSoulDamageReductionPercent = 5.0;

    public static void ApplyRates(System.Text.Json.Nodes.JsonObject? rates)
    {
        if (rates == null) return;
        if (rates.TryGetPropertyValue("expRate", out var expNode) && expNode?.GetValue<double>() is double exp && exp > 0) GlobalExpRate = exp;
        if (rates.TryGetPropertyValue("dropRate", out var dropNode) && dropNode?.GetValue<double>() is double drop && drop > 0) GlobalItemDropRate = drop;
        if (rates.TryGetPropertyValue("adenaRate", out var adenaNode) && adenaNode?.GetValue<double>() is double gold && gold > 0) GlobalGoldAmountRate = gold;
        if (rates.TryGetPropertyValue("thirdHasteAttackFactor", out var atkNode) && atkNode?.GetValue<double>() is double atk && atk > 0) ThirdHasteAttackFactor = atk;
        if (rates.TryGetPropertyValue("thirdHasteCastFactor", out var castNode) && castNode?.GetValue<double>() is double cast && cast > 0) ThirdHasteCastFactor = cast;
        if (rates.TryGetPropertyValue("thirdHasteMoveFactor", out var moveNode) && moveNode?.GetValue<double>() is double move && move > 0) ThirdHasteMoveFactor = move;

        if (rates.TryGetPropertyValue("guardianSoulEnabled", out var gseNode) && gseNode?.GetValue<bool>() is bool gse) GuardianSoulEnabled = gse;
        if (rates.TryGetPropertyValue("guardianSoulDropChance", out var gsdNode) && gsdNode?.GetValue<double>() is double gsd && gsd >= 0)
        {
            GuardianSoulDropChance = gsd > 1.0 ? gsd / 100.0 : gsd;
        }
        if (rates.TryGetPropertyValue("guardianSoulDurationMinutes", out var gsmNode) && gsmNode?.GetValue<double>() is double gsm && gsm > 0)
        {
            GuardianSoulDurationSeconds = gsm * 60.0;
        }
        else if (rates.TryGetPropertyValue("guardianSoulDurationSeconds", out var gssNode) && gssNode?.GetValue<double>() is double gss && gss > 0)
        {
            GuardianSoulDurationSeconds = gss;
        }
        if (rates.TryGetPropertyValue("guardianSoulHpBonus", out var gshNode) && gshNode?.GetValue<double>() is double gsh && gsh >= 0) GuardianSoulHpBonus = gsh;
        if (rates.TryGetPropertyValue("guardianSoulMpBonus", out var gsmpNode) && gsmpNode?.GetValue<double>() is double gsmp && gsmp >= 0) GuardianSoulMpBonus = gsmp;
        if (rates.TryGetPropertyValue("guardianSoulDamageReductionPercent", out var gsrNode) && gsrNode?.GetValue<double>() is double gsr && gsr >= 0) GuardianSoulDamageReductionPercent = gsr;
    }
}
