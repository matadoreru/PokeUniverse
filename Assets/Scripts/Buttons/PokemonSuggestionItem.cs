using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class PokemonSuggestionItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage; // Optional: to show the sprite

    private Action<string> onSelectCallback;
    private string pokemonName;

    private void Start()
    {
        button.onClick.AddListener(OnItemClicked);
    }

    public void Setup(string name, Sprite sprite, Action<string> callback)
    {
        pokemonName = name;
        nameText.text = name;
        onSelectCallback = callback;

        if (iconImage != null && sprite != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }
    }

    private void OnItemClicked()
    {
        onSelectCallback?.Invoke(pokemonName);
    }
}