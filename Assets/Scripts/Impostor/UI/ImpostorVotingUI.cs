using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Netcode;

public class ImpostorVotingUI : MonoBehaviour
{
    public static ImpostorVotingUI Instance;
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Container")]
    [SerializeField] private Transform voteButtonContainer;
    [SerializeField] private VoteButton voteButtonPrefab;
    [SerializeField] private GameObject voteAvatarPrefab;

    public Dictionary<ulong, VoteButton> voteButtonDictionary = new Dictionary<ulong, VoteButton>();

    private HashSet<ulong> playersWhoHaveVoted = new HashSet<ulong>();

    public void Awake()
    {
        if (Instance == null) Instance = this;
    }
    public void Show() => panelRoot.SetActive(true);
    public void Hide() => panelRoot.SetActive(false);

    public void SetupButtons(NetworkList<PlayerData> players, ulong myClientId)
    {
        foreach (Transform child in voteButtonContainer) Destroy(child.gameObject);
        voteButtonDictionary.Clear();

        playersWhoHaveVoted.Clear();

        foreach (var player in players)
        {
            VoteButton btn = Instantiate(voteButtonPrefab, voteButtonContainer);
            btn.Setup(player.ClientId, player.PlayerName.ToString());

            btn.button.onClick.AddListener(() => ImpostorPokeManager.Instance.CastVoteServerRpc(player.ClientId));

            btn.button.interactable = player.ClientId != myClientId;

            voteButtonDictionary.Add(player.ClientId, btn);
        }
    }

    public void AddVoteIndicator(ulong suspectId, ulong voterId)
    {
        if (playersWhoHaveVoted.Contains(voterId)) return;

        playersWhoHaveVoted.Add(voterId);

        Texture2D avatarTex = null;
        if (AppManager.Instance != null)
        {
            var voterData = AppManager.Instance.GetPlayerData(voterId);
            if (voterData.HasValue && AppManager.Instance.steamAvatars.ContainsKey(voterData.Value.SteamId))
            {
                avatarTex = AppManager.Instance.steamAvatars[voterData.Value.SteamId];
            }
        }

        if (voteButtonDictionary.TryGetValue(suspectId, out VoteButton targetBtn))
        {
            if (targetBtn.voteContainer != null)
            {
                GameObject avatarObj = Instantiate(voteAvatarPrefab, targetBtn.voteContainer.transform);
                if (avatarTex != null)
                {
                    var rawImg = avatarObj.GetComponent<RawImage>();
                    if (rawImg != null) rawImg.texture = avatarTex;
                }
            }
        }
    }
}