using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using static PokemonDatabase;

public class SayOnePokeUI : MonoBehaviour
{
    public static SayOnePokeUI Instance;

    [Header("Main Panels")]
    [SerializeField] private GameObject duelInfoPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Input Area")]
    [SerializeField] private GameObject inputArea; // El panel inferior donde escribes
    [SerializeField] private TMP_InputField searchInput;

    [Header("Autocomplete Suggestions")]
    [SerializeField] private Transform suggestionsContainer; // El Content del ScrollView o Panel vertical
    [SerializeField] private GameObject suggestionPrefab; // Prefab con el script SuggestionButton
    [SerializeField] private GameObject suggestionsPanelParent; // Para ocultar la lista si no escribes nada

    [Header("Duel Info Visuals")]
    [SerializeField] private TextMeshProUGUI challengeText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI vsText;
    [SerializeField] private RawImage p1Avatar;
    [SerializeField] private RawImage p2Avatar;

    [Header("Game Over")]
    [SerializeField] private TextMeshProUGUI rankingText;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private Button replayButton;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        if (SayOnePokeManager.Instance != null)
        {
            SayOnePokeManager.Instance.player1Id.OnValueChanged += UpdateDuelists;
            SayOnePokeManager.Instance.player2Id.OnValueChanged += UpdateDuelists;
        }

        // Listener para cada letra que escribas
        searchInput.onValueChanged.AddListener(OnSearchInputChanged);

        lobbyButton.onClick.AddListener(() => SayOnePokeManager.Instance.ReturnToLobby());
        replayButton.onClick.AddListener(() => SayOnePokeManager.Instance.ReplayGame());

        // Estado inicial
        duelInfoPanel.SetActive(true);
        resultPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        inputArea.SetActive(false); // Se activa solo si es tu turno
        suggestionsPanelParent.SetActive(false); // Oculto al inicio
    }

    private void Update()
    {
        if (SayOnePokeManager.Instance == null) return;

        // Timer Visual
        timerText.text = SayOnePokeManager.Instance.timer.Value.ToString("F1");

        // Controlar visibilidad del Input
        CheckIfActivePlayer();
    }

    // --- LÓGICA DE AUTOCOMPLETADO ---

    private void OnSearchInputChanged(string text)
    {
        // 1. Limpiar sugerencias anteriores
        foreach (Transform child in suggestionsContainer) Destroy(child.gameObject);

        // Si está vacío o es muy corto, ocultar lista
        if (string.IsNullOrEmpty(text))
        {
            suggestionsPanelParent.SetActive(false);
            return;
        }

        suggestionsPanelParent.SetActive(true);

        // 2. Buscar coincidencias
        PokemonDatabase db = SayOnePokeManager.Instance.GetDatabase();
        var allowedGens = SayOnePokeManager.Instance.config.allowedGens;

        int matchesFound = 0;
        string searchLower = text.ToLower();

        for (int i = 0; i < db.allPokemon.Count; i++)
        {
            PokemonEntry poke = db.allPokemon[i];

            // A) Filtro de Generación (Lobby)
            if (!allowedGens.Contains(poke.generation)) continue;

            // B) Filtro de Texto (Empieza por...)
            if (poke.pokemonName.ToLower().StartsWith(searchLower))
            {
                CreateSuggestion(poke, i);
                matchesFound++;
            }

            // C) Límite de 6 resultados
            if (matchesFound >= 6) break;
        }

        // Si no hay coincidencias, ocultar el panel para que no moleste
        if (matchesFound == 0) suggestionsPanelParent.SetActive(false);
    }

    private void CreateSuggestion(PokemonEntry poke, int index)
    {
        GameObject btnObj = Instantiate(suggestionPrefab, suggestionsContainer);
        SuggestionButton script = btnObj.GetComponent<SuggestionButton>();
        script.Setup(poke, index);
    }

    // Llamado desde SuggestionButton
    public void SubmitAnswer(int index)
    {
        SayOnePokeManager.Instance.SubmitPokemonServerRpc(index);

        // Limpiar UI tras enviar
        searchInput.text = "";
        suggestionsPanelParent.SetActive(false);
        inputArea.SetActive(false); // Feedback visual inmediato de que ya enviaste
    }

    // --- VISUALIZACIÓN ---

    private void UpdateDuelists(ulong prev, ulong curr)
    {
        ulong id1 = SayOnePokeManager.Instance.player1Id.Value;
        ulong id2 = SayOnePokeManager.Instance.player2Id.Value;

        string n1 = "...", n2 = "...";
        var d1 = AppManager.Instance.GetPlayerData(id1);
        var d2 = AppManager.Instance.GetPlayerData(id2);

        if (d1.HasValue)
        {
            n1 = d1.Value.PlayerName.ToString();
            if (AppManager.Instance.steamAvatars.ContainsKey(d1.Value.SteamId))
                p1Avatar.texture = AppManager.Instance.steamAvatars[d1.Value.SteamId];
        }
        if (d2.HasValue)
        {
            n2 = d2.Value.PlayerName.ToString();
            if (AppManager.Instance.steamAvatars.ContainsKey(d2.Value.SteamId))
                p2Avatar.texture = AppManager.Instance.steamAvatars[d2.Value.SteamId];
        }

        vsText.text = $"{n1}  VS  {n2}";
    }

    private void CheckIfActivePlayer()
    {
        ulong myId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        ulong p1 = SayOnePokeManager.Instance.player1Id.Value;
        ulong p2 = SayOnePokeManager.Instance.player2Id.Value;

        var state = SayOnePokeManager.Instance.currentState.Value;
        bool isDuelActive = (state == SayOnePokeManager.DuelState.Active);
        bool amIDueling = (myId == p1 || myId == p2);

        // Mostrar Input Area SOLO si soy yo y es el momento activo
        bool showInput = amIDueling && isDuelActive && !resultPanel.activeSelf;

        if (showInput != inputArea.activeSelf)
        {
            inputArea.SetActive(showInput);
            if (showInput)
            {
                searchInput.text = ""; // Resetear texto al aparecer
                searchInput.Select();  // Auto-focus para escribir rápido
                searchInput.ActivateInputField();
            }
        }
    }

    public void UpdateChallengeText(string txt)
    {
        challengeText.text = txt;
        resultPanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    public void ShowRoundResult(string msg)
    {
        resultPanel.SetActive(true);
        resultPanel.GetComponentInChildren<TextMeshProUGUI>().text = msg;
        inputArea.SetActive(false);
        suggestionsPanelParent.SetActive(false);
    }

    public void ShowGameOver(string rank)
    {
        gameOverPanel.SetActive(true);
        rankingText.text = rank;
        lobbyButton.interactable = Unity.Netcode.NetworkManager.Singleton.IsServer;
        replayButton.interactable = Unity.Netcode.NetworkManager.Singleton.IsServer;
    }
}