using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class ImpostorHUDUI : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Turn UI")]
    [SerializeField] private TMP_InputField wordInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private TextMeshProUGUI turnOrderLabel;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Info Elements")]
    [SerializeField] private TextMeshProUGUI totalRoundText;

    [Header("Log System")]
    [SerializeField] private GameObject logPanel;
    [SerializeField] private TextMeshProUGUI historyLogText;

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendClicked);
        wordInput.onSubmit.AddListener((val) => OnSendClicked());
    }

    public void Show() => panelRoot.SetActive(true);
    public void Hide() => panelRoot.SetActive(false);

    public void UpdateTimer(float currentTime, float maxTime)
    {
        if (timerSlider != null)
        {
            float normalizedTime = Mathf.Clamp01(currentTime / maxTime);
            timerSlider.value = normalizedTime;

            if (timerFillImage != null)
            {
                if (normalizedTime > 0.5f)
                    timerFillImage.color = Color.Lerp(Color.yellow, Color.green, (normalizedTime - 0.5f) * 2);
                else
                    timerFillImage.color = Color.Lerp(Color.red, Color.yellow, normalizedTime * 2);
            }
        }
    }

    public void UpdateTurnStatus(bool isMyTurn, string activePlayerName)
    {
        wordInput.text = "";

        if (isMyTurn)
        {
            wordInput.interactable = true;
            sendButton.interactable = true;
            wordInput.Select();
            statusText.text = "Es tu turno, escribe una pista:";
        }
        else
        {
            wordInput.interactable = false;
            sendButton.interactable = false;
            statusText.text = $"Esperando a {activePlayerName}...";
        }
    }

    public void UpdateTurnOrderText(List<ulong> turnOrderIds, ulong activePlayerId)
    {
        if (turnOrderIds == null || turnOrderIds.Count == 0)
        {
            turnOrderLabel.text = "";
            return;
        }

        string finalText = "";
        for (int i = 0; i < turnOrderIds.Count; i++)
        {
            ulong playerId = turnOrderIds[i];
            string pName = "Unknown";

            // Acceso a AppManager para nombres
            PlayerData? pData = AppManager.Instance.GetPlayerData(playerId);
            if (pData.HasValue) pName = pData.Value.PlayerName.ToString();

            if (playerId == activePlayerId)
                finalText += $"<color=green><b>{pName}</b></color>";
            else
                finalText += pName;

            if (i < turnOrderIds.Count - 1)
                finalText += " -> ";
        }
        turnOrderLabel.text = finalText;
    }

    public void AddToLog(string msg, int currentRound, int totalRounds)
    {
        historyLogText.text += msg + "\n";
        totalRoundText.text = $"Ronda {currentRound} / {totalRounds}";
    }

    public void ClearLog()
    {
        historyLogText.text = "";
    }

    private void OnSendClicked()
    {
        if (!string.IsNullOrEmpty(wordInput.text))
        {
            ImpostorPokeManager.Instance.SubmitWordServerRpc(wordInput.text);
        }
    }

    public void ShowLogPanel() => logPanel.SetActive(true);
    public void HideLogPanel() => logPanel.SetActive(false);
}