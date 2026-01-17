using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using static PokemonDatabase;

public class HigherOrLowerManager : NetworkBehaviour
{
    public static HigherOrLowerManager Instance;

    [Header("Data References")]
    [SerializeField] private PokemonDatabase pokemonDatabase;

    [Header("Runtime State")]
    public NetworkVariable<GameStateHigherOrLower> currentState = new NetworkVariable<GameStateHigherOrLower>(GameStateHigherOrLower.Voting);
    public NetworkVariable<int> currentRound = new NetworkVariable<int>(1);
    public NetworkVariable<float> turnTimer = new NetworkVariable<float>(0f);

    public HigherOrLowerConfig gameConfig;

    // Logic Data
    private StatType currentStatType;
    private ComparisonOperator currentOperator;
    private int referencePokemonIndex;

    // Store logic data (Index, IsCorrect)
    private List<(int index, bool isCorrect)> currentOptions = new List<(int, bool)>();
    private Dictionary<ulong, int> playerVotes = new Dictionary<ulong, int>();

    public System.Action<ulong, int> OnVoteCast;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            LoadConfig();
            StartRound();
        }
    }

    private void LoadConfig()
    {
        if (AppManager.Instance != null)
            gameConfig = AppManager.Instance.higherOrLowerConfig;
        else
        {
            gameConfig = new HigherOrLowerConfig
            {
                answerTime = 30f,
                allowedGens = new List<int> { 1 },
                difficulty = Difficulty.MediumHard,
                guessAT = true
            };
        }
    }

    public void StartRound()
    {
        currentState.Value = GameStateHigherOrLower.Voting;
        playerVotes.Clear();
        turnTimer.Value = gameConfig.answerTime;

        PokemonEntry refPoke = pokemonDatabase.GetRandomPokemonFiltered(gameConfig.allowedGens, out referencePokemonIndex);
        PickRandomStatAndOperator();

        if (!GenerateCandidates(refPoke))
        {
            Debug.LogWarning("No se encontraron candidatos. Reintentando...");
            StartRound();
            return;
        }

        // Prepare data arrays for RPC
        int[] optionIndices = currentOptions.Select(x => x.index).ToArray();
        bool[] optionCorrectness = currentOptions.Select(x => x.isCorrect).ToArray();

        ClientRefreshUIClientRpc(
            referencePokemonIndex,
            currentStatType,
            currentOperator,
            optionIndices,
            optionCorrectness
        );
    }

    private void PickRandomStatAndOperator()
    {
        List<StatType> enabledStats = new List<StatType>();
        if (gameConfig.guessHP) enabledStats.Add(StatType.HP);
        if (gameConfig.guessAT) enabledStats.Add(StatType.AT);
        if (gameConfig.guessDF) enabledStats.Add(StatType.DF);
        if (gameConfig.guessATESP) enabledStats.Add(StatType.SP_AT);
        if (gameConfig.guessDFESP) enabledStats.Add(StatType.SP_DF);
        if (gameConfig.guessVEL) enabledStats.Add(StatType.SPD);
        if (gameConfig.guessHEI) enabledStats.Add(StatType.HEIGHT);
        if (gameConfig.guessWEI) enabledStats.Add(StatType.WEIGHT);

        if (enabledStats.Count == 0) enabledStats.Add(StatType.AT);

        currentStatType = enabledStats[Random.Range(0, enabledStats.Count)];
        currentOperator = (Random.value > 0.5f) ? ComparisonOperator.Higher : ComparisonOperator.Lower;
    }

    private int GetStatValue(PokemonEntry p, StatType stat)
    {
        switch (stat)
        {
            case StatType.HP: return p.stats[0];
            case StatType.AT: return p.stats[1];
            case StatType.DF: return p.stats[2];
            case StatType.SP_AT: return p.stats[3];
            case StatType.SP_DF: return p.stats[4];
            case StatType.SPD: return p.stats[5];
            case StatType.HEIGHT: return (int)(p.heightMeters * 10);
            case StatType.WEIGHT: return (int)(p.weightKg * 10);
            default: return 0;
        }
    }

    private void GetGapRange(Difficulty diff, out int minGap, out int maxGap)
    {
        minGap = (int)diff; 

        switch (diff)
        {
            case Difficulty.Hard: maxGap = (int)Difficulty.MediumHard; break; // 10 a 15
            case Difficulty.MediumHard: maxGap = (int)Difficulty.Medium; break;     // 15 a 25
            case Difficulty.Medium: maxGap = (int)Difficulty.MediumEasy; break; // 25 a 35
            case Difficulty.MediumEasy: maxGap = (int)Difficulty.Easy; break;       // 35 a 40
            case Difficulty.Easy: maxGap = (int)Difficulty.VeryEasy; break;   // 40 a 50
            case Difficulty.VeryEasy: maxGap = 9999; break; // 50 a Infinito
            default: maxGap = 9999; break;
        }
    }

    private bool GenerateCandidates(PokemonEntry refPoke)
    {
        currentOptions.Clear();
        List<int> correctPool = new List<int>();
        List<int> incorrectPool = new List<int>();

        int refValue = GetStatValue(refPoke, currentStatType);

        GetGapRange(gameConfig.difficulty, out int minGap, out int maxGap);

        for (int i = 0; i < pokemonDatabase.allPokemon.Count; i++)
        {
            if (i == referencePokemonIndex) continue;
            if (!gameConfig.allowedGens.Contains(pokemonDatabase.allPokemon[i].generation)) continue;

            int candValue = GetStatValue(pokemonDatabase.allPokemon[i], currentStatType);
            int difference = candValue - refValue;
            int absDiff = Mathf.Abs(difference);

            bool isInRange = (absDiff >= minGap && absDiff <= maxGap);

            if (!isInRange) continue; 

            if (currentOperator == ComparisonOperator.Higher)
            {
                if (difference > 0) correctPool.Add(i);     
                else incorrectPool.Add(i);                 
            }
            else
            {
                if (difference < 0) correctPool.Add(i);      
                else incorrectPool.Add(i);                   
            }
        }

        if (correctPool.Count < 1)
        {
            for (int i = 0; i < pokemonDatabase.allPokemon.Count; i++)
            {
                if (i == referencePokemonIndex) continue;
                if (!gameConfig.allowedGens.Contains(pokemonDatabase.allPokemon[i].generation)) continue;

                int val = GetStatValue(pokemonDatabase.allPokemon[i], currentStatType);
                bool isHigher = val > refValue;
                bool isLower = val < refValue; 

                if (currentOperator == ComparisonOperator.Higher && isHigher) correctPool.Add(i);
                if (currentOperator == ComparisonOperator.Lower && isLower) correctPool.Add(i);
            }
        }

        if (incorrectPool.Count < 2)
        {
            for (int i = 0; i < pokemonDatabase.allPokemon.Count; i++)
            {
                if (i == referencePokemonIndex) continue;
                if (!gameConfig.allowedGens.Contains(pokemonDatabase.allPokemon[i].generation)) continue;
                if (correctPool.Contains(i)) continue; // No repetir

                int val = GetStatValue(pokemonDatabase.allPokemon[i], currentStatType);
                bool isHigher = val > refValue;
                bool isLower = val < refValue;

                if (currentOperator == ComparisonOperator.Higher && isLower) incorrectPool.Add(i);
                if (currentOperator == ComparisonOperator.Lower && isHigher) incorrectPool.Add(i);
            }
        }

        if (correctPool.Count < 1 || incorrectPool.Count < 2) return false;

        int correctIdx = correctPool[Random.Range(0, correctPool.Count)];
        currentOptions.Add((correctIdx, true));

        for (int i = 0; i < incorrectPool.Count; i++)
        {
            int r = Random.Range(i, incorrectPool.Count);
            int t = incorrectPool[i]; incorrectPool[i] = incorrectPool[r]; incorrectPool[r] = t;
        }
        currentOptions.Add((incorrectPool[0], false));
        currentOptions.Add((incorrectPool[1], false));

        for (int i = 0; i < currentOptions.Count; i++)
        {
            int r = Random.Range(i, currentOptions.Count);
            var t = currentOptions[i]; currentOptions[i] = currentOptions[r]; currentOptions[r] = t;
        }

        return true;
    }

    [ClientRpc]
    private void ClientRefreshUIClientRpc(int refIndex, StatType stat, ComparisonOperator op, int[] options, bool[] correctness)
    {
        PokemonEntry refPoke = pokemonDatabase.allPokemon[refIndex];

        // Pass Raw Data to UI
        List<(PokemonEntry, bool)> uiOptions = new List<(PokemonEntry, bool)>();
        for (int i = 0; i < options.Length; i++)
        {
            uiOptions.Add((pokemonDatabase.allPokemon[options[i]], correctness[i]));
        }

        HigherOrLowerUI.Instance.SetupRound(refPoke, stat, op, uiOptions);
    }

    private void Update()
    {
        if (IsServer && currentState.Value == GameStateHigherOrLower.Voting)
        {
            turnTimer.Value -= Time.deltaTime;
            if (turnTimer.Value <= 0) ResolveRound();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CastVoteServerRpc(int buttonIndex, ServerRpcParams rpcParams = default)
    {
        if (currentState.Value != GameStateHigherOrLower.Voting) return;
        ulong senderId = rpcParams.Receive.SenderClientId;
        ShowVoteClientRpc(senderId, buttonIndex);

        if (!playerVotes.ContainsKey(senderId))
        {
            playerVotes[senderId] = buttonIndex;
            if (playerVotes.Count >= AppManager.Instance.NetworkPlayers.Count)
            {
                ResolveRound();
            }
        }
    }

    [ClientRpc]
    private void ShowVoteClientRpc(ulong voterId, int buttonIndex)
    {
        OnVoteCast?.Invoke(voterId, buttonIndex);
    }

    private void ResolveRound()
    {
        currentState.Value = GameStateHigherOrLower.Result;

        var votes = playerVotes.Values.GroupBy(x => x).OrderByDescending(g => g.Count());
        int winningButtonIndex = -1;
        if (votes.Any()) winningButtonIndex = votes.First().Key;

        bool success = false;
        if (winningButtonIndex != -1 && winningButtonIndex < currentOptions.Count)
        {
            success = currentOptions[winningButtonIndex].isCorrect;
        }

        if (success)
        {
            currentRound.Value++;
            Invoke(nameof(StartRound), 3.0f);
        }
        else
        {
            // Find the correct one to show in Game Over
            int correctIndexInList = currentOptions.FindIndex(x => x.isCorrect);
            int correctPokeDbIndex = currentOptions[correctIndexInList].index;
            ShowGameOverClientRpc(correctPokeDbIndex);
        }
    }

    [ClientRpc]
    private void ShowGameOverClientRpc(int correctPokeIndex)
    {
        PokemonEntry correctP = pokemonDatabase.allPokemon[correctPokeIndex];
        HigherOrLowerUI.Instance.ShowGameOver(correctP);
    }

    public void ReturnToLobby()
    {
        if (IsServer) AppManager.Instance.LoadScene("Lobby");
    }
}