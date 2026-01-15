using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CopyRoomCodeButton : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el componente TextMeshPro (TMP_Text) cuyo texto quieres copiar.")]
    [SerializeField] private TMP_Text roomCodeToCopy;

    // (Opcional) Para dar feedback visual de que se ha copiado
    [Header("Feedback Opcional")]
    [Tooltip("Arrastra el texto de un botón si quieres que cambie temporalmente al copiar.")]
    [SerializeField] private TMP_Text textFeedback;
    private string textOriginal;

    public void CopyToClipboard()
    {
        string textoACopiar = roomCodeToCopy.text;

        GUIUtility.systemCopyBuffer = textoACopiar;
        Debug.Log($"Texto copiado al portapapeles: {textoACopiar}");

        if (textFeedback != null)
        {
            StartCoroutine(ShowFeedbackCopy());
        }
    }

    private System.Collections.IEnumerator ShowFeedbackCopy()
    {
        if (textOriginal == null) textOriginal = textFeedback.text;

        textFeedback.text = "¡Copiado!";
        yield return new WaitForSeconds(1f);

        textFeedback.text = textOriginal;
    }
}
