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
    [SerializeField] private GameObject inputArea;
    [SerializeField] private TMP_InputField searchInput;

    [Header("Autocomplete Suggestions")]
    [SerializeField] private Transform suggestionsContainer;
    [SerializeField] private GameObject suggestionPrefab;
    [SerializeField] private GameObject suggestionsPanelParent;

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
            UpdateDuelists(0, 0); // Inicializar visuales
        }

        searchInput.onValueChanged.AddListener(OnSearchInputChanged);

        lobbyButton.onClick.AddListener(() => SayOnePokeManager.Instance.ReturnToLobby());
        replayButton.onClick.AddListener(() => SayOnePokeManager.Instance.ReplayGame());

        duelInfoPanel.SetActive(true);
        resultPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        inputArea.SetActive(false);
        suggestionsPanelParent.SetActive(false);
    }

    private void Update()
    {
        if (SayOnePokeManager.Instance == null) return;

        // Mostrar Timer con 1 decimal
        float time = SayOnePokeManager.Instance.timer.Value;
        timerText.text = time.ToString("F1");

        // Si estamos en cuenta atrás (Timer < 3 pero Estado Countdown), pintar el timer de rojo
        if (SayOnePokeManager.Instance.currentState.Value == SayOnePokeManager.DuelState.Countdown)
            timerText.color = Color.red;
        else
            timerText.color = Color.white;

        CheckIfActivePlayer();
    }

    private void OnSearchInputChanged(string text)
    {
        // 1. Limpiar
        foreach (Transform child in suggestionsContainer) Destroy(child.gameObject);

        if (string.IsNullOrEmpty(text))
        {
            suggestionsPanelParent.SetActive(false);
            return;
        }

        // 2. Buscar
        PokemonDatabase db = SayOnePokeManager.Instance.GetDatabase();
        if (db == null) return;

        int matchesFound = 0;
        string searchLower = text.ToLower();

        var allowedGensList = SayOnePokeManager.Instance.allowedGensNetList;

        for (int i = 0; i < db.allPokemon.Count; i++)
        {
            PokemonEntry poke = db.allPokemon[i];

            // Filtro Generación: Si la lista tiene elementos, verificar. Si está vacía, permitir todo (seguridad).
            if (allowedGensList != null && allowedGensList.Count > 0)
            {
                if (!allowedGensList.Contains(poke.generation)) continue;
            }

            // Filtro Texto (StartsWith = IntelliSense al inicio)
            if (poke.pokemonName.ToLower().StartsWith(searchLower))
            {
                CreateSuggestion(poke, i);
                matchesFound++;
            }

            if (matchesFound >= 5) break; // Máximo 5 sugerencias
        }

        suggestionsPanelParent.SetActive(matchesFound > 0);
    }

    private void CreateSuggestion(PokemonEntry poke, int index)
    {
        GameObject btnObj = Instantiate(suggestionPrefab, suggestionsContainer);
        SuggestionButton script = btnObj.GetComponent<SuggestionButton>();
        if (script != null) script.Setup(poke, index);
    }

    public void SubmitAnswer(int index)
    {
        SayOnePokeManager.Instance.SubmitPokemonServerRpc(index);
        searchInput.text = "";
        suggestionsPanelParent.SetActive(false);
        inputArea.SetActive(false);
    }

    private void UpdateDuelists(ulong prev, ulong curr)
    {
        if (SayOnePokeManager.Instance == null) return;

        ulong id1 = SayOnePokeManager.Instance.player1Id.Value;
        ulong id2 = SayOnePokeManager.Instance.player2Id.Value;

        // Resetear visuales
        p1Avatar.texture = null;
        p1Avatar.color = Color.gray; // Gris si no hay imagen
        p2Avatar.texture = null;
        p2Avatar.color = Color.gray;

        string n1 = "Esperando...", n2 = "Esperando...";

        if (id1 != ulong.MaxValue)
        {
            var d1 = AppManager.Instance.GetPlayerData(id1);
            if (d1.HasValue)
            {
                n1 = d1.Value.PlayerName.ToString();
                if (AppManager.Instance.steamAvatars.ContainsKey(d1.Value.SteamId))
                {
                    p1Avatar.texture = AppManager.Instance.steamAvatars[d1.Value.SteamId];
                    p1Avatar.color = Color.white;
                }
            }
        }

        if (id2 != ulong.MaxValue)
        {
            var d2 = AppManager.Instance.GetPlayerData(id2);
            if (d2.HasValue)
            {
                n2 = d2.Value.PlayerName.ToString();
                if (AppManager.Instance.steamAvatars.ContainsKey(d2.Value.SteamId))
                {
                    p2Avatar.texture = AppManager.Instance.steamAvatars[d2.Value.SteamId];
                    p2Avatar.color = Color.white;
                }
            }
        }

        vsText.text = $"{n1}  VS  {n2}";
    }

    private void CheckIfActivePlayer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null) return;

        ulong myId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        ulong p1 = SayOnePokeManager.Instance.player1Id.Value;
        ulong p2 = SayOnePokeManager.Instance.player2Id.Value;

        var state = SayOnePokeManager.Instance.currentState.Value;

        // --- LÓGICA IMPORTANTE DEL BLOQUEO ---
        // El input solo se activa si:
        // 1. Soy uno de los jugadores (p1 o p2)
        // 2. El estado es ACTIVE (ha pasado la cuenta atrás)
        // 3. No estamos viendo la pantalla de resultados
        bool isDuelActive = (state == SayOnePokeManager.DuelState.Active);
        bool amIDueling = (myId == p1 || myId == p2);

        bool showInput = amIDueling && isDuelActive && !resultPanel.activeSelf;

        if (showInput != inputArea.activeSelf)
        {
            inputArea.SetActive(showInput);
            if (showInput)
            {
                // Al activarse (justo al acabar la cuenta atrás), dar foco
                searchInput.text = "";
                searchInput.Select();
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

        bool isServer = Unity.Netcode.NetworkManager.Singleton.IsServer;
        lobbyButton.interactable = isServer;
        replayButton.interactable = isServer;

        duelInfoPanel.SetActive(false);
        resultPanel.SetActive(false);
        inputArea.SetActive(false);
    }
}