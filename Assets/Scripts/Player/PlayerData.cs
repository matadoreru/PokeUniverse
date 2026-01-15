using Unity.Netcode;
using Unity.Collections;
using System;
using UnityEngine.UI;

public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public ulong SteamId;
    public FixedString64Bytes PlayerName; 
    public GameRole Role;
    public bool IsImpostor => Role == GameRole.TeamRocket;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref SteamId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref Role);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId;
    }
}