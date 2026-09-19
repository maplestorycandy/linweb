using System;
using System.Collections.Generic;
using Godot;
using IdleLineage.Network;

namespace IdleLineage.Ui;

public sealed partial class MultiplayerLobbyWindow : Panel
{
    private LineEdit _ipInput = null!;
    private LineEdit _portInput = null!;
    private LineEdit _hostPortInput = null!;
    private Label _statusLabel = null!;
    private Label _localIpLabel = null!;
    private ItemList _playerList = null!;
    private Button _hostBtn = null!;
    private Button _joinBtn = null!;
    private Button _startBtn = null!;
    private Action? _onStartGame;

    public static MultiplayerLobbyWindow Create(Action? onStartGame = null)
    {
        var win = new MultiplayerLobbyWindow();
        win._onStartGame = onStartGame;
        win.CustomMinimumSize = new Vector2(460, 360);
        win.Size = new Vector2(460, 360);
        return win;
    }

    public override void _Ready()
    {
        base._Ready();
        BuildUi();
        RegisterNetworkEvents();
        UpdateLocalIps();
        SetProcess(true);
    }

    private int _lastPeerCount = -1;
    public override void _Process(double delta)
    {
        base._Process(delta);
        NetworkManager.Instance.Update();
        int curPeers = NetworkManager.Instance.PeerCount;
        if (curPeers != _lastPeerCount)
        {
            _lastPeerCount = curPeers;
            RefreshPlayerList(null);
        }
    }

    public override void _ExitTree()
    {
        UnregisterNetworkEvents();
        base._ExitTree();
    }

    private void BuildUi()
    {
        // Dark metallic fantasy panel background
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.12f, 0.95f),
            BorderColor = new Color(0.78f, 0.65f, 0.38f, 1f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6
        };
        AddThemeStyleboxOverride("panel", sb);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        // Header
        var header = new HBoxContainer();
        var title = new Label
        {
            Text = "⚔️ 多人連線大廳 (Multiplayer Lobby)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.5f));
        title.AddThemeFontSizeOverride("font_size", 16);
        header.AddChild(title);

        var closeBtn = new Button { Text = " ✕ " };
        closeBtn.Pressed += () => QueueFree();
        header.AddChild(closeBtn);
        vbox.AddChild(header);

        // TabContainer
        var tabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        vbox.AddChild(tabs);

        // Tab 1: Host Room
        var hostTab = new VBoxContainer { Name = "  開房（建立主機）  " };
        hostTab.AddThemeConstantOverride("separation", 8);
        tabs.AddChild(hostTab);

        _localIpLabel = new Label
        {
            Text = "本機 IP: 獲取中...",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _localIpLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1.0f));
        hostTab.AddChild(_localIpLabel);

        var hostPortBox = new HBoxContainer();
        hostPortBox.AddChild(new Label { Text = "通訊埠 (Port): " });
        _hostPortInput = new LineEdit { Text = "7777", CustomMinimumSize = new Vector2(100, 26) };
        hostPortBox.AddChild(_hostPortInput);
        hostTab.AddChild(hostPortBox);

        _hostBtn = new Button { Text = "🚀 啟動伺服器開房" };
        _hostBtn.Pressed += OnHostPressed;
        hostTab.AddChild(_hostBtn);

        hostTab.AddChild(new Label { Text = "已連線玩家清單：" });
        _playerList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 70),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        hostTab.AddChild(_playerList);

        _startBtn = new Button { Text = "進入遊戲", Visible = false };
        _startBtn.Pressed += () =>
        {
            _onStartGame?.Invoke();
            QueueFree();
        };
        hostTab.AddChild(_startBtn);

        // Tab 2: Join Room
        var joinTab = new VBoxContainer { Name = "  加入（連線主機）  " };
        joinTab.AddThemeConstantOverride("separation", 8);
        tabs.AddChild(joinTab);

        var ipBox = new HBoxContainer();
        ipBox.AddChild(new Label { Text = "主機 IP 位址: " });
        _ipInput = new LineEdit { Text = "127.0.0.1", CustomMinimumSize = new Vector2(180, 26), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ipBox.AddChild(_ipInput);
        joinTab.AddChild(ipBox);

        var joinPortBox = new HBoxContainer();
        joinPortBox.AddChild(new Label { Text = "通訊埠 (Port): " });
        _portInput = new LineEdit { Text = "7777", CustomMinimumSize = new Vector2(100, 26) };
        joinPortBox.AddChild(_portInput);
        joinTab.AddChild(joinPortBox);

        _joinBtn = new Button { Text = "🔗 連線加入房間" };
        _joinBtn.Pressed += OnJoinPressed;
        joinTab.AddChild(_joinBtn);

        var joinHint = new Label
        {
            Text = "同台電腦測試請填 127.0.0.1，不同電腦請填主機的區網/實體 IP",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        joinHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        joinTab.AddChild(joinHint);

        // Status Label
        _statusLabel = new Label
        {
            Text = "請選擇開房或輸入 IP 加入...",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        vbox.AddChild(_statusLabel);
    }

    private void UpdateLocalIps()
    {
        var ips = NetworkManager.GetLocalIpAddresses();
        _localIpLabel.Text = "本機區網 IP: " + string.Join(" / ", ips) + " (提供給朋友連線)";
    }

    private void RegisterNetworkEvents()
    {
        NetworkManager.Instance.OnStatusChanged += UpdateStatus;
        NetworkManager.Instance.OnRemotePlayerJoined += RefreshPlayerList;
        NetworkManager.Instance.OnRemotePlayerLeft += _ => RefreshPlayerList(null);
    }

    private void UnregisterNetworkEvents()
    {
        NetworkManager.Instance.OnStatusChanged -= UpdateStatus;
        NetworkManager.Instance.OnRemotePlayerJoined -= RefreshPlayerList;
        NetworkManager.Instance.OnRemotePlayerLeft -= _ => RefreshPlayerList(null);
    }

    private void OnHostPressed()
    {
        if (int.TryParse(_hostPortInput.Text, out int port))
        {
            NetworkManager.Instance.StartHost(port);
            _hostBtn.Text = "✅ 伺服器運行中 (點擊可重啟)";
            _startBtn.Visible = true;
            RefreshPlayerList(null);
        }
    }

    private void OnJoinPressed()
    {
        string ip = _ipInput.Text.Trim();
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
        if (int.TryParse(_portInput.Text, out int port))
        {
            NetworkManager.Instance.ConnectToHost(ip, port);
        }
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
        if (NetworkManager.Instance.IsConnected)
        {
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1.0f, 0.5f));
            _startBtn.Visible = true;
            RefreshPlayerList(null);
        }
        else
        {
            _statusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.5f));
        }
    }

    private void RefreshPlayerList(HandshakePacket? _)
    {
        _playerList.Clear();
        if (NetworkManager.Instance.IsHost)
        {
            _playerList.AddItem($"👑 [主機 (我)]");
            int peerCount = NetworkManager.Instance.PeerCount;
            for (int i = 0; i < peerCount; i++)
            {
                _playerList.AddItem($"⚔️ [隊友 {i + 1}] (已連線進入房間)");
            }
        }
        else
        {
            _playerList.AddItem($"⚔️ [已連線本機玩家]");
            if (NetworkManager.Instance.IsConnected)
            {
                _playerList.AddItem("👑 [房長主機] (已連線)");
            }
        }
    }
}
