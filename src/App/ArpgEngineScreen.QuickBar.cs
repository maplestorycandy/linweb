using System;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed partial class ArpgEngineScreen
{
	private void BuildDraggableUtilityBar()
	{
		_utilityBarPanel?.QueueFree();

		float panelW = 126f;
		float panelH = 22f;

		Vector2 defaultPos = new Vector2(Math.Max(0f, RnMacroX + 140f - panelW - 4f), RnPanelY + 5f);
		Vector2 savedPos = _session.UtilityBarPos;
		if (savedPos.X <= 0f || savedPos.Y <= 0f || savedPos.X > _viewW || savedPos.Y > _viewH)
		{
			savedPos = defaultPos;
		}
		savedPos.X = Math.Clamp(savedPos.X, 0f, Math.Max(0f, _viewW - panelW));
		savedPos.Y = Math.Clamp(savedPos.Y, 0f, Math.Max(0f, _viewH - panelH));
		_session.UtilityBarPos = savedPos;

		_utilityBarPanel = new Panel
		{
			Position = savedPos,
			Size = new Vector2(panelW, panelH),
			CustomMinimumSize = new Vector2(panelW, panelH),
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 2100
		};

		StyleBoxFlat panelBg = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.07f, 0.10f, 0.90f),
			BorderColor = Color.FromHtml("#3a404d"),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3
		};
		_utilityBarPanel.AddThemeStyleboxOverride("panel", panelBg);

		// Drag handle
		Control dragHandle = new Control
		{
			Position = new Vector2(2f, 2f),
			Size = new Vector2(14f, 18f),
			CustomMinimumSize = new Vector2(14f, 18f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.Move,
			TooltipText = "按住左鍵可拖曳此功能列"
		};
		Label handleIcon = new Label
		{
			Text = "⋮⋮",
			Position = Vector2.Zero,
			Size = new Vector2(14f, 18f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		handleIcon.AddThemeFontSizeOverride("font_size", 10);
		handleIcon.AddThemeColorOverride("font_color", Color.FromHtml("#6c7b99"));
		dragHandle.AddChild(handleIcon);

		bool isDragging = false;
		Vector2 dragOffset = Vector2.Zero;

		void HandleDragEvent(InputEvent @event, Control target)
		{
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					isDragging = true;
					dragOffset = mb.GlobalPosition - _utilityBarPanel.GlobalPosition;
				}
				else
				{
					isDragging = false;
					_session.UtilityBarPos = _utilityBarPanel.Position;
					SaveManager.Save(_session);
				}
				target.AcceptEvent();
			}
			else if (@event is InputEventMouseMotion mm && isDragging)
			{
				Vector2 newPos = mm.GlobalPosition - dragOffset;
				newPos.X = Math.Clamp(newPos.X, 0f, Math.Max(0f, _viewW - panelW));
				newPos.Y = Math.Clamp(newPos.Y, 0f, Math.Max(0f, _viewH - panelH));
				_utilityBarPanel.Position = newPos;
				_session.UtilityBarPos = newPos;
				target.AcceptEvent();
			}
		}

		dragHandle.GuiInput += (@event) => HandleDragEvent(@event, dragHandle);
		_utilityBarPanel.GuiInput += (@event) => HandleDragEvent(@event, _utilityBarPanel);
		_utilityBarPanel.AddChild(dragHandle);

		StyleBoxFlat btnNormal = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.14f, 0.18f, 0.90f),
			BorderColor = Color.FromHtml("#444955"),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2
		};
		btnNormal.SetContentMarginAll(0f);

		StyleBoxFlat btnHover = new StyleBoxFlat
		{
			BgColor = new Color(0.20f, 0.23f, 0.30f, 1f),
			BorderColor = Color.FromHtml("#6c7b99"),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2
		};
		btnHover.SetContentMarginAll(0f);

		// 1. [過濾]
		Button btnFilter = new Button
		{
			Text = "過濾",
			Position = new Vector2(18f, 2f),
			Size = new Vector2(33f, 18f),
			CustomMinimumSize = new Vector2(33f, 18f),
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = "物品過濾器與文字搜尋清單"
		};
		btnFilter.AddThemeFontSizeOverride("font_size", 10);
		btnFilter.AddThemeColorOverride("font_color", Color.FromHtml("#c8b98a"));
		btnFilter.AddThemeColorOverride("font_hover_color", Colors.White);
		btnFilter.AddThemeStyleboxOverride("normal", btnNormal);
		btnFilter.AddThemeStyleboxOverride("hover", btnHover);
		btnFilter.AddThemeStyleboxOverride("pressed", btnNormal);
		btnFilter.AddThemeStyleboxOverride("focus", btnNormal);
		btnFilter.Pressed += delegate
		{
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.45f);
			ToggleLootFilter();
		};
		_utilityBarPanel.AddChild(btnFilter);

		// 2. [夜視]
		_btnUtilityNightVision = new Button
		{
			Text = "夜視",
			Position = new Vector2(54f, 2f),
			Size = new Vector2(33f, 18f),
			CustomMinimumSize = new Vector2(33f, 18f),
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		_btnUtilityNightVision.AddThemeFontSizeOverride("font_size", 10);
		_btnUtilityNightVision.AddThemeStyleboxOverride("normal", btnNormal);
		_btnUtilityNightVision.AddThemeStyleboxOverride("hover", btnHover);
		_btnUtilityNightVision.AddThemeStyleboxOverride("pressed", btnNormal);
		_btnUtilityNightVision.AddThemeStyleboxOverride("focus", btnNormal);
		_btnUtilityNightVision.Pressed += delegate
		{
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.45f);
			ToggleElfNightVision();
			RefreshUtilityNightVisionBtn();
		};
		RefreshUtilityNightVisionBtn();
		_utilityBarPanel.AddChild(_btnUtilityNightVision);

		// 3. [快捷]
		_floatingQuickBarToggle = new Button
		{
			Position = new Vector2(90f, 2f),
			Size = new Vector2(33f, 18f),
			CustomMinimumSize = new Vector2(33f, 18f),
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		_floatingQuickBarToggle.AddThemeFontSizeOverride("font_size", 10);
		_floatingQuickBarToggle.AddThemeStyleboxOverride("normal", btnNormal);
		_floatingQuickBarToggle.AddThemeStyleboxOverride("hover", btnHover);
		_floatingQuickBarToggle.AddThemeStyleboxOverride("pressed", btnNormal);
		_floatingQuickBarToggle.AddThemeStyleboxOverride("focus", btnNormal);

		void RefreshToggleText()
		{
			if (_session.FloatingQuickBarVisible)
			{
				_floatingQuickBarToggle.Text = "收起";
				_floatingQuickBarToggle.AddThemeColorOverride("font_color", Color.FromHtml("#00ffcc"));
				_floatingQuickBarToggle.AddThemeColorOverride("font_hover_color", Color.FromHtml("#33ffdd"));
				_floatingQuickBarToggle.TooltipText = "懸浮快捷列：已顯示（點擊收起）";
			}
			else
			{
				_floatingQuickBarToggle.Text = "快捷";
				_floatingQuickBarToggle.AddThemeColorOverride("font_color", Color.FromHtml("#888888"));
				_floatingQuickBarToggle.AddThemeColorOverride("font_hover_color", Color.FromHtml("#aaaaaa"));
				_floatingQuickBarToggle.TooltipText = "懸浮快捷列：已收起（點擊開啟）";
			}
			if (_floatingQuickBarPanel != null)
			{
				_floatingQuickBarPanel.Visible = _session.FloatingQuickBarVisible;
			}
		}

		RefreshToggleText();

		_floatingQuickBarToggle.Pressed += delegate
		{
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.45f);
			_session.FloatingQuickBarVisible = !_session.FloatingQuickBarVisible;
			RefreshToggleText();
			SaveManager.Save(_session);
		};
		_utilityBarPanel.AddChild(_floatingQuickBarToggle);

		_hud.AddChild(_utilityBarPanel, forceReadableName: false, InternalMode.Disabled);
	}

	private void RefreshUtilityNightVisionBtn()
	{
		if (_btnUtilityNightVision == null)
		{
			return;
		}
		bool on = _session.ElfNightVisionEnabled;
		if (on)
		{
			_btnUtilityNightVision.Text = "夜視";
			_btnUtilityNightVision.AddThemeColorOverride("font_color", Color.FromHtml("#ffe066"));
			_btnUtilityNightVision.AddThemeColorOverride("font_hover_color", Color.FromHtml("#fff2a8"));
			_btnUtilityNightVision.TooltipText = "夜視功能：已開啟（點擊關閉）";
		}
		else
		{
			_btnUtilityNightVision.Text = "夜視";
			_btnUtilityNightVision.AddThemeColorOverride("font_color", Color.FromHtml("#888888"));
			_btnUtilityNightVision.AddThemeColorOverride("font_hover_color", Color.FromHtml("#aaaaaa"));
			_btnUtilityNightVision.TooltipText = "夜視功能：已關閉（點擊開啟）";
		}
	}

	private void BuildFloatingQuickBar()
	{
		_floatingQuickBarPanel?.QueueFree();
		Array.Clear(_floatingQuickSlotAction);

		GameData shared = GameDataProvider.Shared;
		string[] skillLayout = QuickBarSkillLayout();

		float panelW = 144f;
		float panelH = 92f;

		Vector2 defaultBottomRight = new Vector2(Math.Max(0f, _viewW - panelW - 4f), Math.Max(0f, RnPanelY - panelH - 4f));
		Vector2 savedPos = _session.FloatingQuickBarPos;
		if (savedPos.X <= 0f || savedPos.Y <= 0f || savedPos.X > _viewW || savedPos.Y > _viewH ||
		    (Math.Abs(savedPos.X - 490f) < 2f && Math.Abs(savedPos.Y - 430f) < 2f))
		{
			savedPos = defaultBottomRight;
		}
		savedPos.X = Math.Clamp(savedPos.X, 0f, Math.Max(0f, _viewW - panelW));
		savedPos.Y = Math.Clamp(savedPos.Y, 0f, Math.Max(0f, _viewH - panelH));
		_session.FloatingQuickBarPos = savedPos;

		Panel panel = new Panel
		{
			Position = savedPos,
			Size = new Vector2(panelW, panelH),
			CustomMinimumSize = new Vector2(panelW, panelH),
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 2100,
			Visible = _session.FloatingQuickBarVisible
		};

		StyleBoxFlat bg = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
			BorderColor = Color.FromHtml("#3a404d"),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3
		};
		panel.AddThemeStyleboxOverride("panel", bg);

		Control titleBar = new Control
		{
			Position = Vector2.Zero,
			Size = new Vector2(panelW, 18f),
			CustomMinimumSize = new Vector2(panelW, 18f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = "按住左鍵可拖曳懸浮快捷列"
		};

		Label titleLbl = new Label
		{
			Text = "快捷列",
			Position = new Vector2(6f, 1f),
			Size = new Vector2(panelW - 24f, 16f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		titleLbl.AddThemeFontSizeOverride("font_size", 10);
		titleLbl.AddThemeColorOverride("font_color", Color.FromHtml("#a0aab8"));
		titleBar.AddChild(titleLbl);

		bool isDragging = false;
		Vector2 dragOffset = Vector2.Zero;
		titleBar.GuiInput += delegate(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					isDragging = true;
					dragOffset = mb.GlobalPosition - panel.GlobalPosition;
				}
				else
				{
					isDragging = false;
					_session.FloatingQuickBarPos = panel.Position;
					SaveManager.Save(_session);
				}
				titleBar.AcceptEvent();
			}
			else if (@event is InputEventMouseMotion mm && isDragging)
			{
				Vector2 newPos = mm.GlobalPosition - dragOffset;
				newPos.X = Math.Clamp(newPos.X, 0f, Math.Max(0f, _viewW - panelW));
				newPos.Y = Math.Clamp(newPos.Y, 0f, Math.Max(0f, _viewH - panelH));
				panel.Position = newPos;
				_session.FloatingQuickBarPos = newPos;
				titleBar.AcceptEvent();
			}
		};
		panel.AddChild(titleBar);

		Button closeBtn = new Button
		{
			Text = "×",
			Position = new Vector2(panelW - 17f, 1f),
			Size = new Vector2(14f, 14f),
			CustomMinimumSize = new Vector2(14f, 14f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop,
			TooltipText = "收起懸浮快捷列"
		};
		closeBtn.AddThemeFontSizeOverride("font_size", 10);
		closeBtn.AddThemeColorOverride("font_color", Color.FromHtml("#888888"));
		StyleBoxFlat closeStyle = new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0f)
		};
		closeStyle.SetContentMarginAll(0f);
		foreach (string s in new[] { "normal", "hover", "pressed", "focus" })
		{
			closeBtn.AddThemeStyleboxOverride(s, closeStyle);
		}
		closeBtn.Pressed += delegate
		{
			_session.FloatingQuickBarVisible = false;
			panel.Visible = false;
			if (_floatingQuickBarToggle != null)
			{
				_floatingQuickBarToggle.Text = "快捷";
				_floatingQuickBarToggle.AddThemeColorOverride("font_color", Color.FromHtml("#888888"));
				_floatingQuickBarToggle.TooltipText = "懸浮快捷列：已收起（點擊開啟）";
			}
			SaveManager.Save(_session);
		};
		panel.AddChild(closeBtn);

		for (int k = 0; k < 8; k++)
		{
			int globalSlot = 16 + k;
			int col = k % 4;
			int row = k / 4;
			Vector2 slotPos = new Vector2(4f + (float)col * 34f, 19f + (float)row * 34f);

			string itemAssignment = _session.QuickItems[globalSlot];
			string skillId = skillLayout.Length > globalSlot ? skillLayout[globalSlot] : null;

			if (!string.IsNullOrEmpty(itemAssignment))
			{
				var (itemKey, stackUid, _) = QuickBar.DecodeAssignment(itemAssignment);
				if (!string.IsNullOrWhiteSpace(itemKey))
				{
					BuildFloatingQuickItemSlot(panel, k, globalSlot, slotPos, itemAssignment, itemKey, stackUid, shared);
					continue;
				}
			}

			if (!string.IsNullOrEmpty(skillId))
			{
				BuildFloatingQuickSkillSlot(panel, k, globalSlot, slotPos, skillId);
				continue;
			}

			BuildFloatingQuickEmptyTarget(panel, k, globalSlot, slotPos);
		}

		_floatingQuickBarPanel = panel;
		_hud.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildFloatingQuickItemSlot(Control parent, int localIdx, int globalSlot, Vector2 position, string assignment, string itemKey, string stackUid, GameData data)
	{
		string text = data.Item(itemKey)?["n"]?.GetValue<string>() ?? itemKey;
		QuickSlotButton btn = new QuickSlotButton
		{
			Slot = globalSlot,
			OnDropItem = OnQuickDrop,
			Text = "",
			Position = position,
			Size = QuickSlotSize,
			CustomMinimumSize = QuickSlotSize,
			TooltipText = $"{text}（懸浮第 {localIdx + 1} 格·可拖曳替換·拖到空白處取消）",
			DragPayload = assignment,
			DragLabel = text,
			OnDragOut = ClearQuickSlot,
			OnActivate = delegate
			{
				UseQuickItem(globalSlot, assignment);
			}
		};
		btn.SetIcon(ItemIcons.For(itemKey));
		if (QuickBar.CanAutoUse(data, itemKey))
		{
			AttachAutoBadge(btn, _session.AutoUseItems.Contains(itemKey), AutoUseHint(data, itemKey), delegate(bool on)
			{
				if (on)
				{
					_session.AutoUseItems.Add(itemKey);
				}
				else
				{
					_session.AutoUseItems.Remove(itemKey);
				}
				SaveManager.Save(_session);
			});
		}
		parent.AddChild(btn, forceReadableName: false, InternalMode.Disabled);
		_quickItemBtns.Add((globalSlot, assignment, itemKey, stackUid, btn));
		_floatingQuickSlotAction[localIdx] = delegate
		{
			UseQuickItem(globalSlot, assignment);
		};
	}

	private void BuildFloatingQuickSkillSlot(Control parent, int localIdx, int globalSlot, Vector2 position, string skillId)
	{
		bool isTeleport = skillId == "sk_teleport";
		bool isManual = SkillExecutionRules.IsManualOnly(GameDataProvider.Shared, skillId);
		int mpCost = PlayerSkillMpCost(skillId);
		string id = skillId;
		bool isSummon = SummonRules.SkillIds.Contains(id);
		Texture2D texture2D = SkillIcons.For(id);

		QuickSlotButton btn = new QuickSlotButton
		{
			Slot = globalSlot,
			OnDropItem = OnQuickDrop,
			DragPayload = "skill:" + id,
			DragLabel = SkillInfo.Name(id),
			OnDragOut = ClearQuickSlot,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Text = (texture2D == null ? SkillInfo.Name(id) : ""),
			Position = position,
			Size = QuickSlotSize,
			CustomMinimumSize = QuickSlotSize
		};
		string tooltip = $"{SkillInfo.Name(id)}（{SkillInfo.ResourceLabel(id, mpCost)} · 懸浮第 {localIdx + 1} 格）";
		if (isSummon) tooltip += "\n點擊選擇召喚形態";
		string usageDesc = SkillInfo.UsageDescription(id);
		if (!string.IsNullOrEmpty(usageDesc)) tooltip += "\n" + usageDesc;
		tooltip += "\n可拖曳替換·拖到空白處取消";
		btn.TooltipText = tooltip;
		btn.SetQuickBarSkillIcon(texture2D);
		btn.AddThemeFontSizeOverride("font_size", 11);

		if (!isTeleport && !SkillInfo.IsProcOnly(id) && !isManual)
		{
			AttachAutoBadge(btn, _session.AutoCast.Contains(id), AutoCastHint(id), delegate(bool on)
			{
				SetAutoSkillEnabled(id, on);
			});
		}

		Action act = isTeleport ? new Action(CastTeleportSkill) : (isSummon ? ((Action)delegate
		{
			ToggleSummonPicker(id);
		}) : ((Action)delegate
		{
			TryCastPlayerSkill(id);
		}));

		btn.OnActivate = act;
		_floatingQuickSlotAction[localIdx] = (isSummon ? ((Action)delegate { TryCastPlayerSkill(id); }) : act);
		parent.AddChild(btn, forceReadableName: false, InternalMode.Disabled);
		_skillBtns.Add((id, btn, mpCost));
	}

	private void BuildFloatingQuickEmptyTarget(Control parent, int localIdx, int globalSlot, Vector2 position)
	{
		Panel emptyFrame = new Panel
		{
			Position = position,
			Size = QuickSlotSize,
			CustomMinimumSize = QuickSlotSize,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		StyleBoxFlat emptyBox = new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0.4f),
			BorderColor = Color.FromHtml("#2c3038"),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1
		};
		emptyFrame.AddThemeStyleboxOverride("panel", emptyBox);
		parent.AddChild(emptyFrame, forceReadableName: false, InternalMode.Disabled);

		QuickSlotTarget target = new QuickSlotTarget
		{
			Slot = globalSlot,
			OnDropItem = OnQuickDrop,
			Position = position,
			Size = QuickSlotSize,
			TooltipText = $"懸浮快捷第 {localIdx + 1} 格（從背包拖曳道具、或從技能視窗拖曳技能到這裡）"
		};
		parent.AddChild(target, forceReadableName: false, InternalMode.Disabled);
		_quickEmptyTargets.Add(target);
	}
}
