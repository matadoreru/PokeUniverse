using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using static PokemonDatabase;
using Unity.Netcode;

public class HigherOrLowerUI : MonoBehaviour
{
    public static HigherOrLowerUI Instance;

    [Header("Prefabs")]
    [SerializeField] private GameObject voteAvatarPrefab;

    [Header("Panels")]
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Player List UI")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerCardPrefab;

    [Header("Reference Pokemon")]
    [SerializeField] private Image refImage;
    [SerializeField] private TextMeshProUGUI refNameText;
    [SerializeField] private TextMeshProUGUI challengeText; 
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Options")]
    [SerializeField] public GuessPokemonButton correctButton;
    [SerializeField] private GuessPokemonButton[] optionButtons; 

    [Header("Game Over")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Image refGameOverImage;
    [SerializeField] private TextMeshProUGUI refGameOverNameText;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private Button replayButton;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        HigherOrLowerManager.Instance.OnVoteCast += (voter, idxButton) => AddVoteIndicator(voter, idxButton);

        if (AppManager.Instance != null)
        {
            AppManager.Instance.NetworkPlayers.OnListChanged += OnPlayerListChanged;
        }

        RefreshPlayerList();

        
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

    private void Update()
    {
        if (HigherOrLowerManager.Instance != null)
        {
            timerText.text = HigherOrLowerManager.Instance.turnTimer.Value.ToString("F0");
        }
    }

    public void SetupRound(PokemonEntry refPoke, StatType stat, ComparisonOperator op, List<(PokemonEntry poke, bool isCorrect)> options)
    {
        gamePanel.SetActive(true);
        gameOverPanel.SetActive(false);

        // Setup Reference
        refImage.sprite = refPoke.pokemonSprite;
        refNameText.text = refPoke.pokemonName;

        // Setup Text
        string statName = GetStatName(stat);
        string opText = (op == ComparisonOperator.Higher) ? "MAS" : "MENOS";
        string color = (op == ComparisonOperator.Higher) ? "green" : "red";

        challengeText.text = $"Elige un Pokémon con <color={color}>{opText}</color> <b>{statName}</b>";

        // Setup Buttons
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                // Pass raw data to Setup
                optionButtons[i].Setup(options[i].poke, i, options[i].isCorrect);
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void ShowGameOver(PokemonEntry correctPokemon)
    {
        // Add delay or show immediately
        ShowGameOverScreen(correctPokemon);
    }

    private void ShowGameOverScreen(PokemonEntry correctPokemon)
    {
        gamePanel.SetActive(false);
        gameOverPanel.SetActive(true);
        resultText.text = $"GAME OVER! Ronda: {HigherOrLowerManager.Instance.currentRound.Value}";

        refGameOverImage.sprite = correctPokemon.pokemonSprite;
        refGameOverNameText.text = correctPokemon.pokemonName;

        lobbyButton.onClick.RemoveAllListeners();
        lobbyButton.onClick.AddListener(() => HigherOrLowerManager.Instance.ReturnToLobby());
        // Add replay logic if needed
    }

    private string GetStatName(StatType s)
    {
        switch (s)
        {
            case StatType.HP: return "HP";
            case StatType.AT: return "Ataque";
            case StatType.DF: return "Defensa";
            case StatType.SP_AT: return "At. Especial";
            case StatType.SP_DF: return "Df. Especial";
            case StatType.SPD: return "Velocidad";
            case StatType.HEIGHT: return "Altura";
            case StatType.WEIGHT: return "Peso";
            default: return "Stat";
        }
    }

    private void AddVoteIndicator(ulong senderId, int idxButton) {
        GuessPokemonButton pokeButton = optionButtons[idxButton];

        if (pokeButton.playersWhoHaveVoted.Contains(senderId)) return;

        pokeButton.playersWhoHaveVoted.Add(senderId);

        Texture2D avatarTex = null;
        if (AppManager.Instance != null)
        {
            var voterData = AppManager.Instance.GetPlayerData(senderId);
            if (voterData.HasValue && AppManager.Instance.steamAvatars.ContainsKey(voterData.Value.SteamId))
            {
                avatarTex = AppManager.Instance.steamAvatars[voterData.Value.SteamId];
            }
        }

        GameObject avatarObj = Instantiate(voteAvatarPrefab, pokeButton.voteContainer.transform);
        var rawImg = avatarObj.GetComponent<RawImage>();
        if (rawImg != null) rawImg.texture = avatarTex;                     
    }
}