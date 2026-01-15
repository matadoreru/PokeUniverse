using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class VoteButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    public Button button;
    public GameObject voteContainer;

    private ulong targetPlayerId;

    public void Setup(ulong playerId, string name)
    {
        targetPlayerId = playerId;
        playerNameText.text = name;
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        ImpostorPokeManager.Instance.CastVoteServerRpc(targetPlayerId);
        foreach (KeyValuePair<ulong,VoteButton> buttonpair in ImpostorVotingUI.Instance.voteButtonDictionary) 
        {
            buttonpair.Value.button.interactable = false;
        }
    }
}