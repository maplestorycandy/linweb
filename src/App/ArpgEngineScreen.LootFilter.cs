using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public partial class ArpgEngineScreen
{
	private string _lootFilterMessage = "";

	private void ToggleLootFilter()
	{
		if (_classicRightKind != "loot-filter")
		{
			_lootFilterMessage = "";
		}
		ToggleRightAnchor("loot-filter", BuildLootFilterPanel);
	}

	private Control BuildLootFilterPanel()
	{
		var (panel, body) = BarPanelShell("物品過濾器", 460f);
		body.AddChild(BarRow("黑名單中的怪物掉落不會自動進背包，會留在怪物原位置；手動拾取不受限制。", BarPanelDim, 12, wrap: true, 464f));
		
		LineEdit search = new LineEdit
		{
			Name = "LootFilterSearch",
			PlaceholderText = "輸入物品名稱（至少 1 個字）",
			ClearButtonEnabled = true,
			CustomMinimumSize = new Vector2(464f, 30f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		string[] states = new string[3] { "normal", "focus", "read_only" };
		foreach (string state in states)
		{
			search.AddThemeStyleboxOverride(state, GroundDropPromptInputStyle());
		}
		search.AddThemeColorOverride("font_color", BarPanelText);
		search.AddThemeColorOverride("font_placeholder_color", BarPanelDim);
		search.AddThemeFontSizeOverride("font_size", 13);
		body.AddChild(search);

		body.AddChild(BarRow("搜尋結果", BarPanelGold, 13));
		VBoxContainer resultList = new VBoxContainer
		{
			Name = "LootFilterSearchResults"
		};
		resultList.AddThemeConstantOverride("separation", 2);
		resultList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		ScrollContainer searchScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(464f, 105f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		searchScroll.AddChild(resultList);
		ClassicMapFrame.MakeScrollbarsTransparent(searchScroll);
		body.AddChild(searchScroll);

		body.AddChild(BarRow("目前黑名單", BarPanelGold, 13));
		VBoxContainer blacklist = new VBoxContainer
		{
			Name = "LootFilterBlacklist"
		};
		blacklist.AddThemeConstantOverride("separation", 2);
		blacklist.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		ScrollContainer blacklistScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(464f, 130f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		blacklistScroll.AddChild(blacklist);
		ClassicMapFrame.MakeScrollbarsTransparent(blacklistScroll);
		body.AddChild(blacklistScroll);

		Label status = BarRow(_lootFilterMessage, BarPanelDim, 12, wrap: true, 464f);
		status.Name = "LootFilterStatus";
		body.AddChild(status);

		search.TextChanged += RefreshSearch;
		RefreshBlacklist();
		RefreshSearch("");
		search.CallDeferred(Control.MethodName.GrabFocus);
		panel.Name = "LootFilterPanel";
		return panel;

		static void ClearRows(VBoxContainer list)
		{
			foreach (Node child in list.GetChildren())
			{
				list.RemoveChild(child);
				child.QueueFree();
			}
		}

		bool PersistChange(string itemKey, bool add)
		{
			if (!(add ? _session.LootFilterItemKeys.Add(itemKey) : _session.LootFilterItemKeys.Remove(itemKey)))
			{
				return true;
			}
			if (SaveManager.Save(_session))
			{
				return true;
			}
			if (add)
			{
				_session.LootFilterItemKeys.Remove(itemKey);
			}
			else
			{
				_session.LootFilterItemKeys.Add(itemKey);
			}
			_lootFilterMessage = "存檔失敗，名單沒有變更。";
			SetBarStatus(status, _lootFilterMessage, good: false);
			return false;
		}

		void RefreshBlacklist()
		{
			ClearRows(blacklist);
			string[] array = _session.LootFilterItemKeys
				.OrderBy(key => LootFilterRules.DisplayName(GameDataProvider.Shared, key), StringComparer.Ordinal)
				.ThenBy(key => key, StringComparer.Ordinal)
				.ToArray();

			if (array.Length == 0)
			{
				blacklist.AddChild(BarRow("尚未加入任何物品。", BarPanelDim, 12));
			}
			else
			{
				foreach (string text in array)
				{
					string capturedKey = text;
					HBoxContainer row = new HBoxContainer
					{
						CustomMinimumSize = new Vector2(464f, 27f)
					};
					row.AddThemeConstantOverride("separation", 4);
					Label name = BarRow(LootFilterRules.DisplayName(GameDataProvider.Shared, capturedKey), BarPanelText, 12);
					name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
					name.TooltipText = name.Text;

					Button removeBtn = new Button
					{
						Name = "LootFilterRemoveButton",
						Text = "×",
						TooltipText = "取消黑名單",
						CustomMinimumSize = new Vector2(32f, 26f),
						FocusMode = Control.FocusModeEnum.None
					};
					removeBtn.AddThemeFontSizeOverride("font_size", 16);
					removeBtn.Pressed += delegate
					{
						if (PersistChange(capturedKey, add: false))
						{
							_lootFilterMessage = "已取消：" + name.Text;
							SetBarStatus(status, _lootFilterMessage, good: true);
							RefreshBlacklist();
							RefreshSearch(search.Text);
						}
					};

					row.AddChild(name);
					row.AddChild(removeBtn);
					blacklist.AddChild(row);
				}
			}
		}

		void RefreshSearch(string query)
		{
			ClearRows(resultList);
			if (string.IsNullOrWhiteSpace(query))
			{
				resultList.AddChild(BarRow("輸入一個字後開始搜尋。", BarPanelDim, 12));
			}
			else
			{
				IReadOnlyList<LootFilterCandidate> candidates = LootFilterRules.Search(GameDataProvider.Shared, query);
				if (candidates.Count != 0)
				{
					foreach (LootFilterCandidate item in candidates)
					{
						LootFilterCandidate captured = item;
						bool isBlocked = _session.LootFilterItemKeys.Contains(captured.ItemKey);
						HBoxContainer row = new HBoxContainer
						{
							CustomMinimumSize = new Vector2(464f, 27f)
						};
						row.AddThemeConstantOverride("separation", 4);
						Label name = BarRow(captured.DisplayName, BarPanelText, 12);
						name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
						name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
						name.TooltipText = captured.DisplayName;

						Button addBtn = new Button
						{
							Name = "LootFilterAddButton",
							Text = (isBlocked ? "已加入" : "加入"),
							Disabled = isBlocked,
							CustomMinimumSize = new Vector2(58f, 26f),
							FocusMode = Control.FocusModeEnum.None
						};
						addBtn.AddThemeFontSizeOverride("font_size", 11);
						addBtn.Pressed += delegate
						{
							if (PersistChange(captured.ItemKey, add: true))
							{
								_lootFilterMessage = "已加入：" + captured.DisplayName;
								SetBarStatus(status, _lootFilterMessage, good: true);
								RefreshBlacklist();
								RefreshSearch(search.Text);
							}
						};

						row.AddChild(name);
						row.AddChild(addBtn);
						resultList.AddChild(row);
					}
					return;
				}
				resultList.AddChild(BarRow("找不到相符物品。", BarPanelDim, 12));
			}
		}
	}

	private static StyleBoxFlat GroundDropPromptInputStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#12100c"),
			BorderColor = Color.FromHtml("#4a4034"),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 2f,
			ContentMarginRight = 2f,
			ContentMarginTop = 0f,
			ContentMarginBottom = 0f
		};
	}
}
