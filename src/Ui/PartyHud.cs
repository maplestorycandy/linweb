using System;
using System.Collections.Generic;
using Godot;
using IdleLineage.App;
using IdleLineage.Combat;

namespace IdleLineage.Ui;

public sealed partial class PartyHud : Control
{
    public sealed class PartyMemberData
    {
        public string PlayerId { get; set; } = "";
        public string Name { get; set; } = "";
        public string ClassId { get; set; } = "knight";
        public int Index { get; set; } = 1;
        public bool IsLeader { get; set; } = false;
        public double Hp { get; set; } = 100;
        public double MaxHp { get; set; } = 100;
        public double Mp { get; set; } = 100;
        public double MaxMp { get; set; } = 100;
        public bool IsOfflineCompanion { get; set; } = false;
        public int Slot { get; set; } = 0;
    }

    private sealed partial class PartyMemberRow : Control
    {
        private readonly TextureRect _crownRect;
        private readonly Label _indexLabel;
        private readonly TextureRect _classIcon;
        private readonly Label _nameLabel;
        private readonly ColorRect _hpFill;
        private readonly ColorRect _mpFill;
        private readonly Button _tpBtn;
        private readonly Button _followBtn;
        private readonly Button _offlineBtn;
        private string _loadedClassId = "";

        public PartyMemberData? Data { get; private set; }
        public Action<string>? OnRowClicked { get; set; }
        public Action<string>? OnTeleportClicked { get; set; }
        public Action<string>? OnFollowClicked { get; set; }
        public Action? OnOfflineClicked { get; set; }
        public Action<string>? OnDismissCompanionClicked { get; set; }

        public PartyMemberRow()
        {
            CustomMinimumSize = new Vector2(284f, 42f);
            Size = new Vector2(284f, 42f);
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.PointingHand;

            // Crown Icon (for party leader / host)
            _crownRect = new TextureRect
            {
                Position = new Vector2(2f, 1f),
                Size = new Vector2(18f, 13f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            Texture2D? crownTex = GetCrownTexture();
            if (crownTex != null) _crownRect.Texture = crownTex;
            AddChild(_crownRect);

            // Member Index (1, 2...)
            _indexLabel = new Label
            {
                Position = new Vector2(2f, 13f),
                Size = new Vector2(16f, 22f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _indexLabel.AddThemeFontSizeOverride("font_size", 14);
            _indexLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 1.0f));
            _indexLabel.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.18f, 0.38f));
            _indexLabel.AddThemeConstantOverride("outline_size", 3);
            AddChild(_indexLabel);

            // Class Icon
            _classIcon = new TextureRect
            {
                Position = new Vector2(20f, 6f),
                Size = new Vector2(28f, 28f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_classIcon);

            // Right side stats container
            var statsBox = new VBoxContainer
            {
                Position = new Vector2(52f, 2f),
                Size = new Vector2(106f, 38f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            statsBox.AddThemeConstantOverride("separation", 2);
            AddChild(statsBox);

            // Name Label
            _nameLabel = new Label
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 12);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.98f, 1.0f));
            _nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            _nameLabel.AddThemeConstantOverride("outline_size", 2);
            statsBox.AddChild(_nameLabel);

            // HP Bar Panel
            var hpBg = new Panel
            {
                CustomMinimumSize = new Vector2(106f, 7f),
                Size = new Vector2(106f, 7f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            var hpStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.03f, 0.03f, 0.95f),
                BorderColor = new Color(0.08f, 0.01f, 0.01f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 1,
                CornerRadiusBottomRight = 1,
                CornerRadiusTopLeft = 1,
                CornerRadiusTopRight = 1
            };
            hpBg.AddThemeStyleboxOverride("panel", hpStyle);

            _hpFill = new ColorRect
            {
                Position = new Vector2(1f, 1f),
                Size = new Vector2(104f, 5f),
                Color = new Color(0.88f, 0.16f, 0.16f, 1f), // Vibrant Red
                MouseFilter = MouseFilterEnum.Ignore
            };
            hpBg.AddChild(_hpFill);
            statsBox.AddChild(hpBg);

            // MP Bar Panel
            var mpBg = new Panel
            {
                CustomMinimumSize = new Vector2(106f, 5f),
                Size = new Vector2(106f, 5f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            var mpStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.03f, 0.08f, 0.18f, 0.95f),
                BorderColor = new Color(0.01f, 0.03f, 0.08f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 1,
                CornerRadiusBottomRight = 1,
                CornerRadiusTopLeft = 1,
                CornerRadiusTopRight = 1
            };
            mpBg.AddThemeStyleboxOverride("panel", mpStyle);

            _mpFill = new ColorRect
            {
                Position = new Vector2(1f, 1f),
                Size = new Vector2(104f, 3f),
                Color = new Color(0.15f, 0.42f, 0.95f, 1f), // Vibrant Blue
                MouseFilter = MouseFilterEnum.Ignore
            };
            mpBg.AddChild(_mpFill);
            statsBox.AddChild(mpBg);

            // Quick Teleport Button to teammate
            _tpBtn = new Button
            {
                Text = "傳送",
                Position = new Vector2(162f, 7f),
                Size = new Vector2(36f, 28f),
                FocusMode = FocusModeEnum.None,
                TooltipText = "⚡ 快速傳送至隊友身邊",
                MouseDefaultCursorShape = CursorShape.PointingHand,
                Visible = false
            };
            _tpBtn.AddThemeFontSizeOverride("font_size", 11);
            _tpBtn.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 1.0f));
            _tpBtn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.6f));

