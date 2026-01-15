using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerCard : MonoBehaviour
{
    [Header("UI References")]
    public RawImage avatarImage;
    public TMP_Text steamNameText;
    public TMP_Text playerNumberText;
    public ulong playerId;

    public LobbyPlayerCard SetPlayerInfo(string name, Texture2D avatar, int index, ulong playerId)
    {
        this.playerId = playerId;
        steamNameText.text = name;
        playerNumberText.text = $"Player {index}";

        if (avatar != null)
        {
            avatarImage.texture = avatar;
        }
        else
        {
            avatarImage.color = Color.gray;
        }

        return this;
    }
}