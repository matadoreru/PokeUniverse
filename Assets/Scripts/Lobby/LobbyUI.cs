using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;
using System;

public class LobbyUI : MonoBehaviour
{
    [Header("Player List")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private LobbyPlayerCard playerCardPrefab;
    [SerializeField] private TextMeshProUGUI roomCodeText;

    [Header("Game Controls")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    [Header("Tab Panels")]
    [SerializeField] private List<Button> tabButtons;
    [SerializeField] private List<GameObject> rulePanels;

    [SerializeField] private Toggle[] genToggles;

    [Header("Impostor Settings UI")]
    [SerializeField] private TMP_InputField turnTimeInput;
    [SerializeField] private TMP_InputField impostorCountInput;
    [SerializeField] private Toggle hintGenToggle;
    [SerializeField] private Toggle hintTypeToggle;
    [SerializeField] private Toggle hintColorToggle;

    [Header("Higher or Lower UI")]
    [SerializeField] private TMP_InputField hlRoundTimeInput;
    [SerializeField] private TMP_Dropdown hlDifficultyDropdown;
    [SerializeField] private Toggle guessHP;
    [SerializeField] private Toggle guessAT;
    [SerializeField] private Toggle guessDF;
    [SerializeField] private Toggle guessATESP;
    [SerializeField] private Toggle guessDFESP;
    [SerializeField] private Toggle guessVEL;
    [SerializeField] private Toggle guessHEI;
    [SerializeField] private Toggle guessWEI;

    private bool isUpdatingVisuals = false;

    // --- CORRECCIÓN: Definimos el orden explícito para el Dropdown ---
    private List<Difficulty> orderedDifficulties = new List<Difficulty>
    {
        Difficulty.VeryEasy,    // Index 0
        Difficulty.Easy,        // Index 1
        Difficulty.MediumEasy,  // Index 2
        Difficulty.Medium,      // Index 3
        Difficulty.MediumHard,  // Index 4
        Difficulty.Hard         // Index 5
    };

    private void Start()
    {
        // 1. Configuramos el Dropdown antes de añadir listeners
        SetupDifficultyDropdown();

        SetupButtons();
        SetupInputsImpostor();
        SetupInputsHigherLower();
        SetupGenToggles();
        SetupNetworkEvents();

        // Init
        RefreshPlayerList();
        UpdateConfigVisuals();
        UpdateUIForClientRole();

        if (AppManager.Instance != null && AppManager.Instance.currentLobby.HasValue)
            roomCodeText.text = AppManager.Instance.currentLobby.Value.GetData("CODE");
    }

    private void OnDestroy()
    {
        // Buena práctica: desuscribirse para evitar errores si se destruye el objeto
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            UpdateUIForClientRole();
        }
    }

    // --- SETUP HELPERS ---

    // Nuevo método para poblar el dropdown correctamente
    private void SetupDifficultyDropdown()
    {
        if (hlDifficultyDropdown == null) return;

        hlDifficultyDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (var diff in orderedDifficulties)
        {
            // Puedes poner un string personalizado aquí si quieres (ej: "Muy Fácil")
            options.Add(diff.ToString());
        }

        hlDifficultyDropdown.AddOptions(options);
    }

    private void SetupButtons()
    {
        if (startGameButton) startGameButton.onClick.AddListener(() => LobbyManager.Instance.StartGame());
        if (leaveButton) leaveButton.onClick.AddListener(() => LobbyManager.Instance.LeaveLobby());
    }

    private void SetupInputsImpostor()
    {
        turnTimeInput.onValueChanged.AddListener(val => SendSettingsImpostor());
        impostorCountInput.onValueChanged.AddListener(val => SendSettingsImpostor());
        hintGenToggle.onValueChanged.AddListener(val => SendSettingsImpostor());
        hintTypeToggle.onValueChanged.AddListener(val => SendSettingsImpostor());
        hintColorToggle.onValueChanged.AddListener(val => SendSettingsImpostor());
    }

    private void SetupInputsHigherLower()
    {
        hlRoundTimeInput.onValueChanged.AddListener(val => SendSettingsHL());

        // El dropdown ahora tiene el orden correcto garantizado
        hlDifficultyDropdown.onValueChanged.AddListener(val => SendSettingsHL());

        guessHP.onValueChanged.AddListener(val => SendSettingsHL());
        guessAT.onValueChanged.AddListener(val => SendSettingsHL());
        guessDF.onValueChanged.AddListener(val => SendSettingsHL());
        guessATESP.onValueChanged.AddListener(val => SendSettingsHL());
        guessDFESP.onValueChanged.AddListener(val => SendSettingsHL());
        guessVEL.onValueChanged.AddListener(val => SendSettingsHL());
        guessHEI.onValueChanged.AddListener(val => SendSettingsHL());
        guessWEI.onValueChanged.AddListener(val => SendSettingsHL());
    }

    private void SetupGenToggles()
    {
        for (int i = 0; i < genToggles.Length; i++)
        {
            int index = i;
            genToggles[i].onValueChanged.AddListener((isOn) =>
            {
                if (!isUpdatingVisuals && NetworkManager.Singleton.IsServer)
                {
                    if (LobbyManager.Instance.selectedGame.Value == GameType.Impostor)
                        LobbyManager.Instance.SetGenerationStateServerRpc(index, isOn);
                    else
                        LobbyManager.Instance.SetGenerationStateHLServerRpc(index, isOn);
                }
            });
        }
    }

    private void SetupNetworkEvents()
    {
        if (LobbyManager.Instance != null)
        {
            // Impostor
            LobbyManager.Instance.impostorTurnDuration.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.impostorCount.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.impostorHintGen.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.impostorHintType.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.impostorHintColor.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.impostorGenerationStates.OnListChanged += (e) => UpdateGenTogglesVisuals();

            // Higher Lower
            LobbyManager.Instance.hlAnswerTime.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlDifficulty.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessHP.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessAT.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessDF.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessSP_AT.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessSP_DF.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessSPD.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessWEI.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGuessHEI.OnValueChanged += (p, c) => UpdateConfigVisuals();
            LobbyManager.Instance.hlGenerationStates.OnListChanged += (e) => UpdateGenTogglesVisuals();

            LobbyManager.Instance.selectedGame.OnValueChanged += (p, c) => {
                ChangeTabMenu((int)c);
                UpdateGenTogglesVisuals();
            };
        }

        if (AppManager.Instance != null)
        {
            AppManager.Instance.NetworkPlayers.OnListChanged += (e) => RefreshPlayerList();
        }
    }

    private void UpdateUIForClientRole()
    {
        if (this == null) return;
        bool isHost = NetworkManager.Singleton.IsServer;

        if (startGameButton) startGameButton.interactable = isHost;

        // Impostor Inputs
        if (turnTimeInput) turnTimeInput.interactable = isHost;
        if (impostorCountInput) impostorCountInput.interactable = isHost;
        if (hintGenToggle) hintGenToggle.interactable = isHost;
        if (hintTypeToggle) hintTypeToggle.interactable = isHost;
        if (hintColorToggle) hintColorToggle.interactable = isHost;

        // HL Inputs
        if (hlRoundTimeInput) hlRoundTimeInput.interactable = isHost;
        if (hlDifficultyDropdown) hlDifficultyDropdown.interactable = isHost;
        if (guessHP) guessHP.interactable = isHost;
        if (guessAT) guessAT.interactable = isHost;
        if (guessDF) guessDF.interactable = isHost;
        if (guessATESP) guessATESP.interactable = isHost;
        if (guessDFESP) guessDFESP.interactable = isHost;
        if (guessVEL) guessVEL.interactable = isHost;
        if (guessHEI) guessHEI.interactable = isHost;
        if (guessWEI) guessWEI.interactable = isHost;

        foreach (var t in genToggles) if (t) t.interactable = isHost;
        foreach (var b in tabButtons) if (b) b.interactable = isHost;
    }

    private void RefreshPlayerList()
    {
        if (playerListContainer == null || this == null) return;
        while (playerListContainer.childCount > 0) DestroyImmediate(playerListContainer.GetChild(0).gameObject);

        int index = 1;
        if (AppManager.Instance == null) return;

        foreach (PlayerData player in AppManager.Instance.NetworkPlayers)
        {
            if (playerCardPrefab == null) continue;
            LobbyPlayerCard card = Instantiate(playerCardPrefab, playerListContainer);
            Texture2D avatar = null;
            if (AppManager.Instance.steamAvatars.ContainsKey(player.SteamId))
                avatar = AppManager.Instance.steamAvatars[player.SteamId];
            card.SetPlayerInfo(player.PlayerName.ToString(), avatar, index, player.ClientId);
            index++;
        }
    }

    private void SendSettingsImpostor()
    {
        if (isUpdatingVisuals || !NetworkManager.Singleton.IsServer) return;

        float.TryParse(turnTimeInput.text, out float time);
        int.TryParse(impostorCountInput.text, out int imps);

        LobbyManager.Instance.UpdateSettingsImpostorServerRpc(
            time > 0 ? time : 30f,
            imps > 0 ? imps : 1,
            hintGenToggle.isOn, hintTypeToggle.isOn, hintColorToggle.isOn
        );
    }

    private void SendSettingsHL()
    {
        if (isUpdatingVisuals || !NetworkManager.Singleton.IsServer) return;

        float.TryParse(hlRoundTimeInput.text, out float time);

        // --- CORRECCIÓN: Usamos la lista explícita para traducir el índice a la Dificultad ---
        Difficulty diff = Difficulty.MediumHard; // Default
        if (hlDifficultyDropdown.value >= 0 && hlDifficultyDropdown.value < orderedDifficulties.Count)
        {
            diff = orderedDifficulties[hlDifficultyDropdown.value];
        }

        LobbyManager.Instance.UpdateSettingsHigherOrLowerServerRpc(
            time > 0 ? time : 30f,
            diff,
            guessHP.isOn, guessAT.isOn, guessDF.isOn,
            guessATESP.isOn, guessDFESP.isOn, guessVEL.isOn,
            guessWEI.isOn, guessHEI.isOn
        );
    }

    private void UpdateConfigVisuals()
    {
        if (LobbyManager.Instance == null || this == null) return;
        isUpdatingVisuals = true;

        // Impostor
        if (turnTimeInput) turnTimeInput.text = LobbyManager.Instance.impostorTurnDuration.Value.ToString();
        if (impostorCountInput) impostorCountInput.text = LobbyManager.Instance.impostorCount.Value.ToString();
        if (hintGenToggle) hintGenToggle.isOn = LobbyManager.Instance.impostorHintGen.Value;
        if (hintTypeToggle) hintTypeToggle.isOn = LobbyManager.Instance.impostorHintType.Value;
        if (hintColorToggle) hintColorToggle.isOn = LobbyManager.Instance.impostorHintColor.Value;

        // Higher Lower
        if (hlRoundTimeInput) hlRoundTimeInput.text = LobbyManager.Instance.hlAnswerTime.Value.ToString();

        // --- CORRECCIÓN: Buscamos el índice en nuestra lista explícita ---
        Difficulty currentDiff = LobbyManager.Instance.hlDifficulty.Value;
        int diffIndex = orderedDifficulties.IndexOf(currentDiff);

        if (hlDifficultyDropdown && diffIndex != -1)
        {
            hlDifficultyDropdown.value = diffIndex;
        }

        if (guessHP) guessHP.isOn = LobbyManager.Instance.hlGuessHP.Value;
        if (guessAT) guessAT.isOn = LobbyManager.Instance.hlGuessAT.Value;
        if (guessDF) guessDF.isOn = LobbyManager.Instance.hlGuessDF.Value;
        if (guessATESP) guessATESP.isOn = LobbyManager.Instance.hlGuessSP_AT.Value;
        if (guessDFESP) guessDFESP.isOn = LobbyManager.Instance.hlGuessSP_DF.Value;
        if (guessVEL) guessVEL.isOn = LobbyManager.Instance.hlGuessSPD.Value;
        if (guessHEI) guessHEI.isOn = LobbyManager.Instance.hlGuessHEI.Value;
        if (guessWEI) guessWEI.isOn = LobbyManager.Instance.hlGuessWEI.Value;

        isUpdatingVisuals = false;
    }

    private void UpdateGenTogglesVisuals()
    {
        if (LobbyManager.Instance == null || this == null) return;
        isUpdatingVisuals = true;

        NetworkList<bool> targetList = (LobbyManager.Instance.selectedGame.Value == GameType.Impostor)
            ? LobbyManager.Instance.impostorGenerationStates
            : LobbyManager.Instance.hlGenerationStates;

        for (int i = 0; i < genToggles.Length && i < targetList.Count; i++)
        {
            if (genToggles[i] != null) genToggles[i].isOn = targetList[i];
        }

        isUpdatingVisuals = false;
    }

    public void ChangeTabMenu(int tabShow)
    {
        for (int i = 0; i < rulePanels.Count; i++)
            rulePanels[i].SetActive(i == tabShow);

        for (int i = 0; i < tabButtons.Count; i++)
            tabButtons[i].interactable = (i != tabShow);

        if (NetworkManager.Singleton.IsServer)
        {
            LobbyManager.Instance.selectedGame.Value = (GameType)tabShow;
        }
    }

    public void OnTabClicked(int index)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            ChangeTabMenu(index);
        }
    }
}