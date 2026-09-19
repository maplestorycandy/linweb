using System;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class AttributeScrollText
{
	public static string Describe(ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (stack.AttrEnchantLevel > 0)
		{
			return $"{L1jAttrEnchantRules.KindName(stack.AttrEnchantKind)} {stack.AttrEnchantLevel} 階（追加傷害 {BonusOf(stack.AttrEnchantLevel)}）";
		}
		return "無屬性";
	}

	public static string Suffix(ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (stack.AttrEnchantLevel > 0)
		{
			return $"〔{L1jAttrEnchantRules.KindName(stack.AttrEnchantKind)}{stack.AttrEnchantLevel}〕";
		}
		return "";
	}

	public static string BonusOf(int level)
	{
		return level switch
		{
			1 => "+1", 
			2 => "+3", 
			3 => "+5", 
			_ => "+0", 
		};
	}

	public static string Outcome(IGameData data, L1jAttrEnchantResult result)
	{
		if (!result.Attempted)
		{
			return FailureText(result.Failure);
		}
		string value = data.Item(result.TargetItemKey)?["n"]?.GetValue<string>() ?? result.TargetItemKey;
		if (!result.Succeeded)
		{
			return "強化失敗：卷軸已消耗，武器沒有任何變化。";
		}
		return $"✓ {value} 獲得 {L1jAttrEnchantRules.KindName(result.Kind)} 屬性 {result.Level} 階（追加傷害 {BonusOf(result.Level)}）";
	}

	public static string FailureText(L1jAttrEnchantFailure failure)
	{
		return failure switch
		{
			L1jAttrEnchantFailure.ConfirmationRequired => "必須先確認；未消耗卷軸。", 
			L1jAttrEnchantFailure.ScrollMissing => "找不到這張卷軸；未消耗卷軸。", 
			L1jAttrEnchantFailure.ScrollLocked => "鎖定中的卷軸無法使用；未消耗卷軸。", 
			L1jAttrEnchantFailure.TargetMissing => "武器已不在身上；未消耗卷軸。", 
			L1jAttrEnchantFailure.TargetNotWeapon => "只能對武器使用；未消耗卷軸。", 
			L1jAttrEnchantFailure.TargetCannotBeEnchanted => "這件武器無法強化；未消耗卷軸。", 
			L1jAttrEnchantFailure.AttributeAtMaximum => "同屬性已達 3 階；未消耗卷軸。", 
			L1jAttrEnchantFailure.TargetSealed => "封印中的武器無法強化；未消耗卷軸。", 
			_ => "無法使用；未消耗卷軸。", 
		};
	}
}
