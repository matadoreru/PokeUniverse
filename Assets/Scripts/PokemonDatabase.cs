using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PokemonDatabase", menuName = "Game/Pokemon Database")]
public class PokemonDatabase : ScriptableObject
{
    [System.Serializable]
    public struct PokemonEntry
    {
        public int id;
        public string pokemonName;
        public Sprite pokemonSprite;
        public Sprite shinySprite; 

        [Tooltip("Primary and Secondary types")]
        public string[] types;

        public string mainColor; 
        public int generation;
        public float heightMeters;
        public float weightKg;
    }

    public List<PokemonEntry> allPokemon;

    public PokemonEntry GetRandomPokemonFiltered(List<int> allowedGenerations, out int originalIndex)
    {
        List<int> validIndices = new List<int>();
        for (int i = 0; i < allPokemon.Count; i++)
        {
            if (allowedGenerations.Contains(allPokemon[i].generation)) validIndices.Add(i);
        }

        if (validIndices.Count == 0)
        {
            originalIndex = Random.Range(0, allPokemon.Count);
            return allPokemon[originalIndex];
        }

        int randomPointer = Random.Range(0, validIndices.Count);
        originalIndex = validIndices[randomPointer];
        return allPokemon[originalIndex];
    }
}