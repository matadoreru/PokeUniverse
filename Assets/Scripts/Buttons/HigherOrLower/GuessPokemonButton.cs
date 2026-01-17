using UnityEngine;
using TMPro;
using UnityEngine.UI;
using static PokemonDatabase;
using System.Collections.Generic;

public class GuessPokemonButton : MonoBehaviour
{
    [SerializeField] public Image iconImage;
    [SerializeField] public TextMeshProUGUI nameText;
    [SerializeField] public Button button;
    [SerializeField] public GameObject voteContainer;

    // Runtime state
    public PokemonEntry pokemon;
    public bool isCorrect;
    public HashSet<ulong> playersWhoHaveVoted = new HashSet<ulong>();
    private int myIndex;

    // Remove the constructor! MonoBehaviours cannot use constructors like this.

    public void Setup(PokemonEntry pokemon, int index, bool isCorrect)
    {
        this.pokemon = pokemon;
        this.myIndex = index;
        this.isCorrect = isCorrect;

        if (nameText != null) nameText.text = pokemon.pokemonName;
        if (iconImage != null) iconImage.sprite = pokemon.pokemonSprite;

        playersWhoHaveVoted.Clear();
        // Clear previous vote avatars if any
        foreach (Transform child in voteContainer.transform) Destroy(child.gameObject);

        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        HigherOrLowerManager.Instance.CastVoteServerRpc(myIndex);
        button.interactable = false;
    }
}