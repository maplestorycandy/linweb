using System;

namespace IdleLineage.Combat;

public static class CharacterTitleRules
{
	public const int MaximumLength = 35;

	public static CharacterTitleResult TrySetSelf(Combatant player, string? requestedTitle, bool isClanMember, bool isClanLeader, bool changeTitleByOneself = false)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (player.Kind != CombatantKind.Player)
		{
			return CharacterTitleResult.Failed(CharacterTitleFailure.NotPlayer);
		}
		string text = requestedTitle ?? "";
		if (text.Length == 0)
		{
			return CharacterTitleResult.Failed(CharacterTitleFailure.Empty);
		}
		if (text.Length > 35)
		{
			return CharacterTitleResult.Failed(CharacterTitleFailure.TooLong);
		}
		if (isClanLeader)
		{
			if (player.Level < 10)
			{
				return CharacterTitleResult.Failed(CharacterTitleFailure.ClanLeaderBelowLevel10);
			}
		}
		else
		{
			if (isClanMember && !changeTitleByOneself)
			{
				return CharacterTitleResult.Failed(CharacterTitleFailure.ClanMemberCannotSelfTitle);
			}
			if (player.Level < 40)
			{
				return CharacterTitleResult.Failed(CharacterTitleFailure.IndependentBelowLevel40);
			}
		}
		player.Title = text;
		return new CharacterTitleResult(Success: true, CharacterTitleFailure.None, text);
	}

	public static string FailureText(CharacterTitleFailure failure)
	{
		return failure switch
		{
			CharacterTitleFailure.Empty => "請輸入角色封號。", 
			CharacterTitleFailure.TooLong => $"角色封號最多 {35} 字。", 
			CharacterTitleFailure.ClanLeaderBelowLevel10 => "加入血盟之後，盟主等級 10 以上才可使用封號。", 
			CharacterTitleFailure.ClanMemberCannotSelfTitle => "王子或公主盟主才可給血盟成員封號。", 
			CharacterTitleFailure.IndependentBelowLevel40 => "未加入血盟的角色需達等級 40 才可擁有封號。", 
			CharacterTitleFailure.NotPlayer => "只有玩家角色可以設定封號。", 
			_ => "無法設定角色封號。", 
		};
	}
}
