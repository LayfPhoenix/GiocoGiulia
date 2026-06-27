using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Gestisce tutti gli elementi visivi della scena di gioco che non dipendono
/// dalla logica del memory: il conto alla rovescia di anteprima e il popup
/// di fine livello (vittoria o sconfitta).
/// Comunica con MemoryGameController tramite l'evento OnPreviewEnded.
/// </summary>
public class GameUIController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // --- TIMER DI ANTEPRIMA --------------------------------------------------
    // -------------------------------------------------------------------------

    [Header("--- Timer Anteprima ---")]

    // Durata in secondi del conto alla rovescia iniziale (impostata da StartCountdown)
    public float previewTime = 3.0f;

    // Barra di avanzamento che si svuota durante l'anteprima
    public Slider timerSlider;

    // Testo che mostra "3 – 2 – 1 – GO!" durante l'anteprima
    public TextMeshProUGUI timerText;

    // -------------------------------------------------------------------------
    // --- POPUP DI FINE LIVELLO -----------------------------------------------
    // -------------------------------------------------------------------------

    [Header("--- Popup Risultato ---")]

    // Pannello radice del popup (attivato/disattivato a seconda del risultato)
    public GameObject resultPanel;

    // Titolo del popup: "LIVELLO COMPLETATO!" oppure "TEMPO SCADUTO"
    public TextMeshProUGUI resultTitle;

    // Messaggio con dettagli: tempo impiegato, punteggio guadagnato o coppie trovate
    public TextMeshProUGUI resultMessage;

    // Bottone "Avanti" visibile solo in caso di vittoria
    public Button nextLevelButton;

    // Bottone "Riprova" visibile solo in caso di sconfitta
    public Button retryButton;

    // -------------------------------------------------------------------------
    // --- EVENTI --------------------------------------------------------------
    // -------------------------------------------------------------------------

    // Evento pubblicato al termine del conto alla rovescia; MemoryGameController
    // si iscrive a questo evento per avviare la fase di gioco vera e propria
    public System.Action OnPreviewEnded;

    // -------------------------------------------------------------------------
    // --- CICLO DI VITA -------------------------------------------------------
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Garantisce che il gioco parta senza pausa
        Time.timeScale = 1f;

        // Nasconde il popup al primo avvio (potrebbe rimasto aperto da una sessione precedente)
        HideGameOverPanel();
    }

    // -------------------------------------------------------------------------
    // --- CONTO ALLA ROVESCIA DI ANTEPRIMA ------------------------------------
    // -------------------------------------------------------------------------

    /// <summary>
    /// Avvia il conto alla rovescia di anteprima per la durata specificata.
    /// Viene chiamato da MemoryGameController all'inizio di ogni livello.
    /// </summary>
    public void StartCountdown(float time)
    {
        previewTime = time;

        // Ferma eventuali coroutine precedenti (sicurezza in caso di riavvio rapido)
        StopAllCoroutines();
        StartCoroutine(PreviewCountdown());
    }

    /// <summary>
    /// Coroutine che anima il conto alla rovescia frame per frame:
    ///   – aggiorna la barra slider
    ///   – aggiorna il testo numerico (3, 2, 1)
    ///   – al termine mostra "GO!" e invoca OnPreviewEnded
    /// </summary>
    IEnumerator PreviewCountdown()
    {
        float currentTime = previewTime;

        // Mostra barra e testo, resetta i valori iniziali
        timerSlider.gameObject.SetActive(true);
        timerText.gameObject.SetActive(true);
        timerText.text = "";
        timerSlider.maxValue = previewTime;
        timerSlider.value = previewTime;

        // Decrementa il tempo ogni frame
        while (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            timerSlider.value = currentTime;

            // Arrotonda per eccesso: 2.9 → "3", 1.1 → "2", ecc.
            timerText.text = Mathf.CeilToInt(currentTime).ToString();

            yield return null;
        }

        // Il tempo è scaduto: mostra "GO!" e nasconde la barra
        timerText.text = "GO!";
        timerSlider.gameObject.SetActive(false);

        // Notifica MemoryGameController che l'anteprima è terminata
        OnPreviewEnded?.Invoke();

        // Lascia la scritta "GO!" visibile per un secondo prima di nasconderla
        yield return new WaitForSeconds(1.0f);

        timerText.text = "";
        timerText.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // --- POPUP DI FINE LIVELLO -----------------------------------------------
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mostra il popup con il risultato del livello.
    /// </summary>
    /// <param name="isVictory">true = il giocatore ha trovato tutte le coppie in tempo</param>
    /// <param name="details">Riga di dettaglio opzionale (tempo, punteggio, coppie trovate)</param>
    public void ShowGameOver(bool isVictory, string details = "")
    {
        resultPanel.SetActive(true);

        if (isVictory)
        {
            // Vittoria: titolo verde, mostra il bottone per proseguire
            resultTitle.text = "LIVELLO COMPLETATO!";
            resultTitle.color = Color.green;
            resultMessage.text = string.IsNullOrEmpty(details)
                ? "Ottimo lavoro!"
                : "Ottimo lavoro!\n" + details;
            nextLevelButton.gameObject.SetActive(true);
        }
        else
        {
            // Sconfitta: titolo rosso, nasconde il bottone per proseguire
            resultTitle.text = "TEMPO SCADUTO";
            resultTitle.color = Color.red;
            resultMessage.text = string.IsNullOrEmpty(details)
                ? "Non mollare, riprova!"
                : "Non mollare, riprova!\n" + details;
            nextLevelButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Nasconde il popup di fine livello.
    /// Chiamato all'inizio di ogni nuovo livello per pulire lo schermo.
    /// </summary>
    public void HideGameOverPanel()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }
}
