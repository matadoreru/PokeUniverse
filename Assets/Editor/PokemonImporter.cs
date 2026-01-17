using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

#if UNITY_EDITOR
[CustomEditor(typeof(PokemonDatabase))]
public class PokemonImporter : Editor
{
    // --- JSON CLASSES ---
    [System.Serializable]
    private class PokemonJson
    {
        public string name;
        public int id;
        public int height; // Decimetres
        public int weight; // Hectograms
        public SpriteList sprites;
        public TypeEntry[] types;
        public StatEntry[] stats; // NEW
        public SpeciesRef species;
    }

    [System.Serializable]
    private class SpriteList
    {
        public string front_default;
        public string front_shiny;
    }

    [System.Serializable] private class TypeEntry { public TypeInfo type; }
    [System.Serializable] private class TypeInfo { public string name; }

    // Stats JSON structure
    [System.Serializable] private class StatEntry { public int base_stat; public StatInfo stat; }
    [System.Serializable] private class StatInfo { public string name; }

    [System.Serializable] private class SpeciesRef { public string url; }
    [System.Serializable] private class SpeciesJson { public ColorInfo color; }
    [System.Serializable] private class ColorInfo { public string name; }

    private int startId = 1;
    private int endId = 151;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PokemonDatabase db = (PokemonDatabase)target;
        GUILayout.Space(20);
        GUILayout.Label("?? Importador Pro (Stats + Shiny + Color)", EditorStyles.boldLabel);

        startId = EditorGUILayout.IntField("Start ID", startId);
        endId = EditorGUILayout.IntField("End ID", endId);

        if (GUILayout.Button("?? Download Full Data")) DownloadPokemonData(db);
        if (GUILayout.Button("??? Clear Database")) { db.allPokemon.Clear(); EditorUtility.SetDirty(db); }
    }

    private async void DownloadPokemonData(PokemonDatabase db)
    {
        string folderPath = "Assets/Resources/PokemonIcons";
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        if (db.allPokemon == null) db.allPokemon = new List<PokemonDatabase.PokemonEntry>();

        // Avoid duplicates if appending
        // db.allPokemon.Clear(); 

        for (int i = startId; i <= endId; i++)
        {
            float progress = (float)(i - startId) / (endId - startId + 1);
            if (EditorUtility.DisplayCancelableProgressBar("Importing...", $"Fetching Pokemon #{i}", progress)) break;

            // 1. Get Main Data
            string url = $"https://pokeapi.co/api/v2/pokemon/{i}";
            var data = await FetchJson<PokemonJson>(url);
            if (data == null) continue;

            // 2. Get Color Data
            string colorName = "Unknown";
            if (!string.IsNullOrEmpty(data.species.url))
            {
                var speciesData = await FetchJson<SpeciesJson>(data.species.url);
                if (speciesData != null) colorName = Capitalize(speciesData.color.name);
            }

            // 3. Download Sprites
            Sprite normalSprite = await GetSpriteTexture(data.sprites.front_default, $"{data.name}", folderPath);
            Sprite shinySprite = await GetSpriteTexture(data.sprites.front_shiny, $"{data.name}_shiny", folderPath);

            // 4. Process Types
            List<string> typeList = new List<string>();
            foreach (var t in data.types) typeList.Add(Capitalize(t.type.name));

            // 5. Process Stats (Order: HP, ATK, DEF, SP_ATK, SP_DEF, SPD)
            // API order matches usually, but safer to check names if robust. 
            // For simplicity, API order is consistently: hp, attack, defense, special-attack, special-defense, speed.
            int[] baseStats = new int[6];
            if (data.stats != null)
            {
                for (int s = 0; s < data.stats.Length && s < 6; s++)
                {
                    baseStats[s] = data.stats[s].base_stat;
                }
            }

            // 6. Build Entry
            PokemonDatabase.PokemonEntry entry = new PokemonDatabase.PokemonEntry
            {
                id = data.id,
                pokemonName = Capitalize(data.name),
                pokemonSprite = normalSprite,
                shinySprite = shinySprite,
                types = typeList.ToArray(),
                mainColor = colorName,
                heightMeters = data.height / 10f, // API uses decimetres
                weightKg = data.weight / 10f,     // API uses hectograms
                generation = CalculateGeneration(data.id),
                stats = baseStats
            };

            // Remove existing if updating
            db.allPokemon.RemoveAll(x => x.id == entry.id);
            db.allPokemon.Add(entry);
        }

        EditorUtility.ClearProgressBar();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("? Database Updated Successfully!");
    }

    private async Task<T> FetchJson<T>(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            var op = request.SendWebRequest();
            while (!op.isDone) await Task.Delay(10);
            if (request.result != UnityWebRequest.Result.Success) return default;
            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
    }

    private async Task<Sprite> GetSpriteTexture(string url, string name, string folderPath)
    {
        if (string.IsNullOrEmpty(url)) return null;

        // Check if file exists to skip download (Caching)
        string fullPath = $"{folderPath}/{name}.png";
        if (File.Exists(fullPath))
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        }

        using (UnityWebRequest imgRequest = UnityWebRequestTexture.GetTexture(url))
        {
            var op = imgRequest.SendWebRequest();
            while (!op.isDone) await Task.Delay(10);
            if (imgRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(imgRequest);
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(fullPath, bytes);

                // Import settings
                AssetDatabase.ImportAsset(fullPath); // Force import first
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(fullPath);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.filterMode = FilterMode.Point; // Pixel art style
                    importer.spritePixelsPerUnit = 32; // Adjust if sprites look too small/big
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            }
        }
        return null;
    }

    private string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    private int CalculateGeneration(int id)
    {
        if (id <= 151) return 1; if (id <= 251) return 2; if (id <= 386) return 3;
        if (id <= 493) return 4; if (id <= 649) return 5; if (id <= 721) return 6;
        if (id <= 809) return 7; if (id <= 905) return 8; return 9;
    }
}
#endif