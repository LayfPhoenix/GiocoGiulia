using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.IO;

// =============================================================================
// STRUTTURA DATI DI SALVATAGGIO
// Serializzata in JSON su disco con il nome "<NomeGiocatore>_Save.json"
// =============================================================================

/// <summary>
/// Contiene tutti i dati persistenti del giocatore.
/// Viene serializzata in JSON e salvata su disco ad ogni avanzamento di livello.
/// </summary>
[System.Serializable]
public class UserSaveData
{
    // Indice (0-based) del livello corrente: 0 = Livello 1, 9 = Livello 10
    public int currentLevel = 0;

    // Punteggio totale accumulato su tutti i livelli completati
    public int totalScore = 0;

    // true quando il giocatore ha superato tutti e 10 i livelli
    public bool isFinished = false;

    // Tempo migliore (in secondi) per ogni livello; 9999 = mai completato
    public List<float> bestTimes = new List<float>();
}

// =============================================================================
// CONTROLLER PRINCIPALE DI GIOCO
// =============================================================================

/// <summary>
/// Nucleo del gioco del memory.
/// Responsabile di:
///   – generare e mescolare le carte ad ogni livello
///   – gestire il timer di gioco
///   – controllare le coppie trovate
///   – calcolare il punteggio
///   – salvare i progressi su file JSON
///   – scrivere un log testuale di ogni sessione
///   – mostrare il riepilogo finale
/// </summary>
public class MemoryGameController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // --- RIFERIMENTI UI ------------------------------------------------------
    // -------------------------------------------------------------------------

    [Header("UI Reference")]
    // Componente che gestisce il conto alla rovescia e il popup di risultato
    public GameUIController uiController;

    [Header("--- MENU E PAUSA ---")]
    // Bottone per mettere in pausa / riprendere la partita
    public Button pauseButton;
    // Bottone per tornare al menu principale (visibile solo in pausa)
    public Button exitButton;
    // Testo del bottone pausa: alterna "Pausa II" e "Riprendi ▶"
    public TMP_Text pauseBtnText;

    [Header("--- RIEPILOGO FINALE ---")]
    // Pannello mostrato al completamento di tutti i livelli
    public GameObject finalSummaryPanel;
    // Testo con punteggio totale e tempi migliori per ogni livello
    public TMP_Text finalStatsText;

    [Header("--- COLLEGAMENTI UI ---")]
    // Prefab della singola carta (Button con figlio "Icona")
    public GameObject cardPrefab;
    // Contenitore GridLayoutGroup dove vengono istanziate le carte
    public Transform gridContainer;
    // Testo del timer di gioco (MM:SS), cambia colore quando il tempo scarseggia
    public TMP_Text timerText;
    // Testo che mostra il punteggio corrente della sessione
    public TMP_Text scoreText;
    // Testo di stato: "Memorizza le carte!", "Coppie trovate: X/Y", "PAUSA", ecc.
    public TMP_Text infoText;
    // Testo con il nome del giocatore (recuperato dai PlayerPrefs)
    public TMP_Text nameText;
    // Testo con il numero del livello corrente ("Livello 3")
    public TMP_Text levelText;

    [Header("--- IMMAGINI CATEGORIE ---")]
    // Sprite usato come retro della carta (carta coperta)
    public Sprite cardBackImage;
    // Sprite usato come sfondo della carta scoperta
    public Sprite frontShape;
    // Set di icone per ogni coppia di livelli (livelli 1-2, 3-4, 5-6, 7-8, 9-10)
    public Sprite[] iconsLiv1_2;
    public Sprite[] iconsLiv3_4;
    public Sprite[] iconsLiv5_6;
    public Sprite[] iconsLiv7_8;
    public Sprite[] iconsLiv9_10;

    // -------------------------------------------------------------------------
    // --- CONFIGURAZIONE LIVELLI ----------------------------------------------
    // -------------------------------------------------------------------------

    [Header("--- IMPOSTAZIONI LIVELLI ---")]
    // Numero totale di carte per ogni livello (sempre in coppie)
    int[] cardsPerLevel  = { 8, 8, 10, 10, 12, 12, 14, 14, 16, 16 };

    // Secondi di anteprima (fase in cui le carte sono scoperte all'inizio)
    float[] previewTimes = { 5, 3,  5,  3,  5,  3,  5,  3,  5,  3  };

    // Secondi totali disponibili per completare il livello
    float[] gameTimes    = { 30, 25, 40, 35, 50, 45, 60, 55, 70, 65 };

    // -------------------------------------------------------------------------
    // --- STATO INTERNO -------------------------------------------------------
    // -------------------------------------------------------------------------

    // Tempo rimanente al completamento del livello (conto alla rovescia)
    private float timer;

    // Punteggio accumulato nella sessione corrente (resettato al totalScore in caso di sconfitta)
    private int sessionScore;

    // Nome del giocatore corrente, usato come chiave per il file di salvataggio
    private string currentPlayerName;

    // Dati caricati da disco o appena creati per un nuovo giocatore
    private UserSaveData loadedData;

    // true solo durante la fase in cui il giocatore può cliccare le carte
    private bool isGameActive = false;

    // true quando la partita è in pausa (Time.timeScale = 0)
    private bool isPaused = false;

    // Lista di tutti i Button carta istanziati nella griglia corrente
    private List<Button> spawnedButtons = new List<Button>();

    // Valore (ID coppia) associato a ciascuna carta, parallelo a spawnedButtons
    private List<int> cardValues = new List<int>();

    // Riferimenti alle due carte selezionate dal giocatore nel turno corrente
    private Button firstSelected, secondSelected;

    // Numero di coppie trovate correttamente nel livello corrente
    private int pairsFound;

    // =========================================================================
    // CICLO DI VITA UNITY
    // =========================================================================

    void Start()
    {
        // Recupera il nome del giocatore salvato dal menu (default: "Paziente")
        currentPlayerName = PlayerPrefs.GetString("SavedPlayerName", "Paziente");
        if (nameText != null) nameText.text = currentPlayerName;

        // Nasconde elementi UI che compaiono solo durante il gameplay
        if (exitButton != null)       exitButton.gameObject.SetActive(false);
        if (pauseButton != null)      pauseButton.gameObject.SetActive(false);
        if (finalSummaryPanel != null) finalSummaryPanel.SetActive(false);

        // Si iscrive all'evento di fine anteprima per avviare la fase di gioco
        if (uiController != null) uiController.OnPreviewEnded += StartGameplayPhase;

        // Carica il salvataggio esistente o ne crea uno nuovo
        LoadDataFromFile();

        // Se il giocatore aveva già completato tutti i livelli, mostra il riepilogo;
        // altrimenti riparte dal livello dove si era fermato
        if (loadedData.isFinished)
            ShowFinalSummary();
        else
            StartLevel(loadedData.currentLevel);
    }

    void OnDestroy()
    {
        // Rimuove la sottoscrizione all'evento per evitare memory leak
        if (uiController != null) uiController.OnPreviewEnded -= StartGameplayPhase;
    }

    void Update()
    {
        // Il timer scala solo durante la fase attiva (non in pausa, non durante l'anteprima)
        if (isGameActive && !isPaused)
        {
            timer -= Time.deltaTime;
            UpdateUI();
            if (timer <= 0) GameOver();
        }
    }

    // =========================================================================
    // GESTIONE PERCORSI FILE
    // =========================================================================

    /// <summary>
    /// Restituisce la cartella base dove salvare file di log e salvataggi.
    /// In Editor: cartella radice del progetto.
    /// In Build: cartella dell'eseguibile.
    /// </summary>
    string GetGameFolderPath()
    {
#if UNITY_EDITOR
        return Application.dataPath + "/../";
#else
        return System.AppDomain.CurrentDomain.BaseDirectory;
#endif
    }

    // =========================================================================
    // SISTEMA DI LOG (.txt)
    // =========================================================================

    /// <summary>
    /// Aggiunge una riga al file MemoryGame_Log.txt con il risultato della sessione.
    /// Formato: [Nome] [Livello N] [VINTO/PERSO] [XX.XXs] [X/Y coppie] - data ora
    /// </summary>
    /// <param name="result">Stringa "VINTO" o "PERSO"</param>
    /// <param name="timeTaken">Secondi impiegati (o tempo massimo se perso)</param>
    /// <param name="levelIndex">Indice 0-based del livello giocato</param>
    void WriteLogToTextFile(string result, float timeTaken, int levelIndex)
    {
        string folder   = GetGameFolderPath();
        string filePath = Path.Combine(folder, "MemoryGame_Log.txt");

        int    totalPairs = cardsPerLevel[levelIndex] / 2;
        // Usa InvariantCulture per garantire il punto decimale indipendentemente dalla lingua del SO
        string timeStr = timeTaken.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        string logLine = $"[{currentPlayerName}] [Livello {levelIndex + 1}] [{result}] [{timeStr}s] [{pairsFound}/{totalPairs}] - {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        try
        {
            File.AppendAllText(filePath, logLine + "\n");
            Debug.Log("Log salvato in: " + filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Errore scrittura log: " + ex.Message);
        }
    }

    // =========================================================================
    // SISTEMA DI SALVATAGGIO JSON
    // =========================================================================

    /// <summary>
    /// Carica il file JSON del giocatore corrente.
    /// Se il file non esiste o è corrotto, crea un nuovo salvataggio da zero.
    /// Garantisce inoltre che la lista bestTimes abbia una entry per ogni livello.
    /// </summary>
    void LoadDataFromFile()
    {
        string fileName  = currentPlayerName + "_Save.json";
        string fullPath  = Path.Combine(GetGameFolderPath(), fileName);

        if (File.Exists(fullPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(fullPath);
                loadedData = JsonUtility.FromJson<UserSaveData>(jsonContent);
                Debug.Log("Dati caricati da: " + fullPath);
            }
            catch
            {
                // File corrotto: inizia da capo
                Debug.LogError("File di salvataggio corrotto, ne creo uno nuovo.");
                CreateNewSaveData();
            }
        }
        else
        {
            // Prima partita: nessun file esistente
            CreateNewSaveData();
        }

        // Sicurezza: riempie eventuali slot mancanti nella lista tempi
        while (loadedData.bestTimes.Count < cardsPerLevel.Length)
            loadedData.bestTimes.Add(9999f); // 9999 = nessun record registrato
    }

    /// <summary>
    /// Serializza loadedData in JSON e lo scrive su disco.
    /// Chiamato dopo ogni livello completato con successo.
    /// </summary>
    void SaveDataToFile()
    {
        string fileName  = currentPlayerName + "_Save.json";
        string fullPath  = Path.Combine(GetGameFolderPath(), fileName);

        // prettyPrint = true rende il JSON leggibile da un editor di testo
        string jsonContent = JsonUtility.ToJson(loadedData, true);
        File.WriteAllText(fullPath, jsonContent);

        Debug.Log("Dati salvati in: " + fullPath);
    }

    /// <summary>
    /// Inizializza un oggetto UserSaveData vuoto con valori di partenza.
    /// </summary>
    void CreateNewSaveData()
    {
        loadedData = new UserSaveData
        {
            currentLevel = 0,
            totalScore   = 0,
            isFinished   = false,
            bestTimes    = new List<float>()
        };
        for (int i = 0; i < cardsPerLevel.Length; i++)
            loadedData.bestTimes.Add(9999f);
    }

    // =========================================================================
    // LOGICA DI GIOCO
    // =========================================================================

    /// <summary>
    /// Prepara e avvia un livello:
    ///   1. Resetta lo stato interno e l'UI
    ///   2. Configura la griglia (colonne dinamiche per max 3 righe)
    ///   3. Genera un mazzo mescolato di carte
    ///   4. Istanzia le carte e avvia il conto alla rovescia di anteprima
    /// </summary>
    void StartLevel(int levelIndex)
    {
        // ---- Reset stato ----
        isPaused = false;
        Time.timeScale = 1;
        if (uiController != null)      uiController.HideGameOverPanel();
        if (finalSummaryPanel != null) finalSummaryPanel.SetActive(false);
        if (pauseButton != null)       pauseButton.gameObject.SetActive(false);
        if (exitButton != null)        exitButton.gameObject.SetActive(false);

        // Clamp di sicurezza sull'indice livello
        loadedData.currentLevel = Mathf.Clamp(levelIndex, 0, cardsPerLevel.Length - 1);
        if (levelText != null) levelText.text = "Livello " + (loadedData.currentLevel + 1);

        timer        = gameTimes[loadedData.currentLevel];
        pairsFound   = 0;
        isGameActive = false;

        // Il punteggio visivo riparte dall'ultimo totale salvato
        sessionScore = loadedData.totalScore;

        // ---- Pulizia griglia precedente ----
        foreach (Transform child in gridContainer) Destroy(child.gameObject);
        spawnedButtons.Clear();
        cardValues.Clear();

        // ---- Configurazione dinamica GridLayoutGroup ----
        // Il numero di colonne è calcolato in modo che ci siano sempre al massimo 3 righe.
        // Esempio: 16 carte → ceil(16/3) = 6 colonne, 3 righe da 6/5/5 carte
        int numCards = cardsPerLevel[loadedData.currentLevel];
        int cols     = Mathf.CeilToInt(numCards / 3f);
        GridLayoutGroup grid = gridContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            float cell, gap;
            if      (cols <= 4) { cell = 160f; gap = 15f; }  // fino a 12 carte
            else if (cols == 5) { cell = 135f; gap = 12f; }  // 14 carte
            else                { cell = 115f; gap = 10f; }  // 16 carte (6 colonne)
            grid.constraintCount = cols;
            grid.cellSize        = new Vector2(cell, cell);
            grid.spacing         = new Vector2(gap, gap);
        }

        // ---- Generazione mazzo ----
        // Crea coppie con ID 0,1,2,...,N-1 e le duplica
        List<int> deck = new List<int>();
        int totalPairs = numCards / 2;
        for (int i = 0; i < totalPairs; i++) { deck.Add(i); deck.Add(i); }

        // Mescola con algoritmo Fisher-Yates
        for (int i = 0; i < deck.Count; i++)
        {
            int r = Random.Range(i, deck.Count);
            (deck[i], deck[r]) = (deck[r], deck[i]);
        }

        // ---- Istanziazione carte ----
        for (int i = 0; i < deck.Count; i++)
        {
            GameObject newCard = Instantiate(cardPrefab, gridContainer);
            Button btn = newCard.GetComponent<Button>();
            spawnedButtons.Add(btn);
            cardValues.Add(deck[i]);

            // Cattura l'indice in una variabile locale per la lambda
            int index = i;
            btn.onClick.AddListener(() => OnCardClicked(index));
        }

        // Mostra tutte le carte scoperte durante l'anteprima
        for (int i = 0; i < spawnedButtons.Count; i++) ShowCard(i, true);

        // Avvia il conto alla rovescia di anteprima (al termine chiama StartGameplayPhase)
        if (uiController != null) uiController.StartCountdown(previewTimes[loadedData.currentLevel]);
        if (infoText != null)     infoText.text = "Memorizza le carte!";

        UpdateUI();
    }

    /// <summary>
    /// Chiamata dall'evento OnPreviewEnded di GameUIController.
    /// Copre le carte e abilita l'interazione del giocatore.
    /// </summary>
    void StartGameplayPhase()
    {
        // Copre tutte le carte (mostra il retro)
        for (int i = 0; i < spawnedButtons.Count; i++) ShowCard(i, false);

        if (infoText != null)    infoText.text = "Trova le coppie!";
        if (pauseButton != null) pauseButton.gameObject.SetActive(true);

        // Da questo momento il timer inizia a scorrere e le carte sono cliccabili
        isGameActive = true;
    }

    // =========================================================================
    // FINE LIVELLO
    // =========================================================================

    /// <summary>
    /// Chiamata quando tutte le coppie sono state trovate prima dello scadere del tempo.
    /// Aggiorna il record di tempo, il punteggio, avanza il livello e salva su disco.
    /// </summary>
    void LevelComplete()
    {
        isGameActive = false;

        int   levelJustCompleted = loadedData.currentLevel;
        float timeTaken          = gameTimes[levelJustCompleted] - timer;

        // Aggiorna il record del livello se il tempo è migliore
        while (loadedData.bestTimes.Count <= levelJustCompleted) loadedData.bestTimes.Add(9999f);
        if (timeTaken < loadedData.bestTimes[levelJustCompleted])
            loadedData.bestTimes[levelJustCompleted] = timeTaken;

        // Calcola i punti guadagnati in questo livello prima di aggiornare il totale
        int scoreEarned        = sessionScore - loadedData.totalScore;
        loadedData.totalScore  = sessionScore;

        // Avanza al livello successivo oppure segna il gioco come completato
        if (loadedData.currentLevel < cardsPerLevel.Length - 1)
            loadedData.currentLevel++;
        else
            loadedData.isFinished = true;

        // Persistenza: salva su JSON e aggiunge riga al log
        SaveDataToFile();
        WriteLogToTextFile("VINTO", timeTaken, levelJustCompleted);

        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (exitButton != null)  exitButton.gameObject.SetActive(false);

        if (loadedData.isFinished)
        {
            // Tutti i livelli completati: mostra il riepilogo finale
            if (infoText != null) infoText.text = "GIOCO COMPLETATO!";
            ShowFinalSummary();
        }
        else
        {
            // Livello superato ma ce ne sono altri: mostra il popup di vittoria
            if (infoText != null) infoText.text = "Livello Superato!";
            string details = $"Tempo: {timeTaken:F1}s\n+{scoreEarned} punti";
            if (uiController != null) uiController.ShowGameOver(true, details);
        }
    }

    /// <summary>
    /// Chiamata quando il timer raggiunge zero.
    /// Resetta il punteggio al totale salvato e mostra il popup di sconfitta.
    /// </summary>
    void GameOver()
    {
        isGameActive = false;

        // Il tempo totale del livello viene registrato come "tempo impiegato" nel log
        float timeTaken = gameTimes[loadedData.currentLevel];
        WriteLogToTextFile("PERSO", timeTaken, loadedData.currentLevel);

        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (exitButton != null)  exitButton.gameObject.SetActive(false);

        if (infoText != null) infoText.text = "Tempo Scaduto!";

        int totalPairs = cardsPerLevel[loadedData.currentLevel] / 2;
        string details = $"Coppie trovate: {pairsFound} / {totalPairs}";
        if (uiController != null) uiController.ShowGameOver(false, details);

        // Annulla i punti guadagnati in questo tentativo fallito
        sessionScore = loadedData.totalScore;
    }

    // =========================================================================
    // RIEPILOGO FINALE
    // =========================================================================

    /// <summary>
    /// Mostra il pannello di riepilogo con punteggio totale e tempi migliori.
    /// Viene mostrato sia alla prima apertura se il gioco era già completato,
    /// sia immediatamente dopo aver superato l'ultimo livello.
    /// </summary>
    void ShowFinalSummary()
    {
        if (finalSummaryPanel == null) return;
        finalSummaryPanel.SetActive(true);

        string stats = $"Punteggio Totale: {loadedData.totalScore}\n\n";
        for (int i = 0; i < cardsPerLevel.Length; i++)
        {
            // Mostra il tempo migliore se esiste, altrimenti "--" per livelli mai completati
            bool hasRecord = i < loadedData.bestTimes.Count && loadedData.bestTimes[i] < 9000f;
            stats += hasRecord
                ? $"Livello {i + 1}: {loadedData.bestTimes[i]:F1}s\n"
                : $"Livello {i + 1}: --\n";
        }

        if (finalStatsText != null) finalStatsText.text = stats;
    }

    // =========================================================================
    // AZIONI PUBBLICHE (collegate ai bottoni nell'Inspector)
    // =========================================================================

    /// <summary>
    /// Azzera completamente i progressi del giocatore e ricomincia dal livello 1.
    /// Collegato al bottone "Ricomincia" nel pannello finale.
    /// </summary>
    public void ResetGameProgress()
    {
        CreateNewSaveData();
        SaveDataToFile();
        StartLevel(0);
    }

    /// <summary>
    /// Carica il livello successivo (loadedData.currentLevel è già stato incrementato
    /// in LevelComplete). Collegato al bottone "Avanti" del popup di vittoria.
    /// </summary>
    public void LoadNextLevel()
    {
        if (!loadedData.isFinished)
            StartLevel(loadedData.currentLevel);
        else
            ShowFinalSummary();
    }

    /// <summary>
    /// Riavvia il livello corrente senza perdere i progressi.
    /// Collegato al bottone "Riprova" del popup di sconfitta.
    /// </summary>
    public void RestartCurrentLevel()
    {
        // Ripristina il punteggio all'ultimo valore salvato per annullare
        // i punti parziali guadagnati nel tentativo appena fallito
        sessionScore = loadedData.totalScore;
        StartLevel(loadedData.currentLevel);
    }

    /// <summary>
    /// Alterna lo stato di pausa.
    /// In pausa: ferma il tempo, mostra il bottone Esci.
    /// In gioco: riprende il tempo, nasconde il bottone Esci.
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0;
            if (infoText != null)    infoText.text    = "PAUSA";
            if (exitButton != null)  exitButton.gameObject.SetActive(true);
            if (pauseBtnText != null) pauseBtnText.text = "Riprendi ▶";
        }
        else
        {
            Time.timeScale = 1;
            if (infoText != null)    infoText.text    = "Trova le coppie!";
            if (exitButton != null)  exitButton.gameObject.SetActive(false);
            if (pauseBtnText != null) pauseBtnText.text = "Pausa II";
        }
    }

    /// <summary>
    /// Torna al menu principale (scena 0).
    /// Chiamato dal bottone "Esci" (visibile solo in pausa).
    /// </summary>
    public void ExitToMenu()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(0);
    }

    // =========================================================================
    // LOGICA CLICK CARTE
    // =========================================================================

    /// <summary>
    /// Gestisce il click su una carta.
    /// Ignora il click se il gioco non è attivo, è in pausa, o la carta è già
    /// quella già selezionata come prima scelta.
    /// </summary>
    void OnCardClicked(int index)
    {
        if (!isGameActive || isPaused) return;

        // Evita di selezionare due volte la stessa carta come primo e secondo elemento
        if (spawnedButtons[index] == firstSelected) return;

        // Scopre la carta cliccata
        ShowCard(index, true);

        if (firstSelected == null)
        {
            // Prima carta del turno: memorizza la selezione e attende la seconda
            firstSelected = spawnedButtons[index];
        }
        else
        {
            // Seconda carta del turno: avvia il controllo della coppia
            secondSelected = spawnedButtons[index];
            StartCoroutine(CheckMatch());
        }
    }

    /// <summary>
    /// Coroutine che confronta le due carte selezionate dopo una breve pausa visiva.
    /// – Se corrispondono: le disabilita e aggiorna punteggio e coppie trovate.
    /// – Se non corrispondono: le rigira a faccia in giù.
    /// In entrambi i casi resetta la selezione corrente.
    /// </summary>
    IEnumerator CheckMatch()
    {
        // Blocca temporaneamente l'interazione per lasciare tempo di vedere le carte
        isGameActive = false;

        int id1 = cardValues[spawnedButtons.IndexOf(firstSelected)];
        int id2 = cardValues[spawnedButtons.IndexOf(secondSelected)];

        yield return new WaitForSeconds(0.7f);

        if (id1 == id2)
        {
            // Coppia trovata: +100 punti, carte disabilitate definitivamente
            sessionScore += 100;
            pairsFound++;
            firstSelected.interactable  = false;
            secondSelected.interactable = false;

            // Controlla se il livello è completato
            if (pairsFound >= cardsPerLevel[loadedData.currentLevel] / 2)
                LevelComplete();
            else
                isGameActive = true; // Ci sono ancora coppie da trovare
        }
        else
        {
            // Coppia errata: rigira entrambe le carte a faccia in giù
            ShowCard(spawnedButtons.IndexOf(firstSelected),  false);
            ShowCard(spawnedButtons.IndexOf(secondSelected), false);
            isGameActive = true;
        }

        // Resetta la selezione corrente per il prossimo turno
        firstSelected  = null;
        secondSelected = null;
    }

    // =========================================================================
    // RENDERING CARTE
    // =========================================================================

    /// <summary>
    /// Mostra una carta in modalità scoperta (faccia in su) o coperta (faccia in giù).
    /// </summary>
    /// <param name="index">Indice della carta in spawnedButtons</param>
    /// <param name="faceUp">true = fronte visibile, false = retro visibile</param>
    void ShowCard(int index, bool faceUp)
    {
        Button btn      = spawnedButtons[index];
        Image  cardBase = btn.GetComponent<Image>();
        Transform iconObj = btn.transform.Find("Icona");

        if (iconObj == null) return;
        Image iconImg = iconObj.GetComponent<Image>();

        if (faceUp)
        {
            // Mostra lo sfondo frontale e l'icona della categoria corrente
            if (frontShape != null) cardBase.sprite = frontShape;
            cardBase.color = Color.white;
            iconObj.gameObject.SetActive(true);
            iconImg.sprite = GetSpriteForLevel(loadedData.currentLevel, cardValues[index]);
        }
        else
        {
            // Nasconde l'icona e mostra il retro (o uno sfondo rosso se non c'è sprite)
            iconObj.gameObject.SetActive(false);
            if (cardBackImage != null)
            {
                cardBase.sprite = cardBackImage;
                cardBase.color  = Color.white;
            }
            else
            {
                // Fallback: usa frontShape colorato di rosso scuro
                cardBase.sprite = frontShape;
                cardBase.color  = new Color32(200, 50, 50, 255);
            }
        }
    }

    /// <summary>
    /// Restituisce lo sprite corretto per il livello e il valore (ID) della carta.
    /// I livelli sono raggruppati a coppie: liv 0-1 → iconsLiv1_2, ecc.
    /// </summary>
    Sprite GetSpriteForLevel(int level, int cardValue)
    {
        Sprite[] targetArray;

        if      (level <= 1) targetArray = iconsLiv1_2;
        else if (level <= 3) targetArray = iconsLiv3_4;
        else if (level <= 5) targetArray = iconsLiv5_6;
        else if (level <= 7) targetArray = iconsLiv7_8;
        else                 targetArray = iconsLiv9_10;

        if (targetArray == null || targetArray.Length == 0) return null;

        // Modulo per evitare IndexOutOfRange se il set ha meno sprite del previsto
        return targetArray[cardValue % targetArray.Length];
    }

    // =========================================================================
    // AGGIORNAMENTO UI
    // =========================================================================

    /// <summary>
    /// Aggiorna timer, punteggio e testo di stato ogni frame durante il gameplay.
    /// Il colore del timer cambia in base al tempo rimanente:
    ///   bianco  → > 50% del tempo
    ///   arancio → tra 25% e 50%
    ///   rosso   → < 25% del tempo
    /// </summary>
    void UpdateUI()
    {
        if (timerText != null)
        {
            int m = Mathf.FloorToInt(timer / 60f);
            int s = Mathf.FloorToInt(timer - m * 60);
            timerText.text = string.Format("{0:00}:{1:00}", m, s);

            float ratio = timer / gameTimes[loadedData.currentLevel];
            if      (ratio > 0.5f)  timerText.color = Color.white;
            else if (ratio > 0.25f) timerText.color = new Color(1f, 0.6f, 0f); // arancione
            else                    timerText.color = Color.red;
        }

        if (scoreText != null) scoreText.text = "Punti: " + sessionScore;

        // Mostra il progresso delle coppie solo durante la fase di gioco attiva
        if (isGameActive && infoText != null)
        {
            int totalPairs = cardsPerLevel[loadedData.currentLevel] / 2;
            infoText.text = $"Coppie trovate: {pairsFound} / {totalPairs}";
        }
    }
}
