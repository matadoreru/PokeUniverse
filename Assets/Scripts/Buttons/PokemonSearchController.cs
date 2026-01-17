using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq; // Important for sorting efficiently

public class PokemonSearchController : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private PokemonDatabase pokemonDB;
    // If you want to load from Resources automatically:
    // private const string DB_RESOURCE_PATH = "PokemonDatabase"; 

    [Header("UI Components")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Transform resultsContainer;
    [SerializeField] private GameObject suggestionPrefab;
    [SerializeField] private GameObject resultsPanel; // To hide/show the list

    [Header("Settings")]
    [SerializeField] private int maxResults = 5;

    private void Start()
    {
        // Optional: Load from Resources if not assigned
        if (pokemonDB == null)
        {
            pokemonDB = Resources.Load<PokemonDatabase>("PokemonDatabase");
        }

        // Subscribe to the input field event
        searchInput.onValueChanged.AddListener(OnSearchInputValueChanged);

        // Hide results initially
        resultsPanel.SetActive(false);
    }

    private void OnSearchInputValueChanged(string searchText)
    {
        // 1. Clean previous results
        foreach (Transform child in resultsContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Validation: If text is empty, hide panel and return
        if (string.IsNullOrWhiteSpace(searchText))
        {
            resultsPanel.SetActive(false);
            return;
        }

        resultsPanel.SetActive(true);
        string lowerSearch = searchText.ToLower();

        // 3. Search Logic with LINQ
        // We filter by name, then sort to prioritize "StartsWith", then take top 5
        var matches = pokemonDB.allPokemon
            .Where(p => p.pokemonName.ToLower().Contains(lowerSearch))
            .OrderByDescending(p => p.pokemonName.ToLower().StartsWith(lowerSearch)) // StartsWith = True (1) first
            .ThenBy(p => p.pokemonName) // Then alphabetical
            .Take(maxResults);

        // 4. Instantiate results
        foreach (var pokemon in matches)
        {
            GameObject itemObj = Instantiate(suggestionPrefab, resultsContainer);
            PokemonSuggestionItem itemScript = itemObj.GetComponent<PokemonSuggestionItem>();

            // Pass the data and the method to call when clicked
            itemScript.Setup(pokemon.pokemonName, pokemon.pokemonSprite, OnPokemonSelected);
        }

        // Hide panel if no matches found
        if (!matches.Any())
        {
            resultsPanel.SetActive(false);
        }
    }

    private void OnPokemonSelected(string selectedName)
    {
        // Fill the input with the selected name
        searchInput.text = selectedName;

        // Hide the results
        resultsPanel.SetActive(false);

        // Logic to actually load/show the pokemon data in your game
        Debug.Log($"User selected: {selectedName}");
    }
}