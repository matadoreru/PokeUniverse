using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using static PokemonDatabase;
using UnityEngine.UI;

public class ImpostorPokeUI : MonoBehaviour
{
    public static ImpostorPokeUI Instance;

    [Header("Sub-Modules")]
    [SerializeField] private ImpostorHUDUI hudUI;
    [SerializeField] private ImpostorGameOverUI gameOverUI;
    [SerializeField] private ImpostorVotingUI votingUI;
    [SerializeField] private ImpostorSecretUI secretUI;

    [Header("Player List UI")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerCardPrefab;

    [Header("Tab List UI")]
    [SerializeField] private Button logTabButton;
    [SerializeField] private Button voteTabButton;

    private List<ulong> currentTurnOrderCache = new List<ulong>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // Suscripciones a Eventos del Manager
        ImpostorPokeManager.Instance.OnTurnChanged += RefreshTurnUI;
        ImpostorPokeManager.Instance.OnWordSubmitted += HandleLogEntry;
        ImpostorPokeManager.Instance.OnStateChanged += HandleStateChanged;
        ImpostorPokeManager.Instance.OnGameEnded += HandleGameEnded;
        ImpostorPokeManager.Instance.OnVoteCast += (voter, suspect) => votingUI.AddVoteIndicator(suspect, voter);

        // Suscripción a lista de jugadores
        if (AppManager.Instance != null)
        {
            AppManager.Instance.NetworkPlayers.OnListChanged += OnPlayerListChanged;
        }

        ResetAllPanels();
        RefreshPlayerList();

        // Si entramos tarde y el juego ya empezó (Late Join)
        if (ImpostorPokeManager.Instance.currentState.Value == GameState.Playing)
        {
            ImpostorPokeManager.Instance.ClientRefreshSecretUI();
            RefreshTurnUI();
        }
    }

    private void OnDestroy()
    {
        if (AppManager.Instance != null && AppManager.Instance.NetworkPlayers != null)
        {
            AppManager.Instance.NetworkPlayers.OnListChanged -= OnPlayerListChanged;
        }
    }

    private void Update()
    {
        // Actualización del Timer vía Update (más fluido que NetworkVariable events)
        if (ImpostorPokeManager.Instance.currentState.Value == GameState.Playing)
        {
            float maxTime = ImpostorPokeManager.Instance.turnDurationNetVar.Value;
            float currentTime = ImpostorPokeManager.Instance.turnTimer.Value;
            hudUI.UpdateTimer(currentTime, maxTime);
        }
    }

    // --- State Handlers ---

    private void HandleStateChanged(GameState newState)
    {
        if (newState == GameState.Playing)
        {
            voteTabButton.interactable = false;
            logTabButton.interactable = false;
            gameOverUI.Hide();
            votingUI.Hide();
            hudUI.Show();
            RefreshTurnUI();
        }
        else if (newState == GameState.Voting)
        {
            hudUI.Hide(); 
            secretUI.Show();
            votingUI.Show();

            logTabButton.interactable = true;
            votingUI.SetupButtons(AppManager.Instance.NetworkPlayers, NetworkManager.Singleton.LocalClientId);
        }
    }

    private void HandleGameEnded(string result, PokemonEntry info, bool isShiny)
    {
        votingUI.Hide();
        hudUI.Hide();
        secretUI.Hide();
        gameOverUI.Show(result, info, isShiny);
        hudUI.ClearLog();
    }

    public void SetupSecretScreen(PokemonEntry info, bool isImpostor, bool isShiny, bool hGen, bool hType, bool hColor)
    {
        secretUI.SetupDisplay(info, isImpostor, isShiny, hGen, hType, hColor);
    }

    public void UpdateTurnOrderLabel(List<ulong> idTurnOrder)
    {
        currentTurnOrderCache = new List<ulong>(idTurnOrder);
        RefreshTurnUI();
    }

    private void RefreshTurnUI()
    {
        if (ImpostorPokeManager.Instance.currentState.Value != GameState.Playing) return;

        ulong activeId = ImpostorPokeManager.Instance.activePlayerId.Value;
        bool isMyTurn = ImpostorPokeManager.Instance.IsMyTurn();

        string activeName = "Unknown";
        var p = AppManager.Instance.GetPlayerData(activeId);
        if (p.HasValue) activeName = p.Value.PlayerName.ToString();
        hudUI.Show();
        hudUI.UpdateTurnStatus(isMyTurn, activeName);
        hudUI.UpdateTurnOrderText(currentTurnOrderCache, activeId);
    }

    private void HandleLogEntry(string msg)
    {
        // Obtenemos ronda desde Manager
        int r = ImpostorPokeManager.Instance.currentRound.Value;
        int maxR = ImpostorPokeManager.Instance.gameConfig.roundsToVote;
        // Si gameConfig no está sincronizado en cliente perfectamente, usamos 2 por defecto
        if (maxR == 0) maxR = 2;

        hudUI.AddToLog(msg, r, maxR);
    }

    private void ResetAllPanels()
    {
        secretUI.Hide();
        hudUI.Hide();
        votingUI.Hide();
        gameOverUI.Hide();
    }

    public void OnLogTabClicked()
    {
        logTabButton.interactable = false;
        voteTabButton.interactable = true;
        votingUI.Hide();
        hudUI.ShowLogPanel();
    }

    public void OnVoteTabClicked()
    {
        logTabButton.interactable = true;
        voteTabButton.interactable = false;
        hudUI.HideLogPanel();
        votingUI.Show();
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerData> changeEvent) => RefreshPlayerList();

    private void RefreshPlayerList()
    {
        if (playerListContainer == null || playerCardPrefab == null) return;
        foreach (Transform child in playerListContainer) Destroy(child.gameObject);

        int index = 1;
        if (AppManager.Instance == null) return;

        foreach (var player in AppManager.Instance.NetworkPlayers)
        {
            GameObject cardObj = Instantiate(playerCardPrefab, playerListContainer);
            LobbyPlayerCard cardScript = cardObj.GetComponent<LobbyPlayerCard>();
            if (cardScript != null)
            {
                Texture2D avatar = null;
                if (AppManager.Instance.steamAvatars.ContainsKey(player.SteamId))
                    avatar = AppManager.Instance.steamAvatars[player.SteamId];
                cardScript.SetPlayerInfo(player.PlayerName.ToString(), avatar, index, player.ClientId);
            }
            index++;
        }
    }
}