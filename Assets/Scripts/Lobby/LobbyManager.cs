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

    [Header("Say One Pokemon Settings")]
    [SerializeField] public NetworkVariable<float> sayOnePokeTurnDuration = new NetworkVariable<float>(30f);
    [SerializeField] public NetworkVariable<int> sayOnePokeTotalCycles = new NetworkVariable<int>(1);
    [SerializeField] public NetworkVariable<bool> sayOnePokeFilterByGen = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkVariable<bool> sayOnePokeFilterByType = new NetworkVariable<bool>(true);
    [SerializeField] public NetworkList<bool> sayOnePokeGenerationStates;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        impostorGenerationStates = new NetworkList<bool>();
        sayOnePokeGenerationStates = new NetworkList<bool>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (impostorGenerationStates.Count == 0)
            {
                for (int i = 0; i < 9; i++) impostorGenerationStates.Add(true);
            }
        }
    }


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
        {
            impostorGenerationStates[index] = state;
        }
    }

    public void StartGame()
    {
        if (!IsServer) return;

        switch (selectedGame.Value) {
            case GameType.Impostor:
                StartGameImpostor();
                break;
            case GameType.SayOnePokemon:
                StartGameSayOnePokemon();
                break;
        }
    }

    private void StartGameImpostor()
    {
        // TODO HARDCODED NUM OF ROUNDS CHANGE IT
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

    private void StartGameSayOnePokemon()
    {
        SayOnePokemonConfig config = new SayOnePokemonConfig
        {
            answerTime = sayOnePokeTurnDuration.Value, 
            totalCycles = sayOnePokeTotalCycles.Value, 
            filterByGen = sayOnePokeFilterByGen.Value,
            filterByType = sayOnePokeFilterByType.Value,
            allowedGens = new List<int>()
        };

        for (int i = 0; i < sayOnePokeGenerationStates.Count; i++)
        {
            if (sayOnePokeGenerationStates[i]) config.allowedGens.Add(i + 1);
        }
        if (config.allowedGens.Count == 0) config.allowedGens.Add(1);

        AppManager.Instance.sayOnePokemonConfig = config;
        AppManager.Instance.LoadScene("Game_SayOnePoke");
    }
    public void LeaveLobby()
    {
        NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}