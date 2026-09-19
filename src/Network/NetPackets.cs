using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleLineage.Network;

public enum NetPacketType
{
    Handshake = 1,
    Move = 2,
    Action = 3,
    Chat = 4,
    Leave = 5,
    SyncRequest = 6,
    Ping = 7,
    Equip = 8,
    MobSpawn = 9,
    MobBatchMove = 10,
    MobHit = 11,
    MobHpSync = 12,
    MobDeath = 13,
    PlayerHpSync = 14,
    ItemDrop = 15,
    ItemPickup = 16,
    FollowerSync = 17
}

public class NetEnvelope
{
    [JsonPropertyName("type")]
    public NetPacketType Type { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = "";

    public static NetEnvelope Create<T>(NetPacketType type, T payload)
    {
        return new NetEnvelope
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload)
        };
    }

    public T? Deserialize<T>()
    {
        if (string.IsNullOrEmpty(Payload)) return default;
        return JsonSerializer.Deserialize<T>(Payload);
    }
}

public class HandshakePacket
{
    [JsonPropertyName("id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("class")]
    public string ClassId { get; set; } = "knight";

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = "";

    [JsonPropertyName("weapon")]
    public string WeaponPrefix { get; set; } = "";

    [JsonPropertyName("mainWeapon")]
    public string MainWeaponId { get; set; } = "";

    [JsonPropertyName("lvl")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("hp")]
    public double Hp { get; set; } = 100;

    [JsonPropertyName("maxHp")]
    public double MaxHp { get; set; } = 100;

    [JsonPropertyName("mp")]
    public double Mp { get; set; } = 100;

    [JsonPropertyName("maxMp")]
    public double MaxMp { get; set; } = 100;

    [JsonPropertyName("isHost")]
    public bool IsHost { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("facing")]
    public int Facing8 { get; set; } = 4;

    [JsonPropertyName("map")]
    public string MapKey { get; set; } = "";

    [JsonPropertyName("isResp")]
    public bool IsResponse { get; set; }

    [JsonPropertyName("poly")]
    public string PolymorphForm { get; set; } = "";

    [JsonPropertyName("poison")]
    public bool IsPoisoned { get; set; }
}

public class MovePacket
{
    [JsonPropertyName("id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("facing")]
    public int Facing8 { get; set; }

    [JsonPropertyName("stepping")]
    public bool Stepping { get; set; }

    [JsonPropertyName("hp")]
    public double Hp { get; set; } = 100;

    [JsonPropertyName("maxHp")]
    public double MaxHp { get; set; } = 100;

    [JsonPropertyName("mp")]
    public double Mp { get; set; } = 100;

    [JsonPropertyName("maxMp")]
    public double MaxMp { get; set; } = 100;

    [JsonPropertyName("map")]
    public string MapKey { get; set; } = "";

    [JsonPropertyName("poly")]
    public string PolymorphForm { get; set; } = "";

    [JsonPropertyName("poison")]
    public bool IsPoisoned { get; set; }
}

public class EquipPacket
{
    [JsonPropertyName("id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("mainWeapon")]
    public string MainWeaponId { get; set; } = "";

    [JsonPropertyName("weaponPrefix")]
    public string WeaponPrefix { get; set; } = "";
}

public class PlayerHpSyncPacket
{
    [JsonPropertyName("id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("hp")]
    public double Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public double MaxHp { get; set; }

    [JsonPropertyName("mp")]
    public double Mp { get; set; }

    [JsonPropertyName("maxMp")]
    public double MaxMp { get; set; }

    [JsonPropertyName("dmg")]
    public double DamageTaken { get; set; }

    [JsonPropertyName("poison")]
    public bool IsPoisoned { get; set; }
}

public class ActionPacket
{
    [JsonPropertyName("id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("action")]
    public string ActionType { get; set; } = "attack";

    [JsonPropertyName("skill")]
    public string SkillId { get; set; } = "";

    [JsonPropertyName("morph")]
    public string MorphName { get; set; } = "";

    [JsonPropertyName("tx")]
    public double TargetX { get; set; }

    [JsonPropertyName("ty")]
    public double TargetY { get; set; }
}

public class ChatPacket
{
    [JsonPropertyName("id")]
    public string SenderId { get; set; } = "";

    [JsonPropertyName("sender")]
    public string SenderName { get; set; } = "";

    [JsonPropertyName("msg")]
    public string Message { get; set; } = "";

    [JsonPropertyName("color")]
    public string ColorHex { get; set; } = "#66d9ef";
}

public class LeavePacket
{
    [JsonPropertyName("id")]
    public string PlayerId { get; set; } = "";
}

public class MobSpawnPacket
{
    [JsonPropertyName("mobId")]
    public string MobId { get; set; } = "";

    [JsonPropertyName("mobKey")]
    public string MobKey { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("hp")]
    public double Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public double MaxHp { get; set; }

    [JsonPropertyName("facing")]
    public int Facing8 { get; set; }

    [JsonPropertyName("map")]
    public string MapKey { get; set; } = "";
}

public class MobMoveEntry
{
    [JsonPropertyName("mobId")]
    public string MobId { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("facing")]
    public int Facing8 { get; set; }

    [JsonPropertyName("stepping")]
    public bool Stepping { get; set; }
}

public class MobBatchMovePacket
{
    [JsonPropertyName("moves")]
    public List<MobMoveEntry> Moves { get; set; } = new();
}

public class MobHitPacket
{
    [JsonPropertyName("mobId")]
    public string MobId { get; set; } = "";

    [JsonPropertyName("attackerId")]
    public string AttackerId { get; set; } = "";

    [JsonPropertyName("damage")]
    public double Damage { get; set; }
}

public class MobHpSyncPacket
{
    [JsonPropertyName("mobId")]
    public string MobId { get; set; } = "";

    [JsonPropertyName("hp")]
    public double CurrentHp { get; set; }

    [JsonPropertyName("damage")]
    public double DamageTaken { get; set; }

    [JsonPropertyName("attackerId")]
    public string AttackerId { get; set; } = "";
}

public class MobDeathPacket
{
    [JsonPropertyName("mobId")]
    public string MobId { get; set; } = "";

    [JsonPropertyName("killerId")]
    public string KillerId { get; set; } = "";

    [JsonPropertyName("exp")]
    public double Exp { get; set; }

    [JsonPropertyName("gold")]
    public long Gold { get; set; }
}

public class ItemDropPacket
{
    [JsonPropertyName("dropId")]
    public string DropId { get; set; } = "";

    [JsonPropertyName("itemKey")]
    public string ItemKey { get; set; } = "";

    [JsonPropertyName("qty")]
    public long Quantity { get; set; } = 1;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("map")]
    public string MapKey { get; set; } = "";

    [JsonPropertyName("blessing")]
    public int Blessing { get; set; }

    [JsonPropertyName("enh")]
    public int Enhancement { get; set; }

    [JsonPropertyName("ident")]
    public bool IsIdentified { get; set; }

    [JsonPropertyName("lvl")]
    public int ItemLevel { get; set; }

    [JsonPropertyName("affixes")]
    public string AffixesJson { get; set; } = "";

    [JsonPropertyName("dropperId")]
    public string DropperId { get; set; } = "";

    [JsonPropertyName("dropperName")]
    public string DropperName { get; set; } = "";
}

public class ItemPickupPacket
{
    [JsonPropertyName("dropId")]
    public string DropId { get; set; } = "";

    [JsonPropertyName("pickerId")]
    public string PickerId { get; set; } = "";

    [JsonPropertyName("pickerName")]
    public string PickerName { get; set; } = "";

    [JsonPropertyName("itemKey")]
    public string ItemKey { get; set; } = "";

    [JsonPropertyName("qty")]
    public long Quantity { get; set; } = 1;

    [JsonPropertyName("remaining")]
    public long Remaining { get; set; }
}

public class FollowerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "pet"; // "pet" or "summon"

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("f8")]
    public int Facing8 { get; set; }

    [JsonPropertyName("hp")]
    public double Hp { get; set; }

    [JsonPropertyName("max_hp")]
    public double MaxHp { get; set; }

    [JsonPropertyName("stepping")]
    public bool Stepping { get; set; }
}

public class FollowerSyncPacket
{
    [JsonPropertyName("owner")]
    public string OwnerId { get; set; } = "";

    [JsonPropertyName("map")]
    public string MapKey { get; set; } = "";

    [JsonPropertyName("followers")]
    public List<FollowerInfo> Followers { get; set; } = new List<FollowerInfo>();
}

