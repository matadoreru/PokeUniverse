using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Collections;
using Steamworks.Data;

public class AppManager : NetworkBehaviour
{
    public static AppManager Instance;
    public Lobby? currentLobby;

    [Header("Global Network Data")]
    public NetworkList<PlayerData> NetworkPlayers;
    public Dictionary<ulong, Texture2D> steamAvatars = new Dictionary<ulong, Texture2D>();

    [Header("Minigames Config")]
    public ImpostorPokeConfig impostorPokeConfig;
    public HigherOrLowerConfig higherOrLowerConfig;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            NetworkPlayers = new NetworkList<PlayerData>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void RegisterPlayerServerRpc(ulong clientId, ulong steamId, string name)
    {
        PlayerData newPlayer = new PlayerData
        {
            ClientId = clientId,
            SteamId = steamId,
            PlayerName = new FixedString64Bytes(name),
            Role = GameRole.None 
        };

        NetworkPlayers.Add(newPlayer);
    }

    private void OnClientDisconnect(ulong clientId)
    {
        for (int i = 0; i < NetworkPlayers.Count; i++)
        {
            if (NetworkPlayers[i].ClientId == clientId)
            {
                NetworkPlayers.RemoveAt(i);
                break;
            }
        }
    }

    public PlayerData? GetPlayerData(ulong clientId)
    {
        foreach (var p in NetworkPlayers)
        {
            if (p.ClientId == clientId) return p;
        }
        return null;
    }

    public void AddAvatarToCache(ulong steamId, Texture2D texture)
    {
        if (!steamAvatars.ContainsKey(steamId)) steamAvatars.Add(steamId, texture);
    }

    public void LoadScene(string sceneName)
    {
        if (!IsServer) return;

        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}

[System.Serializable]
public struct ImpostorPokeConfig
{
    public int roundsToVote;
    public float turnDuration;
    public int impostorCount;
    public bool hintGen;
    public bool hintType;
    public bool hintColor;
    public List<int> activeGenerations; 
}

[System.Serializable]
public struct HigherOrLowerConfig
{
    public float answerTime;
    public List<int> allowedGens;
    public Difficulty difficulty;
    public bool guessHP;
    public bool guessAT;
    public bool guessDF;
    public bool guessATESP;
    public bool guessDFESP;
    public bool guessVEL;
    public bool guessHEI; 
    public bool guessWEI;
}

public enum Difficulty
{
    VeryEasy = 50,
    Easy = 40,
    MediumEasy = 35,
    Medium = 25,
    MediumHard = 15,
    Hard = 10
}

public enum StatType
{
    HP, AT, DF, SP_AT, SP_DF, SPD, HEIGHT, WEIGHT
}

public enum ComparisonOperator
{
    Higher,
    Lower,
    Equal // (Equal is tough with precise stats, maybe keep it simple with Higher/Lower first)
}