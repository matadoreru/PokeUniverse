using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

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

    [Header("Rules Panels")]
    [SerializeField] private List<GameObject> rulePanels;


    [Header("Configuration Impostor UI")]
    [SerializeField] private TMP_InputField turnTimeInput;
    [SerializeField] private TMP_InputField impostorCountInput;
    [SerializeField] private Toggle hintGenToggle;
    [SerializeField] private Toggle hintTypeToggle;
    [SerializeField] private Toggle hintColorToggle;
    [SerializeField] private Toggle[] genToggles;

    private bool isUpdatingVisuals = false;

    private void Start()
    {
        if (startGameButton) startGameButton.onClick.AddListener(() => LobbyManager.Instance.StartGame());
        if (leaveButton) leaveButton.onClick.AddListener(() => LobbyManager.Instance.LeaveLobby());

        turnTimeInput.onValueChanged.AddListener(val => SendSettings());
        impostorCountInput.onValueChanged.AddListener(val => SendSettings());
        hintGenToggle.onValueChanged.AddListener(val => SendSettings());
        hintTypeToggle.onValueChanged.AddListener(val => SendSettings());
        hintColorToggle.onValueChanged.AddListener(val => SendSettings());

        for (int i = 0; i < genToggles.Length; i++)
        {
            int index = i;
            genToggles[i].onValueChanged.AddListener((isOn) =>
            {
                if (!isUpdatingVisuals && NetworkManager.Singleton.IsServer)
                    LobbyManager.Instance.SetGenerationStateServerRpc(index, isOn);
            });
        }

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.impostorTurnDuration.OnValueChanged += OnConfigChanged;
            LobbyManager.Instance.impostorCount.OnValueChanged += OnConfigChanged;
            LobbyManager.Instance.impostorHintGen.OnValueChanged += OnConfigChangedBool; // Necesita firma compatible
            LobbyManager.Instance.impostorHintType.OnValueChanged += OnConfigChangedBool;
            LobbyManager.Instance.impostorHintColor.OnValueChanged += OnConfigChangedBool;
            LobbyManager.Instance.impostorGenerationStates.OnListChanged += OnGenListChanged;
        }

        if (AppManager.Instance != null)
        {
            AppManager.Instance.NetworkPlayers.OnListChanged += OnPlayerListChanged;
        }

        // Inicialización
        RefreshPlayerList();
        UpdateConfigVisuals();
        UpdateUIForClientRole();

        if (AppManager.Instance.currentLobby.HasValue)
            roomCodeText.text = AppManager.Instance.currentLobby.Value.GetData("CODE");
    }

    private void OnDestroy()
    {
        if (AppManager.Instance != null && AppManager.Instance.NetworkPlayers != null)
        {
            AppManager.Instance.NetworkPlayers.OnListChanged -= OnPlayerListChanged;
        }


        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.impostorTurnDuration.OnValueChanged -= OnConfigChanged;
            LobbyManager.Instance.impostorCount.OnValueChanged -= OnConfigChanged;
            LobbyManager.Instance.impostorHintGen.OnValueChanged -= OnConfigChangedBool;
            LobbyManager.Instance.impostorHintType.OnValueChanged -= OnConfigChangedBool;
            LobbyManager.Instance.impostorHintColor.OnValueChanged -= OnConfigChangedBool;
            LobbyManager.Instance.impostorGenerationStates.OnListChanged -= OnGenListChanged;
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            UpdateUIForClientRole();
        }
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerData> changeEvent) => RefreshPlayerList();
    private void OnConfigChanged(float prev, float curr) => UpdateConfigVisuals();
    private void OnConfigChanged(int prev, int curr) => UpdateConfigVisuals();
    private void OnConfigChangedBool(bool prev, bool curr) => UpdateConfigVisuals();
    private void OnGenListChanged(NetworkListEvent<bool> changeEvent) => UpdateGenTogglesVisuals();


    private void UpdateUIForClientRole()
    {
        // Seguridad por si se llama cuando se está destruyendo
        if (this == null) return;

        bool isHost = NetworkManager.Singleton.IsServer;

        if (startGameButton) startGameButton.interactable = isHost;
        if (turnTimeInput) turnTimeInput.interactable = isHost;
        if (impostorCountInput) impostorCountInput.interactable = isHost;
        if (hintGenToggle) hintGenToggle.interactable = isHost;
        if (hintTypeToggle) hintTypeToggle.interactable = isHost;
        if (hintColorToggle) hintColorToggle.interactable = isHost;

        foreach (var t in genToggles) if (t) t.interactable = isHost;
    }

    private void RefreshPlayerList()
    {
        if (playerListContainer == null || this == null) return;

        while (playerListContainer.childCount > 0)
        {
            DestroyImmediate(playerListContainer.GetChild(0).gameObject);
        }

        int index = 1;

        if (AppManager.Instance == null) return;

        foreach (PlayerData player in AppManager.Instance.NetworkPlayers)
        {
            if (playerCardPrefab == null) continue;

            LobbyPlayerCard lobbyPlayerCard = Instantiate(playerCardPrefab, playerListContainer);

            Texture2D avatar = null;
            if (AppManager.Instance.steamAvatars.ContainsKey(player.SteamId))
            {
                avatar = AppManager.Instance.steamAvatars[player.SteamId];
            }

            lobbyPlayerCard.SetPlayerInfo(player.PlayerName.ToString(), avatar, index, player.ClientId);
            index++;
        }
    }

    private void SendSettings()
    {
        if (isUpdatingVisuals) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (LobbyManager.Instance == null) return;

        if (!float.TryParse(turnTimeInput.text, out float time)) time = 30f;
        if (!int.TryParse(impostorCountInput.text, out int imps)) imps = 1;

        LobbyManager.Instance.UpdateSettingsImpostorServerRpc(
            time, imps, hintGenToggle.isOn, hintTypeToggle.isOn, hintColorToggle.isOn
        );
    }

    private void UpdateConfigVisuals()
    {
        if (LobbyManager.Instance == null || this == null) return;
        isUpdatingVisuals = true;

        if (turnTimeInput) turnTimeInput.text = LobbyManager.Instance.impostorTurnDuration.Value.ToString();
        if (impostorCountInput) impostorCountInput.text = LobbyManager.Instance.impostorCount.Value.ToString();
        if (hintGenToggle) hintGenToggle.isOn = LobbyManager.Instance.impostorHintGen.Value;
        if (hintTypeToggle) hintTypeToggle.isOn = LobbyManager.Instance.impostorHintType.Value;
        if (hintColorToggle) hintColorToggle.isOn = LobbyManager.Instance.impostorHintColor.Value;

        isUpdatingVisuals = false;
    }

    private void UpdateGenTogglesVisuals()
    {
        if (LobbyManager.Instance == null || this == null) return;
        isUpdatingVisuals = true;

        var list = LobbyManager.Instance.impostorGenerationStates;
        for (int i = 0; i < genToggles.Length && i < list.Count; i++)
        {
            if (genToggles[i] != null) genToggles[i].isOn = list[i];
        }

        isUpdatingVisuals = false;
    }

    public void ChangeTabMenu(int tabShow) {
        int index = 0;
        foreach (GameObject rulePanel in rulePanels) {
            if (index == tabShow)
            {
                rulePanel.SetActive(true);
            }
            else {
                rulePanel.SetActive(false);
            }
            index++;
        }

        index = 0;

        foreach (Button tabButton in tabButtons) {
            if (index == tabShow)
            {
                tabButton.interactable = false;
            }
            else
            {
                tabButton.interactable = true;
            }
            index++;
        }

        LobbyManager.Instance.selectedGame.Value = (GameType)tabShow;
    }


}