            var btnNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.16f, 0.25f, 0.90f),
                BorderColor = new Color(0.35f, 0.60f, 0.85f, 0.85f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _tpBtn.AddThemeStyleboxOverride("normal", btnNormal);

            var btnHover = new StyleBoxFlat
            {
                BgColor = new Color(0.16f, 0.32f, 0.52f, 0.95f),
                BorderColor = new Color(0.65f, 0.90f, 1.0f, 1.0f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _tpBtn.AddThemeStyleboxOverride("hover", btnHover);

            var btnPressed = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.12f, 0.20f, 0.95f),
                BorderColor = new Color(0.4f, 0.7f, 0.9f, 1.0f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _tpBtn.AddThemeStyleboxOverride("pressed", btnPressed);

            _tpBtn.Pressed += () =>
            {
                if (Data != null && !string.IsNullOrEmpty(Data.PlayerId))
                {
                    OnTeleportClicked?.Invoke(Data.PlayerId);
                }
            };
            AddChild(_tpBtn);

            // Auto Follow Button
            _followBtn = new Button
            {
                Text = "跟隨",
                Position = new Vector2(202f, 7f),
                Size = new Vector2(38f, 28f),
                FocusMode = FocusModeEnum.None,
                TooltipText = "🐾 自動跟隨隊友移動 (再次點擊取消)",
                MouseDefaultCursorShape = CursorShape.PointingHand,
                Visible = false
            };
            _followBtn.AddThemeFontSizeOverride("font_size", 10);
            _followBtn.AddThemeColorOverride("font_color", new Color(0.65f, 0.98f, 0.75f));
            _followBtn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.6f));

            var followNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.20f, 0.15f, 0.90f),
                BorderColor = new Color(0.30f, 0.70f, 0.45f, 0.85f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _followBtn.AddThemeStyleboxOverride("normal", followNormal);

            var followHover = new StyleBoxFlat
            {
                BgColor = new Color(0.16f, 0.35f, 0.25f, 0.95f),
                BorderColor = new Color(0.50f, 0.95f, 0.65f, 1.0f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _followBtn.AddThemeStyleboxOverride("hover", followHover);

            var followPressed = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.16f, 0.12f, 0.95f),
                BorderColor = new Color(0.3f, 0.8f, 0.5f, 1.0f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _followBtn.AddThemeStyleboxOverride("pressed", followPressed);

            _followBtn.Pressed += () =>
            {
                if (Data != null && !string.IsNullOrEmpty(Data.PlayerId))
                {
                    OnFollowClicked?.Invoke(Data.PlayerId);
                }
            };
            AddChild(_followBtn);

            // Auto-Offline / Offline Companion Button
            _offlineBtn = new Button
            {
                Text = "⚡ 自動離線",
                Position = new Vector2(156f, 7f),
                Size = new Vector2(122f, 28f),
                FocusMode = FocusModeEnum.None,
                TooltipText = "⚡ 設定此角色在線掛機託管，並返回選角畫面切換/創角",
                MouseDefaultCursorShape = CursorShape.PointingHand,
                Visible = false
            };
            _offlineBtn.AddThemeFontSizeOverride("font_size", 11);
            _offlineBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.40f));
            _offlineBtn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.80f));

            var offlineNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.13f, 0.05f, 0.90f),
                BorderColor = new Color(0.85f, 0.65f, 0.20f, 0.85f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _offlineBtn.AddThemeStyleboxOverride("normal", offlineNormal);

            var offlineHover = new StyleBoxFlat
            {
                BgColor = new Color(0.28f, 0.20f, 0.06f, 0.95f),
                BorderColor = new Color(1.0f, 0.85f, 0.35f, 1.0f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _offlineBtn.AddThemeStyleboxOverride("hover", offlineHover);

            var offlinePressed = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.08f, 0.03f, 0.95f),
                BorderColor = new Color(0.70f, 0.50f, 0.15f, 1.0f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _offlineBtn.AddThemeStyleboxOverride("pressed", offlinePressed);

            _offlineBtn.Pressed += () =>
            {
                if (Data != null)
                {
                    if (Data.IsOfflineCompanion)
                    {
                        OnDismissCompanionClicked?.Invoke(Data.PlayerId);
                    }
                    else
                    {
                        OnOfflineClicked?.Invoke();
                    }
                }
            };
            AddChild(_offlineBtn);
        }

        public void Update(PartyMemberData data, string localPlayerId = "", string followingPlayerId = "")
        {
            Data = data;
            _indexLabel.Text = data.Index.ToString();
            _crownRect.Visible = data.IsLeader;

            // Update Class Icon if changed
            string normClass = NormalizeClassId(data.ClassId);
            if (_loadedClassId != normClass)
            {
                _loadedClassId = normClass;
                Texture2D classTex = LoadTextureSafely($"res://assets/ui/classicons/{normClass}.png");
                if (classTex == null)
                {
                    classTex = LoadTextureSafely($"res://assets/ui/web-login/class-icons/{normClass}_normal.png");
                }
                if (classTex != null)
                {
                    _classIcon.Texture = classTex;
                }
            }

            // Update Name
            _nameLabel.Text = string.IsNullOrEmpty(data.Name) ? $"成員 {data.Index}" : data.Name;

            // HP Bar Ratio
            double maxHp = Math.Max(1.0, data.MaxHp);
            float hpRatio = (float)Math.Clamp(data.Hp / maxHp, 0.0, 1.0);
            _hpFill.Size = new Vector2(Mathf.Round(104f * hpRatio), 5f);

            // MP Bar Ratio
            double maxMp = Math.Max(1.0, data.MaxMp);
            float mpRatio = (float)Math.Clamp(data.Mp / maxMp, 0.0, 1.0);
            _mpFill.Size = new Vector2(Mathf.Round(104f * mpRatio), 3f);

            // Death state styling
            if (data.Hp <= 0.0)
            {
                _nameLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 0.8f));
                _hpFill.Color = new Color(0.4f, 0.1f, 0.1f, 0.8f);
            }
            else
            {
                _nameLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.98f, 1.0f));
                _hpFill.Color = new Color(0.88f, 0.16f, 0.16f, 1f);
            }

            // Quick Teleport, Follow & Offline buttons
            bool isTeammate = !string.IsNullOrEmpty(data.PlayerId) && data.PlayerId != localPlayerId;
            if (isTeammate)
            {
                _tpBtn.Visible = true;
                _followBtn.Visible = true;

                if (data.IsOfflineCompanion)
                {
                    _tpBtn.Position = new Vector2(156f, 7f);
                    _tpBtn.Size = new Vector2(36f, 28f);
                    _followBtn.Position = new Vector2(195f, 7f);
                    _followBtn.Size = new Vector2(38f, 28f);

                    _offlineBtn.Visible = true;
                    _offlineBtn.Position = new Vector2(236f, 7f);
                    _offlineBtn.Size = new Vector2(44f, 28f);
                    _offlineBtn.Text = "召回";
                    _offlineBtn.TooltipText = "💤 點擊收回此離線託管隊友 (保存進度並下線)";
                    _offlineBtn.AddThemeFontSizeOverride("font_size", 10);
                    _offlineBtn.AddThemeColorOverride("font_color", new Color(0.70f, 0.85f, 1.0f));
                }
                else
                {
                    _tpBtn.Position = new Vector2(162f, 7f);
                    _tpBtn.Size = new Vector2(56f, 28f);
                    _followBtn.Position = new Vector2(222f, 7f);
                    _followBtn.Size = new Vector2(58f, 28f);
                    _offlineBtn.Visible = false;
                }

                bool isFollowing = !string.IsNullOrEmpty(followingPlayerId) && followingPlayerId == data.PlayerId;
                if (isFollowing)
                {
                    _followBtn.Text = "跟隨中";
                    _followBtn.AddThemeColorOverride("font_color", new Color(0.35f, 1.0f, 0.5f));
                    var activeStyle = new StyleBoxFlat
                    {
                        BgColor = new Color(0.12f, 0.32f, 0.20f, 0.95f),
                        BorderColor = new Color(0.45f, 1.0f, 0.65f, 1.0f),
                        BorderWidthBottom = 1,
                        BorderWidthTop = 1,
                        BorderWidthLeft = 1,
                        BorderWidthRight = 1,
                        CornerRadiusBottomLeft = 3,
                        CornerRadiusBottomRight = 3,
                        CornerRadiusTopLeft = 3,
                        CornerRadiusTopRight = 3
                    };
                    _followBtn.AddThemeStyleboxOverride("normal", activeStyle);
                }
                else
                {
                    _followBtn.Text = "跟隨";
                    _followBtn.AddThemeColorOverride("font_color", new Color(0.65f, 0.98f, 0.75f));
                    var normalStyle = new StyleBoxFlat
                    {
                        BgColor = new Color(0.10f, 0.20f, 0.15f, 0.90f),
                        BorderColor = new Color(0.30f, 0.70f, 0.45f, 0.85f),
                        BorderWidthBottom = 1,
                        BorderWidthTop = 1,
                        BorderWidthLeft = 1,
                        BorderWidthRight = 1,
                        CornerRadiusBottomLeft = 3,
                        CornerRadiusBottomRight = 3,
                        CornerRadiusTopLeft = 3,
                        CornerRadiusTopRight = 3
                    };
                    _followBtn.AddThemeStyleboxOverride("normal", normalStyle);
                }
            }
            else
            {
                // Local player (Self): show [⚡ 自動離線] button next to stats
                _tpBtn.Visible = false;
                _followBtn.Visible = false;

                _offlineBtn.Visible = true;
                _offlineBtn.Position = new Vector2(156f, 7f);
                _offlineBtn.Size = new Vector2(124f, 28f);
                _offlineBtn.Text = "⚡ 自動離線";
                _offlineBtn.TooltipText = "⚡ 設定此角色在線掛機託管，並返回選角畫面切換/創角（本地多開同行）";
                _offlineBtn.AddThemeFontSizeOverride("font_size", 11);
                _offlineBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.40f));
            }

            TooltipText = $"{data.Name} [{GetClassDisplayName(normClass)}]\nHP: {(int)data.Hp} / {(int)maxHp} ({(int)(hpRatio * 100)}%)\nMP: {(int)data.Mp} / {(int)maxMp} ({(int)(mpRatio * 100)}%)";
        }

        public override void _GuiInput(InputEvent @event)
        {
            base._GuiInput(@event);
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                if (Data != null && !string.IsNullOrEmpty(Data.PlayerId))
                {
                    OnRowClicked?.Invoke(Data.PlayerId);
                    AcceptEvent();
                }
            }
        }
    }

    public event Action<string>? OnMemberClicked;
    public event Action<string>? OnTeleportToMemberClicked;
    public event Action<string>? OnFollowToMemberClicked;
    public event Action? OnOfflineClicked;
    public event Action<string>? OnDismissCompanionClicked;
    public string FollowingPlayerId { get; set; } = "";

    public static bool IsCollapsedGlobally { get; set; } = false;

    private readonly PanelContainer _panel;
    private readonly HBoxContainer _headerBox;
    private readonly Label _titleLabel;
    private readonly Button _toggleBtn;
    private readonly VBoxContainer _membersList;
    private readonly List<PartyMemberRow> _rows = new();
    private bool _isDragging = false;
    private bool _hasDragged = false;
    private Vector2 _dragOffset;
    private bool _isCollapsed = false;

    public PartyHud()
    {
        Name = "PartyHud";
        ZIndex = 2100;
        CustomMinimumSize = new Vector2(292f, 40f);
        Position = new Vector2(14f, 110f); // Default left side of screen
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _isCollapsed = IsCollapsedGlobally;

        // Subtle dark fantasy border & backing
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.10f, 0.75f),
            BorderColor = new Color(0.25f, 0.35f, 0.50f, 0.70f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };

        _panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_panel);

        var rootBox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        rootBox.AddThemeConstantOverride("separation", 3);
        _panel.AddChild(rootBox);

        // Header bar with Title and Collapse Corner Button
        _headerBox = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 18f),
            MouseFilter = MouseFilterEnum.Pass
        };
        _headerBox.AddThemeConstantOverride("separation", 4);
        rootBox.AddChild(_headerBox);

        _titleLabel = new Label
        {
            Text = "⚔️ 隊伍",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 11);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.90f, 1.0f));
        _titleLabel.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.14f, 0.25f));
        _titleLabel.AddThemeConstantOverride("outline_size", 2);
        _headerBox.AddChild(_titleLabel);

        _toggleBtn = new Button
        {
            Text = _isCollapsed ? "▼" : "▲",
            CustomMinimumSize = new Vector2(20f, 18f),
            Size = new Vector2(20f, 18f),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = _isCollapsed ? "點擊展開隊伍欄" : "點擊收折隊伍欄（縮小不擋視線）",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            FocusMode = FocusModeEnum.None
        };
        _toggleBtn.AddThemeFontSizeOverride("font_size", 9);
        _toggleBtn.AddThemeColorOverride("font_color", new Color(0.80f, 0.88f, 1.0f));
        _toggleBtn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.90f, 0.40f));

        var toggleNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.18f, 0.28f, 0.85f),
            BorderColor = new Color(0.35f, 0.50f, 0.70f, 0.80f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        _toggleBtn.AddThemeStyleboxOverride("normal", toggleNormal);

        var toggleHover = new StyleBoxFlat
        {
            BgColor = new Color(0.20f, 0.32f, 0.50f, 0.95f),
            BorderColor = new Color(0.55f, 0.80f, 1.0f, 1.0f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        _toggleBtn.AddThemeStyleboxOverride("hover", toggleHover);

        var togglePressed = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.14f, 0.22f, 0.95f),
            BorderColor = new Color(0.30f, 0.60f, 0.85f, 1.0f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        _toggleBtn.AddThemeStyleboxOverride("pressed", togglePressed);

        _toggleBtn.Pressed += ToggleCollapse;
        _headerBox.AddChild(_toggleBtn);

        _membersList = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            Visible = !_isCollapsed
        };
        _membersList.AddThemeConstantOverride("separation", 6);
        rootBox.AddChild(_membersList);
    }

    public void ToggleCollapse()
    {
        _isCollapsed = !_isCollapsed;
        IsCollapsedGlobally = _isCollapsed;
        _membersList.Visible = !_isCollapsed;
        _toggleBtn.Text = _isCollapsed ? "▼" : "▲";
        _toggleBtn.TooltipText = _isCollapsed ? "點擊展開隊伍欄" : "點擊收折隊伍欄（縮小不擋視線）";
        AdjustSize();
        GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.35f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _isDragging = true;
                    _hasDragged = false;
                    _dragOffset = mb.GlobalPosition - GlobalPosition;
                    AcceptEvent();
                }
                else if (_isDragging)
                {
                    _isDragging = false;
                    if (!_hasDragged && _isCollapsed)
                    {
                        ToggleCollapse();
                    }
                    AcceptEvent();
                }
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            _hasDragged = true;
            Vector2 targetPos = mm.GlobalPosition - _dragOffset;
            Vector2 viewportSize = GetViewportRect().Size;
            targetPos.X = Mathf.Clamp(targetPos.X, 0f, Math.Max(0f, viewportSize.X - Size.X));
            targetPos.Y = Mathf.Clamp(targetPos.Y, 0f, Math.Max(0f, viewportSize.Y - Size.Y));
            Position = targetPos;
            AcceptEvent();
        }
    }

    public void UpdateMembers(IReadOnlyList<PartyMemberData> members, string localPlayerId = "")
    {
        _titleLabel.Text = $"⚔️ 隊伍 ({members.Count})";

        while (_rows.Count < members.Count)
        {
            var row = new PartyMemberRow();
            row.OnRowClicked = (id) => OnMemberClicked?.Invoke(id);
            row.OnTeleportClicked = (id) => OnTeleportToMemberClicked?.Invoke(id);
            row.OnFollowClicked = (id) => OnFollowToMemberClicked?.Invoke(id);
            row.OnOfflineClicked = () => OnOfflineClicked?.Invoke();
            row.OnDismissCompanionClicked = (id) => OnDismissCompanionClicked?.Invoke(id);
            _rows.Add(row);
            _membersList.AddChild(row);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (i < members.Count)
            {
                _rows[i].Visible = true;
                _rows[i].Update(members[i], localPlayerId, FollowingPlayerId);
            }
            else
            {
                _rows[i].Visible = false;
            }
        }

        // Adjust overall panel height automatically
        CallDeferred(nameof(AdjustSize));
    }

    private void AdjustSize()
    {
        if (_isCollapsed)
        {
            CustomMinimumSize = new Vector2(106f, 26f);
            Size = new Vector2(106f, 26f);
            return;
        }

        float totalHeight = 10f;
        totalHeight += 22f; // header
        foreach (var r in _rows)
        {
            if (r.Visible) totalHeight += r.Size.Y + 6f;
        }
        CustomMinimumSize = new Vector2(292f, Math.Max(32f, totalHeight));
        Size = new Vector2(292f, Math.Max(32f, totalHeight));
    }

    public static string NormalizeClassId(string classId)
    {
        if (string.IsNullOrEmpty(classId)) return "knight";
        string lower = classId.ToLowerInvariant().Trim();
        return lower switch
        {
            "royal" or "prince" or "princess" or "王族" or "王子" or "公主" => "royal",
            "knight" or "騎士" or "男騎士" or "女騎士" => "knight",
            "elf" or "妖精" or "男妖精" or "女妖精" => "elf",
            "mage" or "wizard" or "法師" or "男法師" or "女法師" => "mage",
            "dark" or "darkelf" or "黑妖" or "黑暗妖精" or "男黑暗妖精" or "女黑暗妖精" => "dark",
            "illusion" or "illusionist" or "幻術" or "幻術師" or "幻術士" or "男幻術士" or "女幻術士" => "illusion",
            "dragon" or "dragonknight" or "dknight" or "龍騎" or "龍騎士" or "男龍騎士" or "女龍騎士" => "dragon",
            "warrior" or "戰士" or "男戰士" or "女戰士" => "warrior",
            _ => "knight"
        };
    }

    public static string GetClassDisplayName(string normClass)
    {
        return normClass switch
        {
            "royal" => "王族",
            "knight" => "騎士",
            "elf" => "妖精",
            "mage" => "法師",
            "dark" => "黑暗妖精",
            "illusion" => "幻術師",
            "dragon" => "龍騎士",
            "warrior" => "戰士",
            _ => "冒險者"
        };
    }

    public static Texture2D LoadTextureSafely(string resPath)
    {
        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                var res = GD.Load<Texture2D>(resPath);
                if (res != null) return res;
            }
        }
        catch { }

        try
        {
            string globalPath = ProjectSettings.GlobalizePath(resPath);
            if (System.IO.File.Exists(globalPath))
            {
                var img = Image.LoadFromFile(globalPath);
                if (img != null)
                {
                    return ImageTexture.CreateFromImage(img);
                }
            }
        }
        catch { }

        return null!;
    }

    private static Texture2D? _crownTextureCached;
    public static Texture2D? GetCrownTexture()
    {
        if (_crownTextureCached != null) return _crownTextureCached;
        var tex = LoadTextureSafely("res://assets/ui/party_crown.png");
        if (tex != null)
        {
            _crownTextureCached = tex;
            return tex;
        }
        try
        {
            const string b64 = "iVBORw0KGgoAAAANSUhEUgAAABIAAAAOCAYAAAAi2ky3AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAG5SURBVDhPY2CgF/j//z/HiydXOv//f2+ALkcS2LNpdsr//x/+P7pzagu6HEkg3IL3/O6lWf9T3eXv2MszcKDLo4And0/6vHx6uQxd3FOTLaA8TO7D63OZ//vydL8E6nGUoKt58+p6EgiDOWcPrTv+7vXtf15azB4wBSDbQ4w5317bEvz/yaGY/7d3hv2PMOd546XFIAFT46HBYnHr8oE/L55eeQwW8NZkbejOUP+f6yX+PtyEZxJILNyEp6c/V+Pz3e2B/2F4frXBr3ATriUg+WBjzoYUJ6H37Skq/0H64QZNzFb9f22N6/+JOarfQ004r4cac368uMIZLIaM46x5P4SYcJ7tSFb6AlafrYpqUG+qwv/zC63AeFO73v9t3QZwPjIGia9q0IbzQfrgBvlos3Zkuwj874qXIhmD9IH0w1x0oMSX5//cfKH/66Lk/28KVQazQXhmvsT/lMyK/1PzFOBiyGpA+kD64QaBBN9tkfv/xMrx/yM1NzAbhNfPjvuvmvz6/9QJFXAxZDUgfQiDNFi7vbVY/4NwurrQ/wI1MTAbhL10eP7b2YX899AVgYuhqwHpBxtEDQAAgBkzCNLB3hkAAAAASUVORK5CYII=";
            var img = new Image();
            if (img.LoadPngFromBuffer(Convert.FromBase64String(b64)) == Error.Ok)
            {
                _crownTextureCached = ImageTexture.CreateFromImage(img);
                return _crownTextureCached;
            }
        }
        catch { }
        return null;
    }

    private static Texture2D? _coinTextureCached;
    public static Texture2D? GetCoinTexture()
    {
        if (_coinTextureCached != null) return _coinTextureCached;
        var tex = LoadTextureSafely("res://assets/icons/accessories/幸運金幣.png");
        if (tex != null)
        {
            _coinTextureCached = tex;
            return tex;
        }
        try
        {
            const string b64 = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAABV1JREFUeJztWi2U4kgYrLt3IrjEEdkyOHCDjGQcuBnJOeTZkbtu1+1KJOt2HMjI4IIjsmXiEkfcnKjq2WXNkhnm3r03/ZmmQ/eXkKrvtwG8ePHixYsXL168ePHyHuWPt75BEODpfB6462fSth0AoOve/pl+lj//y5v9H+Xqb9shbmJCPB3zujGcJ9MYABDHoda3AID8e8XxwHkLri8KMqOu34YZngHXUhSGRH6ScD4ecUxviHR6S+QPeQMAaBoiawzXJVN+6KSvqvjpcU1mbHacl+V1fYVnwGsVOOTvF0R6fkOERlPOjwchZyMAQDwkRYIBbbwVE07SFwVEfDqyXBfwe9tR32ZNH7HZXsc3eAa8dKNDfrmk7a5WdPcDELnjnkiWlrb/DLEkisgIlxc0z9ZPMfGQ+rpvnJtQX/B+a/mGL19qAC9nwrtnwF99N7g4n6ZEZLkkUl1N5Hdb2W5k3HoAQDyMz/Uozg/kC+IhGVHsMwDAoSLCxswBACfL6+kN9d7xMmwhn7DrnoD+0cEzoO8Gh2g6UU4P2uCh4PUOyvRC2bZy/FwLTEwEo2AAALCWzuGw3QEAEsP9ZsSxa8iEbrAAAOyzI7+PyLTxBNLPsbT9fo9nQN8NaUqbn91NAQBdzVdflPQJsWoADIjwABxHCW18vqDxBuDcWkJ2GywBANvdVwBAdSRjJtOJ1h31BJyHKPk8yjTznL7AVv18wbtnwMUe08X9bEMGBDFtNFeO7qK4kQ1X8uLoXFWYAgDaTplfx5qgbYicyweimJliIWYkivsD+YyqLHQ/MmA+o561MsSHr9Tftp4BF8nFPuA5nifKzQ9COCBC0zHHULbvMj9riVDVcn1tGTWGZnimt6r5fWzIgOlEPkb3D9061QYB1GiI9gAAY9z184zyd+IZcPFKvdhG9XhBE4QxLPxloshy2mgkZqwePgAADtl3qhnKRk+t9tOLR8oP3I3GCX2JlS+JXC9xQuT3BZlU6Tl+6in2Es+ASxe6FxspzkeWY3eisVvZtlsYJ7TlbLcBAJQloYqHtP1KtUNsXJeYo5HXb5UBZrtHAECapmdP4qrFSPcxDZ8jCPUc9WW/yzPg0oU/4mrwBPzw3s72y0LFQMAMr6qIcNMqzqv6i5U/zBdEdL1h5lfLmBNFgSxjbTC7nQEAIoWBrjvXb8QUa1t9f+kvongG9N3QuGggrzvo+OanUyJ6tESyKBmf44i2Gg9p28WhPNN3m94DAHKX4QnZf5a8Hun8wNn+sWRNUIlZISo9j3qMbef7AX2kNwN2Gd/8bEZbLgsiEAS0Xeetrfr6WZYDALoT3XLX0VsfVbiHEfWkYlDjbDukc2lVMzTKFFsxbpJo3uq84XjOrEvl3TOgdyfV9QS3W2ZksXp6h4JInDp34qOTHiGYZ+zpaTnmc3Z4XD/gsJf+kPtjZYhDQ69fux6hokgyob6yoMLZggxofU+wn7z4XGA8Zj6wWStnL28AAFWleNwQ+WTCWqF+7g+oanM9w5rrotB1kVX3t/ZsX6IawAWFm4SdqL8fuG79rZ/3d+IZ8FoFqxV9wkonRG1FpPLcZWj0/lP9UWCoE6FSPb5ImaOr+lwnydbcd68DgGFM5qQzjh8fGF0ePr4MeSeeAddS9OlT8gT86NHZ0vUAhbC6vI2QdcXEQDXCCefRYzJOpIdIz2cME5/XXP/hs/oKr/yfgGfAtRUu7xgd7pZEMIL69VY9QJ0HtL/kC872Y534JInyh5zrH/fy9uvX2fyv4hnwVordOcIiVadHXnw+V3U4YrwPFP+taoNDzlphvaGvKJ57fv5fYl68ePHixYsXL168XFH+BZkXG2I5lRJBAAAAAElFTkSuQmCC";
            var img = new Image();
            if (img.LoadPngFromBuffer(Convert.FromBase64String(b64)) == Error.Ok)
            {
                _coinTextureCached = ImageTexture.CreateFromImage(img);
                return _coinTextureCached;
            }
        }
        catch { }
        return null;
    }
}
