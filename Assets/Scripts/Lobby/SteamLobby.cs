using UnityEngine;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using Netcode.Transports.Facepunch;
using System.Threading.Tasks;
using System.Linq; 
using TMPro;
using UnityEngine.UI;

public class SteamLobby : MonoBehaviour
{
    [Header("UI References (Main Menu)")]
    [SerializeField] private TMP_InputField roomCodeInputUI;
    [SerializeField] private Button hostBUtton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject waitPanel;

    public Lobby? currentLobby;

    private void Start()
    {
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToNetcode;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToNetcode;
        }
    }

    private void OnClientConnectedToNetcode(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            AppManager.Instance.RegisterPlayerServerRpc(
                clientId, 
                SteamClient.SteamId.Value, 
                SteamClient.Name
            );
        }
    }

    public async void HostLobby()
    {
        hostBUtton.interactable = false;
        joinButton.interactable = false;
        waitPanel.SetActive(true);

        var lobbyOutput = await SteamMatchmaking.CreateLobbyAsync(8);
        if (!lobbyOutput.HasValue) 
        {
            Debug.LogError("Error creating lobby.");
            hostBUtton.interactable = true;
            joinButton.interactable = true;
            waitPanel.SetActive(false);
            return;
        }

        currentLobby = lobbyOutput.Value;
        currentLobby?.SetPublic();
        currentLobby?.SetJoinable(true);
        currentLobby?.SetData("HostAddress", SteamClient.SteamId.ToString());

        string roomCode = GetRandomCodeRoom(5);
        Debug.Log($"Lobby created wit code. {roomCode}");
        currentLobby?.SetData("CODE", roomCode);
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result == Result.OK)
        {
            lobby.SetData("HostAddress", SteamClient.SteamId.ToString());
            NetworkManager.Singleton.StartHost();

            AppManager.Instance.LoadScene("Lobby");
        }
        else {
            hostBUtton.interactable = true;
            joinButton.interactable = true;
            waitPanel.SetActive(false);
        }
    }

    public async void JoinLobby()
    {
        hostBUtton.interactable = false;
        joinButton.interactable = false;
        waitPanel.SetActive(true);

        Debug.Log($"Searching for lobby: {roomCodeInputUI.text}");
        var list = await SteamMatchmaking.LobbyList
            .WithKeyValue("CODE", roomCodeInputUI.text)
            .RequestAsync();

        if (list != null && list.Length > 0)
        {
            Debug.Log("Lobby Found. Joining...");
            await list[0].Join();
        }
        else
        {
            Debug.LogWarning("No lobby found.");
            hostBUtton.interactable = true;
            joinButton.interactable = true;
            waitPanel.SetActive(false);
        }
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        currentLobby = lobby;
        AppManager.Instance.currentLobby = lobby;

        foreach (var member in lobby.Members)
        {
             ProcessAvatar(member.Id);
        }

        if (NetworkManager.Singleton.IsHost) return;

        string hostAddress = lobby.GetData("HostAddress");
        NetworkManager.Singleton.GetComponent<FacepunchTransport>().targetSteamId = ulong.Parse(hostAddress);
        NetworkManager.Singleton.StartClient();
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        ProcessAvatar(friend.Id);
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        // (Opcional) No borramos de caché por si vuelve a entrar rápido
    }
    
    private async void ProcessAvatar(SteamId steamId)
    {
        Texture2D avatar = await GetAvatar(steamId);
        if (avatar != null)
        {
            AppManager.Instance.AddAvatarToCache(steamId.Value, avatar);
        }
    }

    private async Task<Texture2D> GetAvatar(SteamId steamId)
    {
        var image = await SteamFriends.GetLargeAvatarAsync(steamId);
        if (!image.HasValue) return null;
        Texture2D texture = new Texture2D((int)image.Value.Width, (int)image.Value.Height, TextureFormat.RGBA32, false);
        texture.LoadRawTextureData(image.Value.Data);
        texture.Apply();
        return FlipTexture(texture);
    }

    private Texture2D FlipTexture(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D flipped = new Texture2D(width, height);
        Color32[] originalPixels = original.GetPixels32();
        Color32[] flippedPixels = new Color32[originalPixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flippedPixels[x + (height - y - 1) * width] = originalPixels[x + y * width];
            }
        }
        flipped.SetPixels32(flippedPixels);
        flipped.Apply();
        return flipped;
    }

    public static string GetRandomCodeRoom(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new System.Random();
        return new string(System.Linq.Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}