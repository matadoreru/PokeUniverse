using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using static PokemonDatabase;

public class ImpostorGameOverUI : MonoBehaviour
{
    public static ImpostorGameOverUI Instance;

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Components")]
    [SerializeField] private Button replayButton;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private TextMeshProUGUI winnerText;

    [Header("Reveal Info")]
    [SerializeField] private Image secretPokemonImageGameOver;
    [SerializeField] private TextMeshProUGUI secretPokemonNameGameOver;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        replayButton.onClick.AddListener(() => ImpostorPokeManager.Instance.LoadConfigAndStart());
        returnToLobbyButton.onClick.AddListener(() => ImpostorPokeManager.Instance.ReturnToLobby());
    }

    public void Show(string resultMsg, PokemonEntry info, bool isShiny)
    {
        panelRoot.SetActive(true);
        winnerText.text = resultMsg;

        string nameToShow = isShiny ? $"* {info.pokemonName} *" : info.pokemonName;
        Sprite spriteToShow = isShiny ? info.shinySprite : info.pokemonSprite;

        secretPokemonNameGameOver.text = nameToShow;
        secretPokemonNameGameOver.color = isShiny ? Color.yellow : Color.white;
        secretPokemonImageGameOver.sprite = spriteToShow;

        // Solo el Host puede reiniciar
        replayButton.interactable = NetworkManager.Singleton.IsServer;
        returnToLobbyButton.interactable = NetworkManager.Singleton.IsServer;
    }

    public void Hide() => panelRoot.SetActive(false);
}