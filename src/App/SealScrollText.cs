using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class SealScrollText
{
	public static string Intro(bool sealing)
	{
		if (!sealing)
		{
			return "選擇要解除封印的物品。點選即解除並消耗一張。";
		}
		return "選擇要封印的物品（武器／防具一律可封；道具僅限可封印者）。封印後不可丟棄、不可存倉、不可賣給商店、不可屬性強化，但仍可裝備與使用；紅名死亡若抽中封印品將直接銷毀。點選即封印並消耗一張。";
	}

	public static string NoTargets(bool sealing)
	{
		if (!sealing)
		{
			return "身上沒有封印中的物品。";
		}
		return "沒有可以封印的物品。";
	}

	public static string Prefix(ItemStack stack)
	{
		if (!stack.Sealed)
		{
			return "";
		}
		return "〔封印〕";
	}

	public static string Outcome(IGameData data, L1jSealResult result, bool sealing)
	{
		if (!result.Attempted)
		{
			return FailureText(result.Failure);
		}
		string text = data.Item(result.TargetItemKey)?["n"]?.GetValue<string>() ?? result.TargetItemKey;
		if (!sealing)
		{
			return "✓ " + text + " 的封印已解除。";
		}
		return "✓ " + text + " 已封印（流通鎖定；使用不受影響）。";
	}

	public static string FailureText(L1jSealFailure failure)
	{
		return failure switch
		{
			L1jSealFailure.ConfirmationRequired => "必須先確認；未消耗卷軸。", 
			L1jSealFailure.ScrollMissing => "找不到這張卷軸；未消耗卷軸。", 
			L1jSealFailure.ScrollLocked => "鎖定中的卷軸無法使用；未消耗卷軸。", 
			L1jSealFailure.TargetMissing => "物品已不在身上；未消耗卷軸。", 
			L1jSealFailure.TargetNotSealable => "這件物品無法封印；未消耗卷軸。", 
			L1jSealFailure.AlreadySealed => "這件物品已經封印；未消耗卷軸。", 
			L1jSealFailure.NotSealed => "這件物品沒有封印；未消耗卷軸。", 
			_ => "無法使用；未消耗卷軸。", 
		};
	}
}
