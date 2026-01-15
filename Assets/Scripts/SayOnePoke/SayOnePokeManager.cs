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

    // --- NUEVO: Sincronización de Generaciones ---
    public NetworkList<int> allowedGensNetList;

    public SayOnePokemonConfig config; // Configuración local (solo útil en Host)

    // Logica interna
    private List<ulong> bagOfPlayers = new List<ulong>();
    private int cyclesPlayed = 0;
    public Dictionary<ulong, int> scores = new Dictionary<ulong, int>();

    // Reto actual
    private string targetType = "";
    private int targetGen = -1;

    public enum DuelState { Waiting, Countdown, Active, RoundResult, GameOver }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        allowedGensNetList = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Inicializar scores
            foreach (var p in AppManager.Instance.NetworkPlayers)
            {
                if (!scores.ContainsKey(p.ClientId)) scores.Add(p.ClientId, 0);
            }

            LoadConfigAndSync(); // Cargar y sincronizar con clientes
            StartNewDuelLoop();
        }
    }

    public PokemonDatabase GetDatabase() => pokemonDatabase;

    private void LoadConfigAndSync()
    {
        if (AppManager.Instance != null)
        {
            config = AppManager.Instance.sayOnePokemonConfig;
        }

        // Si la configuración está vacía (ej. prueba directa), poner defaults
        if (config.allowedGens == null || config.allowedGens.Count == 0)
        {
            config.allowedGens = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            config.answerTime = 15f;
        }

        // Sincronizar la lista de generaciones con la Red
        allowedGensNetList.Clear();
        foreach (int gen in config.allowedGens)
        {
            allowedGensNetList.Add(gen);
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
                if (currentState.Value == DuelState.Countdown)
                {
                    BeginDuel();
                }
                else if (currentState.Value == DuelState.Active)
                {
                    EndDuelTimeout();
                }
            }
        }
    }

    // --- LÓGICA DE JUEGO ---

    private void StartNewDuelLoop()
    {
        if (bagOfPlayers.Count < 2)
        {
            int maxCycles = (config.totalCycles > 0) ? config.totalCycles : 2;
            if (bagOfPlayers.Count == 0 && cyclesPlayed >= maxCycles)
            {
                EndGame();
                return;
            }
            RefillBag();
            cyclesPlayed++;
        }

        ulong p1 = bagOfPlayers[0];
        ulong p2 = bagOfPlayers[1];
        bagOfPlayers.RemoveRange(0, 2);

        player1Id.Value = p1;
        player2Id.Value = p2;

        GenerateChallenge();

        // Estado Countdown: Aquí el input estará bloqueado 3 segundos
        currentState.Value = DuelState.Countdown;
        timer.Value = 3.0f;
    }

    private void RefillBag()
    {
        List<ulong> allIds = new List<ulong>();
        foreach (var p in AppManager.Instance.NetworkPlayers) allIds.Add(p.ClientId);

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
        // Usar la lista de red para filtrar (así el Host usa lo mismo que los clientes verán)
        List<int> validGens = new List<int>();
        foreach (int g in allowedGensNetList) validGens.Add(g);

        int index;
        PokemonEntry sample = pokemonDatabase.GetRandomPokemonFiltered(validGens, out index);

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
        // Al pasar a Active, el input se desbloqueará en la UI
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
            if (!scores.ContainsKey(senderId)) scores[senderId] = 0;
            scores[senderId] += 1;
            EndDuelRound(senderId, pokemonIndex, true);
        }
    }

    private bool CheckAnswer(int index)
    {
        if (index < 0 || index >= pokemonDatabase.allPokemon.Count) return false;
        var p = pokemonDatabase.allPokemon[index];

        bool typeMatch = false;
        foreach (var t in p.types)
            if (t.Trim().Equals(targetType.Trim(), System.StringComparison.OrdinalIgnoreCase))
                typeMatch = true;

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

        string msg = "¡Tiempo! Nadie acertó.";
        if (success)
        {
            string pName = "Jugador";
            var pData = AppManager.Instance.GetPlayerData(winnerId);
            if (pData.HasValue) pName = pData.Value.PlayerName.ToString();

            string pokeName = pokemonDatabase.allPokemon[winningPokemonIndex].pokemonName;
            msg = $"¡Punto para {pName}!\nUsó: {pokeName}";
        }

        ShowRoundResultClientRpc(msg);
        Invoke(nameof(StartNewDuelLoop), 3.0f);
    }

    [ClientRpc]
    private void ShowRoundResultClientRpc(string msg)
    {
        SayOnePokeUI.Instance.ShowRoundResult(msg);
    }

    private void EndGame()
    {
        currentState.Value = DuelState.GameOver;

        var ranking = scores.OrderByDescending(x => x.Value).ToList();
        string rankText = "RANKING FINAL:\n\n";
        int pos = 1;
        foreach (var entry in ranking)
        {
            string pName = "Desconocido";
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
        var keys = new List<ulong>(scores.Keys);
        foreach (var key in keys) scores[key] = 0;
        cyclesPlayed = 0;
        bagOfPlayers.Clear();
        StartNewDuelLoop();
    }
}