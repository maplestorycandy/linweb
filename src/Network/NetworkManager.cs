using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace IdleLineage.Network;

public sealed class NetworkManager
{
    private static readonly Lazy<NetworkManager> _instance = new(() => new NetworkManager());
    public static NetworkManager Instance => _instance.Value;

    public const int DefaultPort = 7777;

    public bool IsHost { get; private set; }
    public bool IsConnected { get; private set; }
    public int PeerCount { get { lock (_serverPeers) { return _serverPeers.Count; } } }
    public string LocalPlayerId { get; private set; } = Guid.NewGuid().ToString("N")[..8];

    // Thread-safe main thread dispatch queue
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

    // Events
    public event Action<string>? OnStatusChanged;
    public event Action<HandshakePacket>? OnRemotePlayerJoined;
    public event Action<MovePacket>? OnRemotePlayerMoved;
    public event Action<EquipPacket>? OnRemotePlayerEquipped;
    public event Action<PlayerHpSyncPacket>? OnPlayerHpSynced;
    public event Action<ActionPacket>? OnRemotePlayerAction;
    public event Action<ChatPacket>? OnChatReceived;
    public event Action<string>? OnRemotePlayerLeft;

    // Monster sync events
    public event Action<MobSpawnPacket>? OnMobSpawned;
    public event Action<MobBatchMovePacket>? OnMobBatchMoved;
    public event Action<MobHitPacket>? OnMobHitReceived;
    public event Action<MobHpSyncPacket>? OnMobHpSynced;
    public event Action<MobDeathPacket>? OnMobDied;

    // Item drop sync events
    public event Action<ItemDropPacket>? OnItemDropped;
    public event Action<ItemPickupPacket>? OnItemPickedUp;

    // Follower sync events (pets, summons, magic dolls)
    public event Action<FollowerSyncPacket>? OnFollowerSynced;

    // Host state
    private TcpListener? _listener;
    private readonly List<ConnectedPeer> _serverPeers = new();
    private CancellationTokenSource? _hostCts;

    // Client state
    private TcpClient? _client;
    private NetworkStream? _clientStream;
    private CancellationTokenSource? _clientCts;

    public readonly ConcurrentDictionary<string, HandshakePacket> ConnectedPlayers = new();

    private class ConnectedPeer
    {
        public TcpClient Client { get; set; } = null!;
        public NetworkStream Stream { get; set; } = null!;
        public string Id { get; set; } = "";
    }

    private NetworkManager() { }

