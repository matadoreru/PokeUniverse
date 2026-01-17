using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using static PokemonDatabase;

public class ImpostorPokeManager : NetworkBehaviour
{
    public static ImpostorPokeManager Instance;

    [Header("Data References")]
    [SerializeField] private PokemonDatabase pokemonDatabase;

    [Header("Runtime State")]
    public NetworkVariable<GameStateImpostor> currentState = new NetworkVariable<GameStateImpostor>(GameStateImpostor.Playing);
    public NetworkVariable<ulong> activePlayerId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<int> currentRound = new NetworkVariable<int>(1);
    public NetworkVariable<float> turnTimer = new NetworkVariable<float>(0f);

    public NetworkVariable<ulong> currentImpostorId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<int> currentPokemonIndex = new NetworkVariable<int>(-1);
    public NetworkVariable<bool> currentIsShiny = new NetworkVariable<bool>(false);

    [Header("Synced Config")]
    public NetworkVariable<float> turnDurationNetVar = new NetworkVariable<float>(30f);
    public NetworkVariable<bool> hintGenNetVar = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> hintTypeNetVar = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> hintColorNetVar = new NetworkVariable<bool>(true);

    public ImpostorPokeConfig gameConfig; // Local host config

    private Dictionary<ulong, ulong> playerVotes = new Dictionary<ulong, ulong>();
    private List<ulong> turnOrder = new List<ulong>();
    private int currentTurnIndex = 0;

    // Events
    public System.Action<string> OnWordSubmitted;
    public System.Action OnTurnChanged;
    public System.Action<GameStateImpostor> OnStateChanged;
    public System.Action<string, PokemonEntry, bool> OnGameEnded;
    public System.Action<ulong, ulong> OnVoteCast;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        activePlayerId.OnValueChanged += (prev, curr) => OnTurnChanged?.Invoke();
        currentState.OnValueChanged += (prev, curr) => OnStateChanged?.Invoke(curr);

        currentPokemonIndex.OnValueChanged += (prev, curr) => ClientRefreshSecretUI();
        currentImpostorId.OnValueChanged += (p, c) => ClientRefreshSecretUI();

        LoadConfigAndStart();

