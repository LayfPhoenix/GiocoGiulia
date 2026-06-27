using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Necessario per i file

// CLASSE PER I DATI DI SALVATAGGIO (JSON)
[System.Serializable]
public class UserSaveData
{
    public int currentLevel = 0;
    public int totalScore = 0;
    public bool isFinished = false;
    public List<float> bestTimes = new List<float>(); // Lista dei tempi record
}

public class MemoryGameController : MonoBehaviour
{
    [Header("UI Reference")]
    public GameUIController uiController;

    [Header("--- MENU E PAUSA ---")]
    public Button pauseButton;
    public Button exitButton;
    public TMP_Text pauseBtnText;

    [Header("--- RIEPILOGO FINALE ---")]
    public GameObject finalSummaryPanel;
    public TMP_Text finalStatsText;

    [Header("--- COLLEGAMENTI UI ---")]
    public GameObject cardPrefab;
    public Transform gridContainer;
    public TMP_Text timerText;
    public TMP_Text scoreText;
    public TMP_Text infoText;
    public TMP_Text nameText;
    public TMP_Text levelText;

    [Header("--- IMMAGINI CATEGORIE ---")]
    public Sprite cardBackImage;
    public Sprite frontShape;
    public Sprite[] iconsLiv1_2;
    public Sprite[] iconsLiv3_4;
    public Sprite[] iconsLiv5_6;
    public Sprite[] iconsLiv7_8;
    public Sprite[] iconsLiv9_10;

    [Header("--- IMPOSTAZIONI LIVELLI ---")]
    int[] cardsPerLevel = { 8, 8, 10, 10, 12, 12, 14, 14, 16, 16 };
    float[] previewTimes = { 5, 3, 5, 3, 5, 3, 5, 3, 5, 3 };
    float[] gameTimes = { 30, 25, 40, 35, 50, 45, 60, 55, 70, 65 };

    // Variabili di stato
    private float timer;
    private int sessionScore; // Punteggio accumulato in questa sessione
    private string currentPlayerName;

    // Oggetto che contiene tutti i dati salvati
    private UserSaveData loadedData;

    private bool isGameActive = false;
    private bool isPaused = false;

    // Logica interna
    private List<Button> spawnedButtons = new List<Button>();
    private List<int> cardValues = new List<int>();
    private Button firstSelected, secondSelected;
    private int pairsFound;

    void Start()
    {
        // Recupera nome (questo rimane nei prefs globali perché è un'impostazione di sistema)
        currentPlayerName = PlayerPrefs.GetString("SavedPlayerName", "Paziente");
        if (nameText != null) nameText.text = currentPlayerName;

        // Setup UI
        if (exitButton != null) exitButton.gameObject.SetActive(false);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (finalSummaryPanel != null) finalSummaryPanel.SetActive(false);

        if (uiController != null) uiController.OnPreviewEnded += StartGameplayPhase;

        // CARICA I DATI DAL FILE JSON
        LoadDataFromFile();

        // LOGICA AVVIO
        if (loadedData.isFinished)
        {
            ShowFinalSummary();
        }
        else
        {
            StartLevel(loadedData.currentLevel);
        }
    }

    void OnDestroy()
    {
        if (uiController != null) uiController.OnPreviewEnded -= StartGameplayPhase;
    }

    void Update()
    {
        if (isGameActive && !isPaused)
        {
            timer -= Time.deltaTime;
            UpdateUI();
            if (timer <= 0) GameOver();
        }
    }

    // --- GESTIONE PERCORSI FILE ---

    string GetGameFolderPath()
    {
        // Se siamo nell'Editor, salva nella cartella del progetto (fuori da Assets)
        // Se siamo nella Build (.exe), salva nella stessa cartella dell'.exe
#if UNITY_EDITOR
        return Application.dataPath + "/../";
#else
        return System.AppDomain.CurrentDomain.BaseDirectory;
#endif
    }

    // --- SISTEMA DI LOG (.txt) ---

