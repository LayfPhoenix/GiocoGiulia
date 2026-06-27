using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameUIController : MonoBehaviour
{
    [Header("--- Timer Config ---")]
    public float previewTime = 3.0f;
    public Slider timerSlider;
    public TextMeshProUGUI timerText;

    [Header("--- Popup Config ---")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultTitle;
    public TextMeshProUGUI resultMessage;
    public Button nextLevelButton;
    public Button retryButton;

    // Evento per avvisare il Game Manager
    public System.Action OnPreviewEnded;

    private void Start()
    {
        // Assicuriamoci che il gioco non sia in pausa
        Time.timeScale = 1f;

        // Nascondi il popup all'avvio
        HideGameOverPanel();
    }

    // --- FUNZIONE CHIAMATA DAL GAME MANAGER PER AVVIARE IL TIMER ---
    public void StartCountdown(float time)
    {
        previewTime = time;
        StopAllCoroutines(); // Ferma vecchi timer se presenti
        StartCoroutine(PreviewCountdown());
    }

    IEnumerator PreviewCountdown()
    {
        float currentTime = previewTime;

        // Reset visivo
        timerSlider.gameObject.SetActive(true);
        timerText.gameObject.SetActive(true);
        timerText.text = "";

        timerSlider.maxValue = previewTime;
        timerSlider.value = previewTime;

        while (currentTime > 0)
        {
            currentTime -= Time.deltaTime;

            // Aggiorna la barra
            timerSlider.value = currentTime;
            // Aggiorna il testo (arrotondato per eccesso: 3, 2, 1)
            timerText.text = Mathf.CeilToInt(currentTime).ToString();

            yield return null;
        }

        // --- TEMPO SCADUTO ---
        timerText.text = "GO!";
        timerSlider.gameObject.SetActive(false); // Nascondi barra

        // Avvisa il Game Manager di coprire le carte
        if (OnPreviewEnded != null)
        {
            OnPreviewEnded.Invoke();
        }

        // Aspetta 1 secondo per far leggere "GO!"
        yield return new WaitForSeconds(1.0f);

        // Nascondi la scritta
        timerText.text = "";
        timerText.gameObject.SetActive(false);
    }

    // --- GESTIONE POPUP ---

    public void ShowGameOver(bool isVictory, string details = "")
    {
        resultPanel.SetActive(true);

        if (isVictory)
        {
            resultTitle.text = "LIVELLO COMPLETATO!";
            resultTitle.color = Color.green;
            resultMessage.text = string.IsNullOrEmpty(details) ? "Ottimo lavoro!" : "Ottimo lavoro!\n" + details;
            nextLevelButton.gameObject.SetActive(true);
        }
        else
        {
            resultTitle.text = "TEMPO SCADUTO";
            resultTitle.color = Color.red;
            resultMessage.text = string.IsNullOrEmpty(details) ? "Non mollare, riprova!" : "Non mollare, riprova!\n" + details;
            nextLevelButton.gameObject.SetActive(false);
        }
    }

    // Funzione fondamentale per far funzionare il tasto Avanti/Riprova
    public void HideGameOverPanel()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }
}