        if (currentPokemonIndex.Value != -1)
            ClientRefreshSecretUI();
    }

    public void LoadConfigAndStart()
    {
        if (AppManager.Instance == null) return;
        gameConfig = AppManager.Instance.impostorPokeConfig;

        if (gameConfig.activeGenerations == null || gameConfig.activeGenerations.Count == 0)
            gameConfig.activeGenerations = new List<int> { 1 };
        if (gameConfig.turnDuration <= 0) gameConfig.turnDuration = 30f;

        if (IsHost) StartMatch(gameConfig.activeGenerations);
    }

    private void StartMatch(List<int> activeGens)
    {
        currentState.Value = GameStateImpostor.Playing;
        playerVotes.Clear();
        SetupTurnOrder();
        ShowTurnOrderToPlayersClientRpc(turnOrder.ToArray());

        ulong impostorId = AssignRolesAndGetImpostor();
        int index;
        pokemonDatabase.GetRandomPokemonFiltered(activeGens, out index);

        // 10% de que salga shiny
        bool isShiny = Random.Range(0, 100) <= 10;

        // Sync Vars
        currentPokemonIndex.Value = index;
        currentIsShiny.Value = isShiny;
        currentImpostorId.Value = impostorId;

        turnDurationNetVar.Value = gameConfig.turnDuration;
        hintGenNetVar.Value = gameConfig.hintGen;
        hintTypeNetVar.Value = gameConfig.hintType;
        hintColorNetVar.Value = gameConfig.hintColor;

        currentTurnIndex = 0;
        currentRound.Value = 1;
        activePlayerId.Value = turnOrder[currentTurnIndex];
        turnTimer.Value = gameConfig.turnDuration;

        ShowSecretToPlayersClientRpc();
    }

    [ClientRpc]
    private void ShowSecretToPlayersClientRpc()
    {
        ClientRefreshSecretUI();
    }

    public void ClientRefreshSecretUI()
    {
        if (currentPokemonIndex.Value == -1) return;

        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        bool amIImpostor = (myClientId == currentImpostorId.Value);
        PokemonEntry pokemonInfo = pokemonDatabase.allPokemon[currentPokemonIndex.Value];

        ImpostorPokeUI.Instance.SetupSecretScreen(
            pokemonInfo,
            amIImpostor,
            currentIsShiny.Value,
            hintGenNetVar.Value,
            hintTypeNetVar.Value,
            hintColorNetVar.Value
        );
    }

    private void Update()
    {
        if (IsHost && currentState.Value == GameStateImpostor.Playing && activePlayerId.Value != ulong.MaxValue)
        {
            turnTimer.Value -= Time.deltaTime;
            if (turnTimer.Value <= 0) ForceSkipTurn();
        }
    }

    private void ForceSkipTurn()
    {
        string skippedPlayerName = "Unknown";
        var p = AppManager.Instance.GetPlayerData(activePlayerId.Value);
        if (p.HasValue) skippedPlayerName = p.Value.PlayerName.ToString();

        SubmitWordClientRpc(skippedPlayerName, "<color=yellow>... (Tiempo agotado)</color>");
        AdvanceTurn();
    }

    private void SetupTurnOrder()
    {
        turnOrder.Clear();
        foreach (var player in AppManager.Instance.NetworkPlayers)
            turnOrder.Add(player.ClientId);

        var count = turnOrder.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i)
        {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = turnOrder[i];
            turnOrder[i] = turnOrder[r];
            turnOrder[r] = tmp;
        }
    }

    private ulong AssignRolesAndGetImpostor()
    {
        var players = AppManager.Instance.NetworkPlayers;
        List<int> indices = Enumerable.Range(0, players.Count).ToList();

        // Shuffle indices
        var c = indices.Count;
        for (var i = 0; i < c - 1; ++i)
        {
            var r = UnityEngine.Random.Range(i, c);
            var t = indices[i];
            indices[i] = indices[r];
            indices[r] = t;
        }

        ulong impostorId = ulong.MaxValue;
        for (int i = 0; i < indices.Count; i++)
        {
            int pIndex = indices[i];
            var player = players[pIndex];
            if (i == 0)
            {
                player.Role = GameRole.TeamRocket;
                impostorId = player.ClientId;
            }
            else player.Role = GameRole.Trainer;
            players[pIndex] = player;
        }
        return impostorId;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitWordServerRpc(string word, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != activePlayerId.Value) return;

        string senderName = "Unknown";
        var p = AppManager.Instance.GetPlayerData(senderId);
        if (p.HasValue) senderName = p.Value.PlayerName.ToString();

        SubmitWordClientRpc(senderName, word);
        AdvanceTurn();
    }

    [ClientRpc]
    private void SubmitWordClientRpc(string playerName, string word)
    {
        string message = $"{playerName}: {word}";
        OnWordSubmitted?.Invoke(message);
    }

    private void AdvanceTurn()
    {
        currentTurnIndex++;
        if (currentTurnIndex >= turnOrder.Count)
        {
            currentTurnIndex = 0;
            if (currentRound.Value + 1 > gameConfig.roundsToVote)
            {
                StartVotingPhase();
                return;
            }
            currentRound.Value++;
        }
        activePlayerId.Value = turnOrder[currentTurnIndex];
        turnTimer.Value = turnDurationNetVar.Value;
    }

    public bool IsMyTurn() => NetworkManager.Singleton.LocalClientId == activePlayerId.Value;

    private void StartVotingPhase()
    {
        activePlayerId.Value = ulong.MaxValue;
        currentState.Value = GameStateImpostor.Voting;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CastVoteServerRpc(ulong suspectId, ServerRpcParams rpcParams = default)
    {
        ulong voterId = rpcParams.Receive.SenderClientId;
        ShowVoteClientRpc(voterId, suspectId);

        if (currentState.Value != GameStateImpostor.Voting) return;
        if (playerVotes.ContainsKey(voterId)) return;

        playerVotes[voterId] = suspectId;

        if (playerVotes.Count >= AppManager.Instance.NetworkPlayers.Count)
        {
            CalculateResults();
        }
    }

    [ClientRpc]
    private void ShowTurnOrderToPlayersClientRpc(ulong[] networkTurnOrder)
    {
        ImpostorPokeUI.Instance.UpdateTurnOrderLabel(networkTurnOrder.ToList());
    }

    [ClientRpc]
    private void ShowVoteClientRpc(ulong voterId, ulong suspectId)
    {
        OnVoteCast?.Invoke(voterId, suspectId);
    }

    private void CalculateResults()
    {
        currentState.Value = GameStateImpostor.GameOver;
        var voteCounts = playerVotes.GroupBy(v => v.Value).OrderByDescending(g => g.Count()).ToList();

        ulong mostVotedId = (voteCounts.Count > 0) ? voteCounts[0].Key : ulong.MaxValue;

        bool killedImpostor = (mostVotedId == currentImpostorId.Value);
        string impostorName = "Nadie"; // Texto por defecto si nadie fue votado

        if (mostVotedId != ulong.MaxValue)
        {
            var impData = AppManager.Instance.GetPlayerData(currentImpostorId.Value);
            if (impData.HasValue) impostorName = impData.Value.PlayerName.ToString();
        }

        string resultMessage = killedImpostor ?
            $"Ganan los <color=\"green\">Entrenadores</color>!\nImpostor eliminado: <color=\"red\">{impostorName}</color>" :
            $"Gana el <color=\"red\">Team Rocket</color>!\nImpostor: <color=\"red\">{impostorName}</color>";

        ShowGameResultClientRpc(resultMessage, currentPokemonIndex.Value, currentIsShiny.Value);
    }

    [ClientRpc]
    private void ShowGameResultClientRpc(string message, int finalPokemonIndex, bool finalIsShiny)
    {
        if (finalPokemonIndex >= 0 && finalPokemonIndex < pokemonDatabase.allPokemon.Count)
        {
            PokemonEntry info = pokemonDatabase.allPokemon[finalPokemonIndex];
            OnGameEnded?.Invoke(message, info, finalIsShiny);
        }
    }

    public void ReturnToLobby()
    {
        if (!IsServer) return;
        AppManager.Instance.LoadScene("Lobby");
    }
}