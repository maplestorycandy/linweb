using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jJavaNpcInteractionRules
{
	public const string RoiQuestId = "Roi";

	public const string LyraQuestId = "Lyra";

	public const int RoiNpcId = 81209;

	public const int RoiFollowerNpcId = 70957;

	public const int BashNpcId = 70964;

	public const int RoiBagItemId = 41003;

	public const int EllasNpcId = 80135;

	public const int DomanNpcId = 71198;

	public const int JeronNpcId = 71199;

	public const string DomanQuestId = "71198";

	public const string JeronQuestId = "71199";

	private static int Quest(Combatant actor, string id)
	{
		return NpcActionCatalog.QuestStepOf(actor, id);
	}

	public static bool HasConditionalDialog(int npcId)
	{
		switch (npcId)
		{
		case 70009:
		case 70522:
		case 70555:
		case 70794:
		case 70811:
		case 70837:
		case 70844:
		case 71198:
		case 71199:
		case 80135:
		case 81209:
		case 81245:
			return true;
		default:
			return false;
		}
	}

	public static string? InitialHtmlId(IGameData data, int npcId, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return npcId switch
		{
			70009 => GerenHtml(actor), 
			70522 => GunterHtml(actor), 
			70555 => (CurrentGfx(data, actor) != 2374) ? null : ((actor.ClassId == "knight" && Quest(actor, "Level30") == 6) ? "jim2" : "jim4"), 
			70794 => GerardHtml(actor), 
			70811 => (Quest(actor, "Lyra") >= 1) ? "lyraEv3" : "lyraEv1", 
			70837 => NarhenHtml(actor), 
			70844 => MotherHtml(actor), 
			71198 => DomanHtml(data, actor), 
			71199 => JeronHtml(data, actor), 
			80135 => (!(actor.ClassId == "dragon")) ? null : ((Quest(actor, "Level30") == 255) ? "elas6" : ((actor.Level >= 30 && Quest(actor, "Level30") >= 1) ? "elas1" : null)), 
			81209 => (Quest(actor, "Roi") == 1) ? "roi2" : "roi1", 
			81245 => (actor.ClassId == "dragon" && CurrentGfx(data, actor) == 6984 && Quest(actor, "Level30") == 1) ? "spy_orc1" : null, 
			_ => null, 
		};
	}

	public static string? NavigationHtmlId(int npcId, string action)
	{
		if (!string.Equals(action, "sell", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		switch (npcId)
		{
		case 70523:
		case 70805:
			return "ladar2";
		case 70537:
		case 70807:
			return "farlin2";
		case 70525:
		case 70804:
			return "lien2";
		default:
			return null;
		}
	}

	public static bool Handles(int npcId, string action)
	{
		bool flag = NavigationHtmlId(npcId, action) != null || (npcId == 71198 && IsOneOf(action, "A", "B", "C", "D", "E")) || (npcId == 71199 && IsOneOf(action, "A", "B", "C"));
		if (!flag)
		{
			bool flag2 = npcId == 70811;
			if (flag2)
			{
				bool flag3;
				switch (action)
				{
				case "contract1":
				case "contract1yes":
				case "contract1no":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = flag3;
			}
			flag = flag2;
		}
		if (!flag && (npcId != 80135 || !string.Equals(action, "a", StringComparison.OrdinalIgnoreCase)) && (npcId != 81209 || !string.Equals(action, "start", StringComparison.OrdinalIgnoreCase)))
		{
			if (npcId == 81245)
			{
				return string.Equals(action, "request flute of spy", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		return true;
	}

	public static L1jJavaNpcActionResult Execute(IGameData data, Combatant actor, int npcId, string action)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(action, "action");
		string text = NavigationHtmlId(npcId, action);
		if (text != null)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: true, "", text);
		}
		switch (npcId)
		{
		case 71198:
			return ExecuteDoman(data, actor, action);
		case 71199:
			return ExecuteJeron(data, actor, action);
		case 81209:
			if (string.Equals(action, "start", StringComparison.OrdinalIgnoreCase))
			{
				actor.Progress.QuestSteps["Roi"] = 1;
				return new L1jJavaNpcActionResult(Handled: true, Success: true, "羅伊開始跟隨。", null, StartRoiEscort: true);
			}
			break;
		}
		if (npcId == 80135 && string.Equals(action, "a", StringComparison.OrdinalIgnoreCase))
		{
			if (actor.ClassId != "dragon")
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "只有龍騎士能領取妖魔密使變形卷軸。");
			}
			if (actor.Level < 30 || Quest(actor, "Level30") < 1 || Quest(actor, "Level30") == 255)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "目前尚未進行妖魔密使任務。");
			}
			string text2 = FindItemKey(data, 49220);
			if (text2 == null)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "妖魔密使變形卷軸（49220）資料缺失。");
			}
			if (CombatInventory.Count(actor, text2) > 0)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: true, "", "elas5");
			}
			CombatInventory.Add(actor, text2, 1L);
			return new L1jJavaNpcActionResult(Handled: true, Success: true, "取得妖魔密使變形卷軸（49220）。", "elas4");
		}
		if (npcId == 70811)
		{
			bool flag;
			switch (action)
			{
			case "contract1":
				actor.Progress.QuestSteps["Lyra"] = 1;
				return new L1jJavaNpcActionResult(Handled: true, Success: true, "已與萊拉訂定契約。", "lyraev2");
			case "contract1yes":
			case "contract1no":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				long num = ExchangeLyraTotems(data, actor);
				if (action == "contract1no")
				{
					actor.Progress.QuestSteps["Lyra"] = 0;
				}
				string htmlId = ((action == "contract1yes") ? "lyraev5" : "lyraev4");
				string message = ((num > 0) ? $"交付妖魔圖騰，取得 {num:N0} 金幣。" : "目前沒有可交付的妖魔圖騰。");
				return new L1jJavaNpcActionResult(Handled: true, Success: true, message, htmlId);
			}
		}
		if (npcId == 81245 && string.Equals(action, "request flute of spy", StringComparison.OrdinalIgnoreCase))
		{
			if (actor.ClassId != "dragon")
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "只有龍騎士能完成這項交付。");
			}
			if (Quest(actor, "Level30") != 1 || CurrentGfx(data, actor) != 6984)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "必須在任務第一階段化身為妖魔密使才能交付。");
			}
			string text3 = FindItemKey(data, 49223);
			string text4 = FindItemKey(data, 49222);
			if (text3 == null || text4 == null)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "妖魔密使任務物品資料缺失。");
			}
			if (!CombatInventory.TryRemove(actor, text3, 1L))
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少妖魔密使的徽印（49223）。");
			}
			CombatInventory.Add(actor, text4, 1L);
			return new L1jJavaNpcActionResult(Handled: true, Success: true, "取得妖魔密使之笛子（49222）。");
		}
		return new L1jJavaNpcActionResult(Handled: false, Success: false);
	}

	public static string? FindItemKey(IGameData data, int itemId)
	{
		string text = null;
		foreach (var (text3, jsonNode2) in data.Items)
		{
			if (CombatSkill.ReadInt((jsonNode2 as JsonObject) ?? new JsonObject(), "l1jItemId") == itemId)
			{
				if (text != null)
				{
					return null;
				}
				text = text3;
			}
		}
		return text;
	}

	private static L1jJavaNpcActionResult ExecuteDoman(IGameData data, Combatant actor, string action)
	{
		string text = action.ToUpperInvariant();
		int num = text switch
		{
			"A" => 0, 
			"B" => 1, 
			"C" => 2, 
			"D" => 3, 
			"E" => 4, 
			_ => -1, 
		};
		if (num < 0)
		{
			return new L1jJavaNpcActionResult(Handled: false, Success: false);
		}
		if (HasAvailableItem(data, actor, 21059))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "你已經完成毒蛇傭兵團考驗。");
		}
		if (Quest(actor, "71198") != num)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "目前不是這個考驗階段。");
		}
		return text switch
		{
			"A" => ConsumeAndGrant(data, actor, "71198", 1, 41339, 5L, 41340, "tion4", "tion9", "交付亡者的信件 5 個，取得傭兵團長多文的推薦書。"), 
			"B" => ConsumeAndGrant(data, actor, "71198", 2, 41341, 1L, null, "tion5", "tion10", "交付帝倫的教本。"), 
			"C" => ConsumeAndGrant(data, actor, "71198", 3, 41343, 1L, 21057, "tion6", "tion12", "交付法利昂的血痕，取得第一階訓練騎士披肩。"), 
			"D" => UpgradeDomanCloak(data, actor, 41344, 21057, 21058, 4, "tion7", "tion13", "交付水中的水，訓練騎士披肩提升為第二階。"), 
			"E" => CompleteDomanCloak(data, actor), 
			_ => new L1jJavaNpcActionResult(Handled: false, Success: false), 
		};
	}

	private static L1jJavaNpcActionResult ExecuteJeron(IGameData data, Combatant actor, string action)
	{
		string text = action.ToUpperInvariant();
		if (!IsOneOf(text, "A", "B", "C"))
		{
			return new L1jJavaNpcActionResult(Handled: false, Success: false);
		}
		if (HasAvailableItem(data, actor, 21059))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "你已經完成毒蛇傭兵團考驗。");
		}
		int num = Quest(actor, "71199");
		if (text == "A")
		{
			if (num != 0)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "帝倫已經確認過多文的介紹。");
			}
			if (!HasAvailableItem(data, actor, 41340))
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少傭兵團長多文的推薦書。", "jeron10");
			}
			actor.Progress.QuestSteps["71199"] = 1;
			return new L1jJavaNpcActionResult(Handled: true, Success: true, "帝倫確認了多文的推薦書。", "jeron2");
		}
		if (num != 1)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "目前還不能取得帝倫的教本。");
		}
		string text2 = FindItemKey(data, 41340);
		string text3 = FindItemKey(data, 41341);
		if (text2 == null || text3 == null)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "毒蛇傭兵團任務物品資料缺失。");
		}
		if (AvailableHeldCount(actor, text2) < 1)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少傭兵團長多文的推薦書。", "jeron10");
		}
		if (text == "B")
		{
			if (CombatWallet.Balance(actor) < 1000000)
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "金幣不足 1,000,000。", "jeron8");
			}
			if (!TryRemoveHeld(actor, text2, 1L))
			{
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少傭兵團長多文的推薦書。", "jeron10");
			}
			if (!CombatWallet.TrySpend(actor, 1000000L))
			{
				CombatInventory.Add(data, actor, text2, 1L);
				return new L1jJavaNpcActionResult(Handled: true, Success: false, "金幣不足 1,000,000。", "jeron8");
			}
			CombatInventory.Add(data, actor, text3, 1L);
			actor.Progress.QuestSteps["71199"] = 255;
			return new L1jJavaNpcActionResult(Handled: true, Success: true, "支付 1,000,000 金幣，取得帝倫的教本。", "jeron6");
		}
		string text4 = FindItemKey(data, 41342);
		if (text4 == null)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "梅杜莎之血（41342）資料缺失。");
		}
		if (AvailableHeldCount(actor, text4) < 1)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少梅杜莎之血。", "jeron9");
		}
		if (!TryRemoveHeld(actor, text4, 1L) || !TryRemoveHeld(actor, text2, 1L))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "交付任務物品失敗。");
		}
		CombatInventory.Add(data, actor, text3, 1L);
		actor.Progress.QuestSteps["71199"] = 255;
		return new L1jJavaNpcActionResult(Handled: true, Success: true, "交付梅杜莎之血，取得帝倫的教本。", "jeron5");
	}

	private static L1jJavaNpcActionResult ConsumeAndGrant(IGameData data, Combatant actor, string questId, int nextStep, int consumeItemId, long consumeCount, int? grantItemId, string successHtml, string failureHtml, string successMessage)
	{
		string text = FindItemKey(data, consumeItemId);
		object obj;
		if (grantItemId.HasValue)
		{
			int valueOrDefault = grantItemId.GetValueOrDefault();
			obj = FindItemKey(data, valueOrDefault);
		}
		else
		{
			obj = null;
		}
		string text2 = (string)obj;
		if (text == null || (grantItemId.HasValue && text2 == null))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "毒蛇傭兵團任務物品資料缺失。");
		}
		ItemGainPreview preview = default(ItemGainPreview);
		if (text2 != null && !TryPrepareQuestReward(data, actor, text2, out preview))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "無法建立任務獎勵。");
		}
		if (AvailableHeldCount(actor, text) < consumeCount)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少交付物品。", failureHtml);
		}
		if (!TryRemoveHeld(actor, text, consumeCount))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "交付任務物品失敗。");
		}
		if (text2 != null)
		{
			CommitQuestReward(data, actor, preview);
		}
		actor.Progress.QuestSteps[questId] = nextStep;
		return new L1jJavaNpcActionResult(Handled: true, Success: true, successMessage, successHtml);
	}

	private static L1jJavaNpcActionResult UpgradeDomanCloak(IGameData data, Combatant actor, int materialItemId, int oldCloakItemId, int newCloakItemId, int nextStep, string successHtml, string failureHtml, string successMessage)
	{
		string text = FindItemKey(data, materialItemId);
		string text2 = FindItemKey(data, oldCloakItemId);
		string text3 = FindItemKey(data, newCloakItemId);
		if (text == null || text2 == null || text3 == null)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "毒蛇傭兵團披肩資料缺失。");
		}
		if (!TryPrepareQuestReward(data, actor, text3, out var preview))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "無法建立任務獎勵披肩。");
		}
		if (AvailableHeldCount(actor, text) < 1 || AvailableHeldCount(actor, text2) < 1)
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "缺少交付材料或上一階訓練騎士披肩。", failureHtml);
		}
		if (!TryRemoveHeld(actor, text, 1L) || !TryRemoveHeld(actor, text2, 1L))
		{
			return new L1jJavaNpcActionResult(Handled: true, Success: false, "交付任務物品失敗。");
		}
		CommitQuestReward(data, actor, preview);
		actor.Progress.QuestSteps["71198"] = nextStep;
		return new L1jJavaNpcActionResult(Handled: true, Success: true, successMessage, successHtml);
	}

	private static L1jJavaNpcActionResult CompleteDomanCloak(IGameData data, Combatant actor)
	{
		L1jJavaNpcActionResult result = UpgradeDomanCloak(data, actor, 41345, 21058, 21059, 0, "tion8", "tion15", "交付酸性乳液，取得毒蛇之牙披肩。");
		if (result.Success)
		{
			actor.Progress.QuestSteps["71199"] = 0;
		}
		return result;
	}

	private static string? DomanHtml(IGameData data, Combatant actor)
	{
		switch (Quest(actor, "71198"))
		{
		case 1:
			return "tion4";
		case 2:
		case 5:
			return "tion5";
		case 3:
			return "tion6";
		case 4:
			return "tion7";
		default:
			return HasAvailableItem(data, actor, 21059) ? "tion19" : null;
		}
	}

	private static string? JeronHtml(IGameData data, Combatant actor)
	{
		if (Quest(actor, "71199") != 1)
		{
			if (!HasAvailableItem(data, actor, 21059) && Quest(actor, "71199") != 255)
			{
				return null;
			}
			return "jeron7";
		}
		return "jeron3";
	}

	private static bool HasAvailableItem(IGameData data, Combatant actor, int itemId)
	{
		string text = FindItemKey(data, itemId);
		if (text != null)
		{
			return AvailableHeldCount(actor, text) > 0;
		}
		return false;
	}

	private static long AvailableHeldCount(Combatant actor, string itemKey)
	{
		return CombatInventory.AvailableCount(actor, itemKey) + actor.EquippedItems.Values.Where((ItemStack item) => !item.Locked && string.Equals(item.ItemKey, itemKey, StringComparison.Ordinal)).Sum((ItemStack item) => item.Quantity);
	}

	private static bool TryRemoveHeld(Combatant actor, string itemKey, long count)
	{
		if (CombatInventory.AvailableCount(actor, itemKey) >= count)
		{
			return CombatInventory.TryRemove(actor, itemKey, count);
		}
		if (count != 1)
		{
			return false;
		}
		string key = actor.EquippedItems.FirstOrDefault<KeyValuePair<string, ItemStack>>((KeyValuePair<string, ItemStack> entry) => !entry.Value.Locked && string.Equals(entry.Value.ItemKey, itemKey, StringComparison.Ordinal)).Key;
		if (key != null)
		{
			return actor.EquippedItems.Remove(key);
		}
		return false;
	}

	private static bool TryPrepareQuestReward(IGameData data, Combatant actor, string itemKey, out ItemGainPreview preview)
	{
		try
		{
			preview = ItemGainRules.Preview(data, actor.Key, actor.Progress.ItemGainAttemptSequence, itemKey, new ItemGainOptions(ItemGainSource.QuestReward));
			return !preview.UsesCommittedRoll || actor.Progress.ItemGainAttemptSequence < long.MaxValue;
		}
		catch (KeyNotFoundException)
		{
			preview = default(ItemGainPreview);
			return false;
		}
	}

	private static void CommitQuestReward(IGameData data, Combatant actor, ItemGainPreview preview)
	{
		if (preview.Enhancement == 0 && preview.Blessing == ItemBlessing.Normal && preview.ItemLevel <= 0)
		{
			IReadOnlyList<EquipmentAffixRoll> affixes = preview.Affixes;
			if (affixes == null || affixes.Count <= 0)
			{
				CombatInventory.Add(data, actor, preview.ResolvedItemKey, 1L);
				goto IL_00b0;
			}
		}
		CombatInventory.Add(data, actor, new ItemStack(CombatInventory.NextUid(actor), preview.ResolvedItemKey, 1L)
		{
			Blessing = preview.Blessing,
			Enhancement = preview.Enhancement,
			ItemLevel = preview.ItemLevel,
			Affixes = (preview.Affixes?.ToArray() ?? Array.Empty<EquipmentAffixRoll>())
		});
		goto IL_00b0;
		IL_00b0:
		if (preview.UsesCommittedRoll)
		{
			actor.Progress.ItemGainAttemptSequence++;
		}
	}

	private static bool IsOneOf(string action, params string[] candidates)
	{
		return candidates.Any((string candidate) => string.Equals(action, candidate, StringComparison.OrdinalIgnoreCase));
	}

	private static long ExchangeLyraTotems(IGameData data, Combatant actor)
	{
		long num = 0L;
		(int, int)[] array = new(int, int)[5]
		{
			(40131, 50),
			(40132, 100),
			(40133, 50),
			(40134, 30),
			(40135, 200)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(int, int) tuple = array[i];
			int item = tuple.Item1;
			int item2 = tuple.Item2;
			string text = FindItemKey(data, item);
			if (text != null)
			{
				long num2 = CombatInventory.AvailableCount(actor, text);
				if (num2 > 0 && CombatInventory.TryRemove(actor, text, num2))
				{
					num = checked(num + num2 * item2);
				}
			}
		}
		if (num > 0)
		{
			CombatWallet.Add(actor, num);
		}
		return num;
	}

	private static int CurrentGfx(IGameData data, Combatant actor)
	{
		return PolymorphRules.CurrentForm(data, actor)?.Gfx ?? 0;
	}

	private static string GerenHtml(Combatant actor)
	{
		return ClassKitRegistry.NormalizeClassId(actor.ClassId) switch
		{
			"royal" => "gerengp1", 
			"knight" => "gerengk1", 
			"elf" => "gerenge1", 
			"mage" => GerenMageHtml(actor), 
			"dark" => "gerengde1", 
			"dragon" => "gerengdk1", 
			"illusion" => "gerengi1", 
			"warrior" => "gerengw3", 
			_ => "gerengw3", 
		};
	}

	private static string GerenMageHtml(Combatant actor)
	{
		if (actor.Level < 30 || Quest(actor, "Level15") != 255)
		{
			return "gerengw3";
		}
		int num = Quest(actor, "Level30");
		if (num < 4)
		{
			return num switch
			{
				3 => "gerengT4", 
				2 => "gerengT3", 
				1 => "gerengT2", 
				_ => "gerengT1", 
			};
		}
		return "gerengw3";
	}

	private static string? GunterHtml(Combatant actor)
	{
		switch (actor.ClassId)
		{
		case "royal":
		{
			string result;
			if (actor.Level < 15)
			{
				result = "gunterp12";
			}
			else
			{
				int num = Quest(actor, "Level15");
				bool flag = ((num == 2 || num == 255) ? true : false);
				result = (flag ? "gunterp11" : "gunterp9");
			}
			return result;
		}
		case "knight":
			return Quest(actor, "Level30") switch
			{
				0 => "gunterk9", 
				1 => "gunterkE1", 
				2 => "gunterkE2", 
				_ => "gunterkE3", 
			};
		case "elf":
			return "guntere1";
		case "mage":
			return "gunterw1";
		case "dark":
			return "gunterde1";
		default:
			return null;
		}
	}

	private static string? GerardHtml(Combatant actor)
	{
		if (actor.ClassId == "royal")
		{
			return "gerardp1";
		}
		if (actor.ClassId == "elf")
		{
			return "gerarde1";
		}
		if (actor.ClassId == "mage")
		{
			return "gerardw1";
		}
		if (actor.ClassId == "dark")
		{
			return "gerardde1";
		}
		if (actor.ClassId != "knight")
		{
			return null;
		}
		int num = Quest(actor, "Level30");
		if (num == 255)
		{
			return "gerardkEcg";
		}
		if (num >= 3)
		{
			return num switch
			{
				3 => "gerardkE1", 
				4 => "gerardkE2", 
				5 => "gerardkE3", 
				_ => "gerardkE4", 
			};
		}
		return "gerardk7";
	}

	private static string? NarhenHtml(Combatant actor)
	{
		switch (actor.ClassId)
		{
		case "dark":
			return "narhenM2";
		case "elf":
			if (actor.Alignment <= -501.0)
			{
				return "narhenCE";
			}
			return "narhene1";
		case "mage":
		case "knight":
		case "royal":
			return "narhenm1";
		case "dragon":
			return "narhenM3";
		case "illusion":
			return "narhenM4";
		default:
			return null;
		}
	}

	private static string? MotherHtml(Combatant actor)
	{
		if (actor.ClassId != "elf")
		{
			return "motherm1";
		}
		if (actor.Level < 30 || Quest(actor, "Level15") != 255)
		{
			return "mothere1";
		}
		int num = Quest(actor, "Level30");
		if (num != 255)
		{
			if (num < 1)
			{
				return "motherEE1";
			}
			return "motherEE2";
		}
		return "motherEE3";
	}
}
