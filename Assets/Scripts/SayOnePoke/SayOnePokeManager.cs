using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using static PokemonDatabase;

public class SayOnePokeManager : NetworkBehaviour
{
    public static SayOnePokeManager Instance;

    [Header("Data")]
    [SerializeField] private PokemonDatabase pokemonDatabase;

    [Header("State")]
    public NetworkVariable<DuelState> currentState = new NetworkVariable<DuelState>(DuelState.Waiting);
    public NetworkVariable<float> timer = new NetworkVariable<float>(0);

    public NetworkVariable<ulong> player1Id = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> player2Id = new NetworkVariable<ulong>(ulong.MaxValue);

    public SayOnePokemonConfig config;

    private List<ulong> bagOfPlayers = new List<ulong>();
    private int cyclesPlayed = 0;

    public Dictionary<ulong, int> scores = new Dictionary<ulong, int>();

    private string targetType = "";
    private int targetGen = -1;
    // Opcional: targetColor

    public enum DuelState { Waiting, Countdown, Active, RoundResult, GameOver }

    private void Awake() { if (Instance == null) Instance = this; }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Inicializar scores
            foreach (var p in AppManager.Instance.NetworkPlayers) scores[p.ClientId] = 0;

            LoadConfig();
            StartNewDuelLoop();
        }
    }

    // Getter para la UI
    public PokemonDatabase GetDatabase() => pokemonDatabase;

    private void LoadConfig()
    {
        if (AppManager.Instance != null) config = AppManager.Instance.sayOnePokemonConfig;
        else
        {
            // Fallback para pruebas directas en editor
            config = new SayOnePokemonConfig { answerTime = 10, totalCycles = 2, allowedGens = new List<int> { 1 } };
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (currentState.Value == DuelState.Active || currentState.Value == DuelState.Countdown)
        {
            timer.Value -= Time.deltaTime;
            if (timer.Value <= 0)
            {
                if (currentState.Value == DuelState.Countdown) BeginDuel();
                else if (currentState.Value == DuelState.Active) EndDuelTimeout();
            }
        }
    }

    // --- LÓGICA DE EMPAREJAMIENTO ---

    private void StartNewDuelLoop()
    {
        // Si no quedan suficientes jugadores en la bolsa, rellenar
        if (bagOfPlayers.Count < 2)
        {
            if (bagOfPlayers.Count == 0 && cyclesPlayed >= config.totalCycles)
            {
                EndGame();
                return;
            }
            RefillBag();
            cyclesPlayed++;
        }

        // Sacar 2 jugadores
        ulong p1 = bagOfPlayers[0];
        ulong p2 = bagOfPlayers[1];
        bagOfPlayers.RemoveRange(0, 2);

        player1Id.Value = p1;
        player2Id.Value = p2;

        GenerateChallenge();

        currentState.Value = DuelState.Countdown;
        timer.Value = 3.0f; // 3 segs preparación
    }

    private void RefillBag()
    {
        List<ulong> allIds = new List<ulong>();
        foreach (var p in AppManager.Instance.NetworkPlayers) allIds.Add(p.ClientId);

        // Shuffle
        var count = allIds.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i)
        {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = allIds[i]; allIds[i] = allIds[r]; allIds[r] = tmp;
        }

        bagOfPlayers.AddRange(allIds);
    }

    private void GenerateChallenge()
    {
        int index;
        // Obtenemos uno válido para asegurar que el reto es posible
        PokemonEntry sample = pokemonDatabase.GetRandomPokemonFiltered(config.allowedGens, out index);

        // Elegimos un criterio aleatorio: ¿Tipo? ¿Gen? ¿Ambos?
        // Para simplificar V1: Tipo + Generación
        targetType = sample.types[0];
        targetGen = sample.generation;

        SetChallengeClientRpc($"BUSCA: <color=yellow>{targetType}</color> de <color=orange>Gen {targetGen}</color>");
    }


    [ClientRpc]
    private void SetChallengeClientRpc(string text)
    {
        SayOnePokeUI.Instance.UpdateChallengeText(text);
    }

    private void BeginDuel()
    {
        currentState.Value = DuelState.Active;
        timer.Value = config.answerTime;
    }

    // --- VALIDACIÓN ---

    [ServerRpc(RequireOwnership = false)]
    public void SubmitPokemonServerRpc(int pokemonIndex, ServerRpcParams rpcParams = default)
    {
        if (currentState.Value != DuelState.Active) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != player1Id.Value && senderId != player2Id.Value) return;

        bool isCorrect = CheckAnswer(pokemonIndex);

        if (isCorrect)
        {
            scores[senderId] += 1;
            EndDuelRound(senderId, pokemonIndex, true);
        }
        // Si falla, no hace nada (pierde tiempo)
    }

    private bool CheckAnswer(int index)
    {
        if (index < 0 || index >= pokemonDatabase.allPokemon.Count) return false;
        var p = pokemonDatabase.allPokemon[index];

        bool typeMatch = false;
        foreach (var t in p.types) if (t == targetType) typeMatch = true;

        bool genMatch = (p.generation == targetGen);

        return typeMatch && genMatch;
    }

    private void EndDuelTimeout()
    {
        EndDuelRound(ulong.MaxValue, -1, false);
    }

    private void EndDuelRound(ulong winnerId, int winningPokemonIndex, bool success)
    {
        currentState.Value = DuelState.RoundResult;

        string msg = "¡Tiempo! Empate.";
        if (success)
        {
            string pName = "Unknown";
            var pData = AppManager.Instance.GetPlayerData(winnerId);
            if (pData.HasValue) pName = pData.Value.PlayerName.ToString();

            msg = $"¡Punto para {pName}!";
        }

        ShowRoundResultClientRpc(msg);
        Invoke(nameof(StartNewDuelLoop), 3.0f);
    }

    [ClientRpc]
    private void ShowRoundResultClientRpc(string msg)
    {
        SayOnePokeUI.Instance.ShowRoundResult(msg);
    }

    // --- GAME OVER ---

    private void EndGame()
    {
        currentState.Value = DuelState.GameOver;

        var ranking = scores.OrderByDescending(x => x.Value).ToList();
        string rankText = "RANKING FINAL:\n\n";
        int pos = 1;
        foreach (var entry in ranking)
        {
            string pName = "Unknown";
            var pData = AppManager.Instance.GetPlayerData(entry.Key);
            if (pData.HasValue) pName = pData.Value.PlayerName.ToString();

            rankText += $"{pos}. {pName} - {entry.Value} pts\n";
            pos++;
        }

        ShowGameOverClientRpc(rankText);
    }

    [ClientRpc]
    private void ShowGameOverClientRpc(string text)
    {
        SayOnePokeUI.Instance.ShowGameOver(text);
    }

    public void ReturnToLobby()
    {
        if (!IsServer) return;
        AppManager.Instance.LoadScene("Lobby");
    }

    public void ReplayGame()
    {
        if (!IsServer) return;
        foreach (var key in scores.Keys.ToList()) scores[key] = 0;
        cyclesPlayed = 0;
        bagOfPlayers.Clear();
        StartNewDuelLoop();
    }
}