    void WriteLogToTextFile(string result, float timeTaken, int levelIndex)
    {
        string folder = GetGameFolderPath();
        string filePath = Path.Combine(folder, "MemoryGame_Log.txt");

        int totalPairs = cardsPerLevel[levelIndex] / 2;
        string timeStr = timeTaken.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        string logLine = $"[{currentPlayerName}] [Livello {levelIndex + 1}] [{result}] [{timeStr}s] [{pairsFound}/{totalPairs}] - {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        try
        {
            File.AppendAllText(filePath, logLine + "\n");
            Debug.Log("Log salvato in: " + filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Errore Log: " + ex.Message);
        }
    }

    // --- SISTEMA DI SALVATAGGIO JSON (.json) ---

    void LoadDataFromFile()
    {
        string fileName = currentPlayerName + "_Save.json";
        string fullPath = Path.Combine(GetGameFolderPath(), fileName);

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
                Debug.LogError("File di salvataggio corrotto, ne creo uno nuovo.");
                CreateNewSaveData();
            }
        }
        else
        {
            CreateNewSaveData();
        }

        // Sicurezza: inizializza la lista tempi se vuota
        while (loadedData.bestTimes.Count < cardsPerLevel.Length)
        {
            loadedData.bestTimes.Add(9999f); // 9999 = nessun tempo registrato
        }
    }

    void SaveDataToFile()
    {
        string fileName = currentPlayerName + "_Save.json";
        string fullPath = Path.Combine(GetGameFolderPath(), fileName);

        string jsonContent = JsonUtility.ToJson(loadedData, true); // true = pretty print (leggibile)
        File.WriteAllText(fullPath, jsonContent);

        Debug.Log("Dati salvati in: " + fullPath);
    }

    void CreateNewSaveData()
    {
        loadedData = new UserSaveData();
        loadedData.currentLevel = 0;
        loadedData.totalScore = 0;
        loadedData.isFinished = false;
        loadedData.bestTimes = new List<float>();
        for (int i = 0; i < cardsPerLevel.Length; i++) loadedData.bestTimes.Add(9999f);
    }

    // --- LOGICA DI GIOCO ---

    void StartLevel(int levelIndex)
    {
        isPaused = false;
        Time.timeScale = 1;
        if (uiController != null) uiController.HideGameOverPanel();
        if (finalSummaryPanel != null) finalSummaryPanel.SetActive(false);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (exitButton != null) exitButton.gameObject.SetActive(false);

        // Aggiorna dati interni
        loadedData.currentLevel = levelIndex;
        if (loadedData.currentLevel >= cardsPerLevel.Length) loadedData.currentLevel = cardsPerLevel.Length - 1;

        if (levelText != null) levelText.text = "Livello " + (loadedData.currentLevel + 1);

        timer = gameTimes[loadedData.currentLevel];
        pairsFound = 0;
        isGameActive = false;

        // Lo score visivo parte dal totale salvato
        sessionScore = loadedData.totalScore;

        // Generazione Griglia
        foreach (Transform child in gridContainer) Destroy(child.gameObject);
        spawnedButtons.Clear();
        cardValues.Clear();

        List<int> deck = new List<int>();
        int totalPairs = cardsPerLevel[loadedData.currentLevel] / 2;
        for (int i = 0; i < totalPairs; i++) { deck.Add(i); deck.Add(i); }

        for (int i = 0; i < deck.Count; i++)
        {
            int temp = deck[i]; int r = Random.Range(i, deck.Count);
            deck[i] = deck[r]; deck[r] = temp;
        }

        for (int i = 0; i < deck.Count; i++)
        {
            GameObject newCard = Instantiate(cardPrefab, gridContainer);
            Button btn = newCard.GetComponent<Button>();
            spawnedButtons.Add(btn);
            cardValues.Add(deck[i]);

            int index = i;
            btn.onClick.AddListener(() => OnCardClicked(index));
        }

        for (int i = 0; i < spawnedButtons.Count; i++) ShowCard(i, true);

        if (uiController != null) uiController.StartCountdown(previewTimes[loadedData.currentLevel]);
        if (infoText != null) infoText.text = "Memorizza le carte!";

        UpdateUI();
    }

    void StartGameplayPhase()
    {
        for (int i = 0; i < spawnedButtons.Count; i++) ShowCard(i, false);
        if (infoText != null) infoText.text = "Trova le coppie!";
        if (pauseButton != null) pauseButton.gameObject.SetActive(true);
        isGameActive = true;
    }

    // --- FINE LIVELLO ---

    void LevelComplete()
    {
        isGameActive = false;

        int levelJustCompleted = loadedData.currentLevel;
        float timeTaken = gameTimes[levelJustCompleted] - timer;

        while (loadedData.bestTimes.Count <= levelJustCompleted) loadedData.bestTimes.Add(9999f);
        if (timeTaken < loadedData.bestTimes[levelJustCompleted])
            loadedData.bestTimes[levelJustCompleted] = timeTaken;

        int scoreEarned = sessionScore - loadedData.totalScore;
        loadedData.totalScore = sessionScore;

        if (loadedData.currentLevel < cardsPerLevel.Length - 1)
            loadedData.currentLevel++;
        else
            loadedData.isFinished = true;

        SaveDataToFile();
        WriteLogToTextFile("VINTO", timeTaken, levelJustCompleted);

        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (exitButton != null) exitButton.gameObject.SetActive(false);

        if (loadedData.isFinished)
        {
            if (infoText != null) infoText.text = "GIOCO COMPLETATO!";
            ShowFinalSummary();
        }
        else
        {
            if (infoText != null) infoText.text = "Livello Superato!";
            string details = $"Tempo: {timeTaken:F1}s\n+{scoreEarned} punti";
            if (uiController != null) uiController.ShowGameOver(true, details);
        }
    }

    void GameOver()
    {
        isGameActive = false;
        float timeTaken = gameTimes[loadedData.currentLevel];

        WriteLogToTextFile("PERSO", timeTaken, loadedData.currentLevel);

        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (exitButton != null) exitButton.gameObject.SetActive(false);

        if (infoText != null) infoText.text = "Tempo Scaduto!";

        int totalPairs = cardsPerLevel[loadedData.currentLevel] / 2;
        string details = $"Coppie trovate: {pairsFound} / {totalPairs}";
        if (uiController != null) uiController.ShowGameOver(false, details);

        sessionScore = loadedData.totalScore;
    }

    // --- RIEPILOGO FINALE ---

    void ShowFinalSummary()
    {
        if (finalSummaryPanel == null) return;
        finalSummaryPanel.SetActive(true);

        string stats = $"Punteggio Totale: {loadedData.totalScore}\n\n";

        for (int i = 0; i < cardsPerLevel.Length; i++)
        {
            if (i < loadedData.bestTimes.Count && loadedData.bestTimes[i] < 9000f)
            {
                stats += $"Livello {i + 1}: {loadedData.bestTimes[i]:F1}s\n";
            }
            else
            {
                stats += $"Livello {i + 1}: --\n";
            }
        }
        if (finalStatsText != null) finalStatsText.text = stats;
    }

    public void ResetGameProgress()
    {
        // Resetta l'oggetto dati
        CreateNewSaveData();

        // Sovrascrive il file JSON con i dati vuoti
        SaveDataToFile();

        StartLevel(0);
    }

    // --- ALTRE FUNZIONI ---

    public void LoadNextLevel()
    {
        // Nota: loadedData.currentLevel è già stato incrementato in LevelComplete
        if (!loadedData.isFinished)
        {
            StartLevel(loadedData.currentLevel);
        }
        else
        {
            ShowFinalSummary();
        }
    }

    public void RestartCurrentLevel()
    {
        sessionScore = loadedData.totalScore;
        // Nota: se avevamo incrementato il livello ma poi perso o riavviato prima di salvare?
        // Qui usiamo StartLevel che usa loadedData.currentLevel.
        // In LevelComplete incrementiamo DOPO aver vinto. Quindi qui siamo ancora al livello giusto.
        StartLevel(loadedData.currentLevel);
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            Time.timeScale = 0;
            if (infoText != null) infoText.text = "PAUSA";
            if (exitButton != null) exitButton.gameObject.SetActive(true);
            if (pauseBtnText != null) pauseBtnText.text = "Riprendi ▶";
        }
        else
        {
            Time.timeScale = 1;
            if (infoText != null) infoText.text = "Trova le coppie!";
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            if (pauseBtnText != null) pauseBtnText.text = "Pausa II";
        }
    }

    public void ExitToMenu()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(0);
    }

    void OnCardClicked(int index)
    {
        if (!isGameActive || isPaused) return;
        if (spawnedButtons[index] == firstSelected) return;

        ShowCard(index, true);

        if (firstSelected == null)
        {
            firstSelected = spawnedButtons[index];
        }
        else
        {
            secondSelected = spawnedButtons[index];
            StartCoroutine(CheckMatch());
        }
    }

    IEnumerator CheckMatch()
    {
        isGameActive = false;
        int id1 = cardValues[spawnedButtons.IndexOf(firstSelected)];
        int id2 = cardValues[spawnedButtons.IndexOf(secondSelected)];

        yield return new WaitForSeconds(0.7f);

        if (id1 == id2)
        {
            sessionScore += 100; // Aggiorna score locale
            pairsFound++;
            firstSelected.interactable = false;
            secondSelected.interactable = false;

            if (pairsFound >= cardsPerLevel[loadedData.currentLevel] / 2)
                LevelComplete();
            else
                isGameActive = true;
        }
        else
        {
            ShowCard(spawnedButtons.IndexOf(firstSelected), false);
            ShowCard(spawnedButtons.IndexOf(secondSelected), false);
            isGameActive = true;
        }
        firstSelected = null; secondSelected = null;
    }

    void ShowCard(int index, bool faceUp)
    {
        Button btn = spawnedButtons[index];
        Image cardBase = btn.GetComponent<Image>();
        Transform iconObj = btn.transform.Find("Icona");

        if (iconObj == null) return;
        Image iconImg = iconObj.GetComponent<Image>();

        if (faceUp)
        {
            if (frontShape != null) cardBase.sprite = frontShape;
            cardBase.color = Color.white;
            iconObj.gameObject.SetActive(true);
            iconImg.sprite = GetSpriteForLevel(loadedData.currentLevel, cardValues[index]);
        }
        else
        {
            iconObj.gameObject.SetActive(false);
            if (cardBackImage != null)
            {
                cardBase.sprite = cardBackImage;
                cardBase.color = Color.white;
            }
            else
            {
                cardBase.sprite = frontShape;
                cardBase.color = new Color32(200, 50, 50, 255);
            }
        }
    }

    Sprite GetSpriteForLevel(int level, int cardValue)
    {
        Sprite[] targetArray = iconsLiv1_2;
        if (level == 0 || level == 1) targetArray = iconsLiv1_2;
        else if (level == 2 || level == 3) targetArray = iconsLiv3_4;
        else if (level == 4 || level == 5) targetArray = iconsLiv5_6;
        else if (level == 6 || level == 7) targetArray = iconsLiv7_8;
        else if (level >= 8) targetArray = iconsLiv9_10;

        if (targetArray == null || targetArray.Length == 0) return null;
        return targetArray[cardValue % targetArray.Length];
    }

    void UpdateUI()
    {
        if (timerText != null)
        {
            int m = Mathf.FloorToInt(timer / 60F);
            int s = Mathf.FloorToInt(timer - m * 60);
            timerText.text = string.Format("{0:00}:{1:00}", m, s);

            float ratio = timer / gameTimes[loadedData.currentLevel];
            if (ratio > 0.5f)       timerText.color = Color.white;
            else if (ratio > 0.25f) timerText.color = new Color(1f, 0.6f, 0f); // arancione
            else                    timerText.color = Color.red;
        }
        if (scoreText != null) scoreText.text = "Punti: " + sessionScore;
        if (isGameActive && infoText != null)
        {
            int totalPairs = cardsPerLevel[loadedData.currentLevel] / 2;
            infoText.text = $"Coppie trovate: {pairsFound} / {totalPairs}";
        }
    }
}