    public static List<string> GetLocalIpAddresses()
    {
        var list = new List<string>();
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    list.Add(ip.ToString());
                }
            }
        }
        catch { }
        if (list.Count == 0) list.Add("127.0.0.1");
        return list;
    }

    public static string GetLocalIpAddress()
    {
        var ips = GetLocalIpAddresses();
        return ips.Count > 0 ? ips[0] : "127.0.0.1";
    }

    public void StartHost(int port = DefaultPort)
    {
        _ = StartHostAsync(port);
    }

    public void ConnectToHost(string hostIp, int port = DefaultPort)
    {
        _ = ConnectToHostAsync(hostIp, port);
    }

    public async Task<bool> StartHostAsync(int port = DefaultPort)
    {
        Disconnect();
        try
        {
            _hostCts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            IsHost = true;
            IsConnected = true;

            _ = ServerListenLoop(_hostCts.Token);
            OnStatusChanged?.Invoke($"[房長開房] 伺服器已建立於 Port {port}，等待隊友加入...");
            return true;
        }
        catch (Exception ex)
        {
            Disconnect();
            OnStatusChanged?.Invoke($"[房長開房失敗] {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ConnectToHostAsync(string hostIp, int port = DefaultPort)
    {
        Disconnect();
        try
        {
            _clientCts = new CancellationTokenSource();
            _client = new TcpClient();
            await _client.ConnectAsync(hostIp, port, _clientCts.Token);

            _clientStream = _client.GetStream();
            IsHost = false;
            IsConnected = true;

            _ = ClientReceiveLoop(_clientCts.Token);
            OnStatusChanged?.Invoke($"[連線成功] 已加入主機 {hostIp}:{port}！");
            return true;
        }
        catch (Exception ex)
        {
            Disconnect();
            OnStatusChanged?.Invoke($"[連線失敗] 無法連線至 {hostIp}:{port} ({ex.Message})");
            return false;
        }
    }

    public void Disconnect()
    {
        _hostCts?.Cancel();
        _hostCts = null;
        _clientCts?.Cancel();
        _clientCts = null;

        if (_listener != null)
        {
            try { _listener.Stop(); } catch { }
            _listener = null;
            lock (_serverPeers)
            {
                foreach (var p in _serverPeers)
                {
                    try { p.Client.Close(); } catch { }
                }
                _serverPeers.Clear();
            }
        }

        if (_client != null)
        {
            try { _client.Close(); } catch { }
            _client = null;
            _clientStream = null;
        }

        IsConnected = false;
        IsHost = false;
        ConnectedPlayers.Clear();
    }

    public void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try { action(); } catch (Exception ex) { GD.PushWarning($"[Network] MainThread warning: {ex.Message}"); }
        }
    }

    public void SendHandshake(HandshakePacket handshake)
    {
        handshake.PlayerId = LocalPlayerId;
        Broadcast(NetEnvelope.Create(NetPacketType.Handshake, handshake));
    }

    public void SendMove(MovePacket move)
    {
        move.PlayerId = LocalPlayerId;
        Broadcast(NetEnvelope.Create(NetPacketType.Move, move));
    }

    public void SendEquip(EquipPacket equip)
    {
        equip.PlayerId = LocalPlayerId;
        Broadcast(NetEnvelope.Create(NetPacketType.Equip, equip));
    }

    public void SendPlayerHpSync(PlayerHpSyncPacket hpSync)
    {
        if (string.IsNullOrEmpty(hpSync.PlayerId))
        {
            hpSync.PlayerId = LocalPlayerId;
        }
        Broadcast(NetEnvelope.Create(NetPacketType.PlayerHpSync, hpSync));
    }

    public void SendAction(ActionPacket action)
    {
        action.PlayerId = LocalPlayerId;
        Broadcast(NetEnvelope.Create(NetPacketType.Action, action));
    }

    public void SendChat(string message, string senderName, string colorHex = "#ffffff")
    {
        var chat = new ChatPacket
        {
            SenderId = LocalPlayerId,
            SenderName = senderName,
            Message = message,
            ColorHex = colorHex
        };
        Broadcast(NetEnvelope.Create(NetPacketType.Chat, chat));
    }

    public void SendMobSpawn(MobSpawnPacket spawn)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.MobSpawn, spawn));
    }

    public void SendMobBatchMove(MobBatchMovePacket batch)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.MobBatchMove, batch));
    }

    public void SendMobHit(MobHitPacket hit)
    {
        hit.AttackerId = LocalPlayerId;
        Broadcast(NetEnvelope.Create(NetPacketType.MobHit, hit));
    }

    public void SendMobHpSync(MobHpSyncPacket hpSync)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.MobHpSync, hpSync));
    }

    public void SendMobDeath(MobDeathPacket death)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.MobDeath, death));
    }

    public void SendItemDrop(ItemDropPacket drop)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.ItemDrop, drop));
    }

    public void SendItemPickup(ItemPickupPacket pickup)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.ItemPickup, pickup));
    }

    public void SendFollowerSync(FollowerSyncPacket sync)
    {
        Broadcast(NetEnvelope.Create(NetPacketType.FollowerSync, sync));
    }

    private void Broadcast(NetEnvelope envelope)
    {
        if (!IsConnected) return;

        string json = JsonSerializer.Serialize(envelope) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        if (IsHost)
        {
            lock (_serverPeers)
            {
                foreach (var peer in _serverPeers)
                {
                    try { peer.Stream.Write(bytes, 0, bytes.Length); } catch { }
                }
            }
        }
        else
        {
            if (_clientStream != null)
            {
                try { _clientStream.Write(bytes, 0, bytes.Length); } catch { }
            }
        }
    }

    private void BroadcastExcept(NetEnvelope envelope, ConnectedPeer excludePeer)
    {
        if (!IsConnected || !IsHost) return;

        string json = JsonSerializer.Serialize(envelope) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        lock (_serverPeers)
        {
            foreach (var peer in _serverPeers)
            {
                if (peer == excludePeer) continue;
                try { peer.Stream.Write(bytes, 0, bytes.Length); } catch { }
            }
        }
    }

    private async Task ServerListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(token);
                var peer = new ConnectedPeer
                {
                    Client = tcpClient,
                    Stream = tcpClient.GetStream()
                };
                lock (_serverPeers) { _serverPeers.Add(peer); }
                _mainThreadQueue.Enqueue(() =>
                {
                    OnStatusChanged?.Invoke($"[隊友加入] 隊友已連線進房！(連線人數: {_serverPeers.Count + 1})");
                });
                _ = ServerPeerLoop(peer, token);
            }
            catch { break; }
        }
    }

    private async Task ServerPeerLoop(ConnectedPeer peer, CancellationToken token)
    {
        using var reader = new StreamReader(peer.Stream, Encoding.UTF8);
        try
        {
            while (!token.IsCancellationRequested && peer.Client.Connected)
            {
                string? line = await reader.ReadLineAsync(token);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var envelope = JsonSerializer.Deserialize<NetEnvelope>(line);
                if (envelope == null) continue;

                // Process locally on host
                _mainThreadQueue.Enqueue(() => HandleEnvelope(envelope, peer));

                // Forward to all other clients
                byte[] forwardBytes = Encoding.UTF8.GetBytes(line + "\n");
                lock (_serverPeers)
                {
                    foreach (var other in _serverPeers)
                    {
                        if (other != peer)
                        {
                            try { other.Stream.Write(forwardBytes, 0, forwardBytes.Length); } catch { }
                        }
                    }
                }
            }
        }
        catch { }
        finally
        {
            lock (_serverPeers) { _serverPeers.Remove(peer); }
            if (!string.IsNullOrEmpty(peer.Id))
            {
                _mainThreadQueue.Enqueue(() =>
                {
                    ConnectedPlayers.Remove(peer.Id, out _);
                    OnRemotePlayerLeft?.Invoke(peer.Id);
                });
                var leaveEnv = NetEnvelope.Create(NetPacketType.Leave, new LeavePacket { PlayerId = peer.Id });
                Broadcast(leaveEnv);
            }
        }
    }

    private async Task ClientReceiveLoop(CancellationToken token)
    {
        if (_clientStream == null) return;
        using var reader = new StreamReader(_clientStream, Encoding.UTF8);
        try
        {
            while (!token.IsCancellationRequested && _client != null && _client.Connected)
            {
                string? line = await reader.ReadLineAsync(token);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var envelope = JsonSerializer.Deserialize<NetEnvelope>(line);
                if (envelope == null) continue;

                _mainThreadQueue.Enqueue(() => HandleEnvelope(envelope, null));
            }
        }
        catch { }
        finally
        {
            _mainThreadQueue.Enqueue(() =>
            {
                IsConnected = false;
                OnStatusChanged?.Invoke("[連線] 與伺服器連線已中斷");
            });
        }
    }

    private void HandleEnvelope(NetEnvelope envelope, ConnectedPeer? senderPeer)
    {
        switch (envelope.Type)
        {
            case NetPacketType.Handshake:
                var handshake = envelope.Deserialize<HandshakePacket>();
                if (handshake != null && handshake.PlayerId != LocalPlayerId)
                {
                    if (senderPeer != null) senderPeer.Id = handshake.PlayerId;
                    ConnectedPlayers[handshake.PlayerId] = handshake;
                    if (IsHost && senderPeer != null)
                    {
                        BroadcastExcept(envelope, senderPeer);
                    }
                    OnRemotePlayerJoined?.Invoke(handshake);
                }
                break;

            case NetPacketType.Move:
                var move = envelope.Deserialize<MovePacket>();
                if (move != null && move.PlayerId != LocalPlayerId)
                {
                    OnRemotePlayerMoved?.Invoke(move);
                }
                break;

            case NetPacketType.Equip:
                var equip = envelope.Deserialize<EquipPacket>();
                if (equip != null && equip.PlayerId != LocalPlayerId)
                {
                    OnRemotePlayerEquipped?.Invoke(equip);
                }
                break;

            case NetPacketType.PlayerHpSync:
                var playerHp = envelope.Deserialize<PlayerHpSyncPacket>();
                if (playerHp != null)
                {
                    OnPlayerHpSynced?.Invoke(playerHp);
                }
                break;

            case NetPacketType.Action:
                var action = envelope.Deserialize<ActionPacket>();
                if (action != null)
                {
                    OnRemotePlayerAction?.Invoke(action);
                }
                break;

            case NetPacketType.Chat:
                var chat = envelope.Deserialize<ChatPacket>();
                if (chat != null)
                {
                    if (IsHost && senderPeer != null)
                    {
                        BroadcastExcept(envelope, senderPeer);
                    }
                    OnChatReceived?.Invoke(chat);
                }
                break;

            case NetPacketType.Leave:
                var leave = envelope.Deserialize<LeavePacket>();
                if (leave != null)
                {
                    ConnectedPlayers.Remove(leave.PlayerId, out _);
                    OnRemotePlayerLeft?.Invoke(leave.PlayerId);
                }
                break;

            case NetPacketType.MobSpawn:
                var mobSpawn = envelope.Deserialize<MobSpawnPacket>();
                if (mobSpawn != null)
                {
                    OnMobSpawned?.Invoke(mobSpawn);
                }
                break;

            case NetPacketType.MobBatchMove:
                var mobBatch = envelope.Deserialize<MobBatchMovePacket>();
                if (mobBatch != null)
                {
                    OnMobBatchMoved?.Invoke(mobBatch);
                }
                break;

            case NetPacketType.MobHit:
                var mobHit = envelope.Deserialize<MobHitPacket>();
                if (mobHit != null)
                {
                    OnMobHitReceived?.Invoke(mobHit);
                }
                break;

            case NetPacketType.MobHpSync:
                var mobHp = envelope.Deserialize<MobHpSyncPacket>();
                if (mobHp != null)
                {
                    OnMobHpSynced?.Invoke(mobHp);
                }
                break;

            case NetPacketType.MobDeath:
                var mobDeath = envelope.Deserialize<MobDeathPacket>();
                if (mobDeath != null)
                {
                    OnMobDied?.Invoke(mobDeath);
                }
                break;

            case NetPacketType.ItemDrop:
                var itemDrop = envelope.Deserialize<ItemDropPacket>();
                if (itemDrop != null)
                {
                    if (IsHost && senderPeer != null)
                    {
                        BroadcastExcept(envelope, senderPeer);
                    }
                    OnItemDropped?.Invoke(itemDrop);
                }
                break;

            case NetPacketType.ItemPickup:
                var itemPickup = envelope.Deserialize<ItemPickupPacket>();
                if (itemPickup != null)
                {
                    if (IsHost && senderPeer != null)
                    {
                        BroadcastExcept(envelope, senderPeer);
                    }
                    OnItemPickedUp?.Invoke(itemPickup);
                }
                break;

            case NetPacketType.FollowerSync:
                var followerSync = envelope.Deserialize<FollowerSyncPacket>();
                if (followerSync != null)
                {
                    if (IsHost && senderPeer != null)
                    {
                        BroadcastExcept(envelope, senderPeer);
                    }
                    OnFollowerSynced?.Invoke(followerSync);
                }
                break;
        }
    }
}
