using IdleLineage.Combat;

namespace IdleLineage.App;

public static class PetAcquisitionText
{
	public static string FailText(PetAcquisitionFailure failure)
	{
		return failure switch
		{
			PetAcquisitionFailure.ActorDead => "已死亡", 
			PetAcquisitionFailure.ItemUseBlocked => "無法使用物品", 
			PetAcquisitionFailure.ItemNotFound => "背包裡沒有這個物品", 
			PetAcquisitionFailure.ItemDefinitionMissing => "物品資料異常", 
			PetAcquisitionFailure.UnsupportedItem => "這個物品不能用來捕捉", 
			PetAcquisitionFailure.InvalidTarget => "請選擇活著的怪物", 
			PetAcquisitionFailure.TargetNotTameable => "這種怪物不能捕捉", 
			PetAcquisitionFailure.WrongTamingItem => "這隻怪物不接受該食物", 
			PetAcquisitionFailure.TargetHealthTooHigh => "目標 HP 必須降至 40% 以下", 
			PetAcquisitionFailure.ResurrectedTarget => "復活過的目標不能捕捉", 
			PetAcquisitionFailure.TamingRollFailed => "捕捉失敗", 
			PetAcquisitionFailure.InventoryFull => $"背包沒有項圈空間（上限 {180} 格）", 
			PetAcquisitionFailure.UnknownPet => "寵物資料異常", 
			PetAcquisitionFailure.PetUidUnavailable => "寵物編號衝突", 
			PetAcquisitionFailure.InvalidActor => "只有主角色能使用", 
			_ => "無法使用", 
		};
	}
}
