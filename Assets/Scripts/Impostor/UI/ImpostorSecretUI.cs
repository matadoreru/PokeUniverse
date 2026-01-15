using UnityEngine;
using TMPro;
using UnityEngine.UI;
using static PokemonDatabase;

public class ImpostorSecretUI : MonoBehaviour
{
    public static ImpostorSecretUI Instance;

    [Header("Secret Panel")]
    [SerializeField] private GameObject secretPanel;

    [Header("Elements")]
    [SerializeField] private Image secretPokemonImage;
    [SerializeField] private TextMeshProUGUI secretPokemonName;
    [SerializeField] private TextMeshProUGUI secretPokemonImpostorLabel;
    [SerializeField] private TextMeshProUGUI secretHintsText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void Show() => secretPanel.SetActive(true);
    public void Hide() => secretPanel.SetActive(false);

    public void SetupDisplay(PokemonEntry info, bool isImpostor, bool isShiny, bool hGen, bool hType, bool hColor)
    {
        Show();

        string nameToShow = isShiny ? $"* {info.pokemonName} *" : info.pokemonName;
        Sprite spriteToShow = isShiny ? info.shinySprite : info.pokemonSprite;
        Color textColor = isShiny ? Color.yellow : Color.white;

        if (isImpostor)
        {
            secretPokemonImage.gameObject.SetActive(false);
            secretPokemonName.gameObject.SetActive(false);

            secretPokemonImpostorLabel.gameObject.SetActive(true);
            secretPokemonImpostorLabel.text = "<color=red>¡ERES EL TEAM ROCKET!</color>";

            secretHintsText.gameObject.SetActive(true);
            string hints = "";
            if (hGen) hints += $"Gen.: {info.generation}    ";
            if (hType) hints += $"Tipo: {string.Join("/", info.types)}\n";
            if (hColor) hints += $"Color: {info.mainColor}";

            if (string.IsNullOrEmpty(hints)) hints = "¡Sin pistas!";
            secretHintsText.text = $"PISTAS:\n{hints}";
        }
        else
        {
            secretPokemonImpostorLabel.gameObject.SetActive(false);
            secretHintsText.text = "<color=green>¡ERES ENTRENADOR\nENCUENTRA AL IMPOSTOR!</color>";

            secretPokemonImage.gameObject.SetActive(true);
            secretPokemonImage.sprite = spriteToShow;

            secretPokemonName.gameObject.SetActive(true);
            secretPokemonName.text = nameToShow.ToUpper();
            secretPokemonName.color = textColor;
        }
    }
}