using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static PokemonDatabase;

public class SuggestionButton : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;

    private int pokemonDatabaseIndex;

    public void Setup(PokemonEntry pokemon, int index)
    {
        this.pokemonDatabaseIndex = index;

        nameText.text = pokemon.pokemonName;
        iconImage.sprite = pokemon.pokemonSprite;

        // Al hacer clic, enviamos la respuesta
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        // Enviamos el índice al servidor a través de la UI
        SayOnePokeUI.Instance.SubmitAnswer(pokemonDatabaseIndex);
    }
}