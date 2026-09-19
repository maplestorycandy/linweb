using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class L1jTargetItemUseText
{
	internal static string Title(IGameData data, ItemStack source)
	{
		return MainItemId(data, source.ItemKey) switch
		{
			40964 => "黑魔法粉 · 選擇歷史書頁", 
			41036 => "膠水 · 選擇航海日誌頁", 
			49188 => "索夏依卡靈魂之石 · 選擇笛子", 
			_ => "選擇材料", 
		};
	}

	internal static string Instruction(IGameData data, ItemStack source)
	{
		return MainItemId(data, source.ItemKey) switch
		{
			40964 => $"選擇封印的歷史書頁；成功率 {50}%。" + "成功或失敗都會消耗粉末與書頁。", 
			41036 => $"選擇航海日誌頁；成功率 {67}%。" + "成功或失敗都會消耗膠水與書頁。", 
			49188 => "選擇生鏽的笛子，合成索夏依卡靈魂之笛；兩件材料都會消耗。", 
			_ => "選擇要處理的材料。", 
		};
	}

	internal static string Result(IGameData data, L1jTargetItemUseResult result)
	{
		if (!result.Attempted)
		{
			return "無法使用：" + ItemActivation.TargetItemFailureText(result.Failure);
		}
		if (!result.Succeeded)
		{
			return "加工失敗，來源物品與所選材料都已消耗。";
		}
		string text = data.Item(result.OutputItemKey)?["n"]?.GetValue<string>() ?? result.OutputItemKey;
		return "製作成功：" + text;
	}

	internal static bool IsSuccess(L1jTargetItemUseResult result)
	{
		if (result.Attempted)
		{
			return result.Succeeded;
		}
		return false;
	}

	private static int MainItemId(IGameData data, string key)
	{
		if (!(data.Item(key)?["l1jItemId"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}
}
