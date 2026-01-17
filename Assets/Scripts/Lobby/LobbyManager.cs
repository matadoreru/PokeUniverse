using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance;

    [Header("General Lobby Settings")]
    [SerializeField] public NetworkVariable<GameType> selectedGame = new NetworkVariable<GameType>(GameType.Impostor);

    [Header("Impostor Settings")]
    [SerializeField] public NetworkVariable<float> impostorTurnDuration = new NetworkVariable<float>(30f);
    [SerializeField] public NetworkVariable<int> impostorCount = new NetworkVariable<int>(1);
    [SerializeField] public NetworkVariable<bool> impostorHintGen = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> impostorHintType = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> impostorHintColor = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkList<bool> impostorGenerationStates;

    [Header("Higher Or Lower Settings")]
    [SerializeField] public NetworkVariable<float> hlAnswerTime = new NetworkVariable<float>(30f);
    [SerializeField] public NetworkVariable<Difficulty> hlDifficulty = new NetworkVariable<Difficulty>(Difficulty.MediumHard);

    // Stats Toggles
    [SerializeField] public NetworkVariable<bool> hlGuessHP = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessAT = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessDF = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessSP_AT = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessSP_DF = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessSPD = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessHEI = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> hlGuessWEI = new NetworkVariable<bool>(true);

    [SerializeField] public NetworkList<bool> hlGenerationStates;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        impostorGenerationStates = new NetworkList<bool>();
        hlGenerationStates = new NetworkList<bool>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize Impostor Gens
            if (impostorGenerationStates.Count == 0)
                for (int i = 0; i < 9; i++) impostorGenerationStates.Add(true);

            // Initialize HigherLower Gens
            if (hlGenerationStates.Count == 0)
                for (int i = 0; i < 9; i++) hlGenerationStates.Add(true);
        }
    }

    // --- IMPOSTOR RPCS ---

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSettingsImpostorServerRpc(float time, int impostors, bool hGen, bool hType, bool hColor)
    {
        impostorTurnDuration.Value = time;
        impostorCount.Value = Mathf.Clamp(impostors, 1, 8);
        impostorHintGen.Value = hGen;
        impostorHintType.Value = hType;
        impostorHintColor.Value = hColor;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetGenerationStateServerRpc(int index, bool state)
    {
        if (index >= 0 && index < impostorGenerationStates.Count)
            impostorGenerationStates[index] = state;
    }


    [ServerRpc(RequireOwnership = false)]
    public void UpdateSettingsHigherOrLowerServerRpc(float time, Difficulty diff, bool hp, bool at, bool df, bool spa, bool spd, bool vel, bool wei, bool hei)
    {
        hlAnswerTime.Value = time;
        hlDifficulty.Value = diff;
        hlGuessHP.Value = hp;
        hlGuessAT.Value = at;
        hlGuessDF.Value = df;
        hlGuessSP_AT.Value = spa;
        hlGuessSP_DF.Value = spd;
        hlGuessSPD.Value = vel;
        hlGuessWEI.Value = wei;
        hlGuessHEI.Value = hei;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetGenerationStateHLServerRpc(int index, bool state)
    {
        if (index >= 0 && index < hlGenerationStates.Count)
            hlGenerationStates[index] = state;
    }

    // --- START GAME LOGIC ---

    public void StartGame()
    {
        if (!IsServer) return;

        switch (selectedGame.Value)
        {
            case GameType.Impostor:
                StartGameImpostor();
                break;
            case GameType.HigherOrLower: 
                StartGameHigherOrLower();
                break;
        }
    }

    private void StartGameImpostor()
    {
        ImpostorPokeConfig config = new ImpostorPokeConfig
        {
            roundsToVote = 2,
            turnDuration = impostorTurnDuration.Value,
            impostorCount = impostorCount.Value,
            hintGen = impostorHintGen.Value,
            hintType = impostorHintType.Value,
            hintColor = impostorHintColor.Value,
            activeGenerations = new List<int>()
        };

        for (int i = 0; i < impostorGenerationStates.Count; i++)
        {
            if (impostorGenerationStates[i]) config.activeGenerations.Add(i + 1);
        }
        if (config.activeGenerations.Count == 0) config.activeGenerations.Add(1);

        AppManager.Instance.impostorPokeConfig = config;
        AppManager.Instance.LoadScene("Game_Impostor");
    }

    private void StartGameHigherOrLower()
    {
        HigherOrLowerConfig config = new HigherOrLowerConfig
        {
            answerTime = hlAnswerTime.Value,
            difficulty = hlDifficulty.Value,
            guessHP = hlGuessHP.Value,
            guessAT = hlGuessAT.Value,
            guessDF = hlGuessDF.Value,
            guessATESP = hlGuessSP_AT.Value,
            guessDFESP = hlGuessSP_DF.Value,
            guessVEL = hlGuessSPD.Value,
            guessHEI = hlGuessHEI.Value,
            guessWEI = hlGuessWEI.Value,
            allowedGens = new List<int>()
        };

        for (int i = 0; i < hlGenerationStates.Count; i++)
        {
            if (hlGenerationStates[i]) config.allowedGens.Add(i + 1);
        }
        if (config.allowedGens.Count == 0) config.allowedGens.Add(1);

        AppManager.Instance.higherOrLowerConfig = config;
        AppManager.Instance.LoadScene("Game_HigherOrLower");
    }

    public void LeaveLobby()
    {
        NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}