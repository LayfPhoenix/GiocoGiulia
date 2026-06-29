# GiocoGiulia – Documentazione del Progetto

> Progetto Unity 6 (6000.3.5f2) — Gioco del Memory per uso terapeutico

---

## Indice

1. [Panoramica del Progetto](#panoramica-del-progetto)
2. [Struttura dei File](#struttura-dei-file)
3. [Scene](#scene)
4. [Script](#script)
5. [Flusso di Gioco Completo](#flusso-di-gioco-completo)
6. [Sistema di Salvataggio](#sistema-di-salvataggio)
7. [Sistema di Log](#sistema-di-log)
8. [Tabella dei Livelli](#tabella-dei-livelli)
9. [Layout UI](#layout-ui)
10. [Come Aprire ed Eseguire il Progetto](#come-aprire-ed-eseguire-il-progetto)

---

## Panoramica del Progetto

**GiocoGiulia** è un gioco del memory a 10 livelli progettato per uso terapeutico.
Il terapista inserisce il nome del paziente nel menu iniziale; il gioco salva
automaticamente i progressi in un file JSON e registra ogni sessione in un file
di log testuale. Ogni livello introduce un maggior numero di carte e un tempo
limite più lungo, aumentando gradualmente la difficoltà cognitiva.

---

## Struttura dei File

```
My project/
├── Assets/
│   ├── Prefabs/
│   │   └── CardPrefab.prefab          # Prefab della singola carta
│   ├── Scenes/
│   │   ├── MenuScene.unity            # Menu principale
│   │   └── GameScene.unity            # Scena di gioco
│   ├── Scripts/
│   │   ├── MenuController.cs          # Logica del menu iniziale
│   │   ├── GameUIController.cs        # Anteprima e popup risultato
│   │   └── MemoryGameController.cs    # Logica centrale del gioco
│   └── Sprites/                       # Immagini icone e carte
├── ProjectSettings/
│   └── ProjectVersion.txt             # Unity 6000.3.5f2
├── .gitignore                         # Esclude Library/, Temp/, salvataggi, log
└── DOCUMENTAZIONE.md                  # Questo file
```

I file generati a runtime (non inclusi nel repository):
- `<NomeGiocatore>_Save.json` — salvataggio progressi del paziente
- `MemoryGame_Log.txt` — log testuale di tutte le sessioni

---

## Scene

### MenuScene

Contiene un singolo Canvas con:
- Un campo di testo (`InputField`) dove inserire il nome del giocatore
- Un bottone **Inizia** che chiama `MenuController.StartGame()`

### GameScene

Canvas principale (1920 × 1000) con i seguenti elementi radice:

| Elemento UI         | Descrizione                                             |
|---------------------|---------------------------------------------------------|
| `TimerText`         | Timer di gioco (MM:SS), cambia colore a 50% e 25%      |
| `ScoreText`         | Punteggio sessione corrente                             |
| `LevelText`         | Numero del livello corrente                             |
| `NameText`          | Nome del giocatore                                      |
| `InfoText`          | Testo di stato (istruzioni, coppie trovate, pausa…)     |
| `GrigliaCarte`      | Contenitore `GridLayoutGroup` con le carte              |
| `PauseButton`       | Pausa / Riprendi                                        |
| `ExitButton`        | Esci al menu (visibile solo in pausa)                   |
| `PreviewSlider`     | Barra del conto alla rovescia di anteprima              |
| `PreviewText`       | Numero "3 – 2 – 1 – GO!" durante l'anteprima           |
| `Popup_Window`      | Pannello risultato (vittoria o sconfitta)               |
| `FinalSummaryPanel` | Pannello riepilogo finale (dopo l'ultimo livello)       |

---

## Script

### MenuController.cs

**Scopo:** gestisce il menu iniziale.

**Flusso:**
1. L'utente digita il nome nel campo `nameInput`
2. `StartGame()` legge il testo; se vuoto usa `"Paziente"`
3. Salva il nome in `PlayerPrefs` (`SavedPlayerName`)
4. Carica la scena `"GameScene"` tramite `SceneManager.LoadScene`

---

### GameUIController.cs

**Scopo:** gestisce elementi visivi disgiunti dalla logica del memory:
il conto alla rovescia di anteprima e il popup di risultato.

**Evento pubblico:**
```csharp
public System.Action OnPreviewEnded;
```
Viene invocato al termine dell'anteprima. `MemoryGameController` si iscrive a
questo evento in `Start()` e si disiscrive in `OnDestroy()`.

**Metodi principali:**

| Metodo | Chiamato da | Cosa fa |
|--------|-------------|---------|
| `StartCountdown(float time)` | `MemoryGameController.StartLevel` | Avvia la coroutine di anteprima |
| `ShowGameOver(bool isVictory, string details)` | `MemoryGameController.LevelComplete / GameOver` | Mostra popup vittoria o sconfitta |
| `HideGameOverPanel()` | `MemoryGameController.StartLevel` | Nasconde il popup all'inizio di ogni livello |

**Coroutine `PreviewCountdown`:**
- Mostra barra e numero animato (3, 2, 1)
- Al termine mostra "GO!", chiama `OnPreviewEnded?.Invoke()`
- Nasconde barra e testo dopo 1 secondo

---

### MemoryGameController.cs

**Scopo:** nucleo centrale del gioco. Gestisce livelli, carte, punteggio,
salvataggio e log.

**Struttura dati persistente (`UserSaveData`):**

```csharp
public int currentLevel;      // Indice 0-based del livello (0 = Livello 1)
public int totalScore;        // Punteggio totale accumulato
public bool isFinished;       // true dopo aver superato il livello 10
public List<float> bestTimes; // Tempo migliore per ogni livello (9999 = mai completato)
```

**Percorsi file a runtime:**

| Contesto | Cartella base |
|----------|---------------|
| Unity Editor | Cartella radice del progetto (`Application.dataPath/../`) |
| Build exe | Cartella dell'eseguibile (`AppDomain.CurrentDomain.BaseDirectory`) |

---

## Flusso di Gioco Completo

```
[MenuScene]
    │
    │  L'utente inserisce il nome e preme "Inizia"
    │  → PlayerPrefs["SavedPlayerName"] = nome
    │
    ▼
[GameScene – Start()]
    │
    ├─ LoadDataFromFile()
    │   ├─ Esiste <Nome>_Save.json → deserializza in UserSaveData
    │   └─ Non esiste / corrotto → CreateNewSaveData() (currentLevel=0)
    │
    ├─ isFinished = true? → ShowFinalSummary() ──────────────────────────────┐
    └─ isFinished = false? → StartLevel(currentLevel)                        │
                                                                             │
[StartLevel(levelIndex)]                                                     │
    │                                                                        │
    ├─ Calcola colonne: cols = ceil(numCards / 3)                            │
    ├─ Configura GridLayoutGroup (cellSize e spacing dinamici)               │
    ├─ Genera mazzo mescolato (Fisher-Yates)                                 │
    ├─ Istanzia carte → spawnedButtons[], cardValues[]                       │
    ├─ Mostra tutte le carte scoperte (fase anteprima)                       │
    └─ GameUIController.StartCountdown(previewTime[level])                   │
                                                                             │
[Anteprima – PreviewCountdown coroutine]                                     │
    │  Barra + numero animato (es. 5, 4, 3, 2, 1)                           │
    │  → Al termine: OnPreviewEnded?.Invoke()                                │
    │                                                                        │
    ▼                                                                        │
[StartGameplayPhase()]                                                       │
    │  Copre tutte le carte                                                  │
    │  isGameActive = true → il timer inizia a scorrere                      │
    │                                                                        │
[Update() loop]                                                              │
    │  timer -= Time.deltaTime                                               │
    │  UpdateUI() → colore timer, punteggio, coppie trovate                  │
    │                                                                        │
    ├─ timer ≤ 0 → GameOver()                                                │
    └─ click carta → OnCardClicked(index)                                    │
           │                                                                 │
           ├─ Prima selezione: salva firstSelected                           │
           └─ Seconda selezione: avvia CheckMatch() coroutine               │
                  │                                                          │
                  ├─ Stessa coppia (id1 == id2)                             │
                  │   ├─ sessionScore += 100                                 │
                  │   ├─ pairsFound++                                        │
                  │   ├─ disabilita i due bottoni                            │
                  │   ├─ pairsFound == totalPairs? → LevelComplete()         │
                  │   └─ altrimenti isGameActive = true                      │
                  │                                                          │
                  └─ Coppia errata                                           │
                      ├─ Rigira le due carte                                 │
                      └─ isGameActive = true                                 │
                                                                             │
[LevelComplete()]                                                            │
    │  Aggiorna bestTimes[level] se nuovo record                             │
    │  scoreEarned = sessionScore - loadedData.totalScore                    │
    │  loadedData.totalScore = sessionScore                                  │
    │  currentLevel < 9 → currentLevel++                                     │
    │  currentLevel == 9 → isFinished = true                                 │
    │  SaveDataToFile() + WriteLogToTextFile("VINTO")                        │
    │                                                                        │
    ├─ isFinished = true → ShowFinalSummary() ───────────────────────────────┘
    └─ isFinished = false → ShowGameOver(true, "Tempo: Xs\n+N punti")
              │
              └─ Utente preme "Avanti" → LoadNextLevel() → StartLevel(currentLevel)

[GameOver()]
    │  WriteLogToTextFile("PERSO")
    │  sessionScore = loadedData.totalScore  ← annulla punti del tentativo
    └─ ShowGameOver(false, "Coppie trovate: X / Y")
              │
              └─ Utente preme "Riprova" → RestartCurrentLevel() → StartLevel(currentLevel)

[Pausa – TogglePause()]
    │  isPaused = true  → Time.timeScale = 0, mostra "Esci"
    └─ isPaused = false → Time.timeScale = 1, nasconde "Esci"

[Esci al menu – ExitToMenu()]
    └─ Time.timeScale = 1 → SceneManager.LoadScene(0)
```

---

## Sistema di Salvataggio

**Formato:** JSON leggibile (`prettyPrint = true`)

**Percorso file:** `<cartellaBase>/<NomeGiocatore>_Save.json`

**Esempio di contenuto:**
```json
{
    "currentLevel": 3,
    "totalScore": 600,
    "isFinished": false,
    "bestTimes": [
        12.5,
        18.3,
        25.1,
        9999.0,
        9999.0,
        9999.0,
        9999.0,
        9999.0,
        9999.0,
        9999.0
    ]
}
```

**Quando viene salvato:** solo dopo una vittoria (`LevelComplete`), non dopo una sconfitta.
Il file viene aggiornato anche in caso di reset (`ResetGameProgress`).

---

## Sistema di Log

**Formato:** testo semplice, una riga per sessione

**Percorso file:** `<cartellaBase>/MemoryGame_Log.txt`

**Formato di ogni riga:**
```
[NomeGiocatore] [Livello N] [VINTO/PERSO] [XX.XXs] [X/Y] - AAAA-MM-GG HH:MM:SS
```

**Esempio:**
```
[Giulia] [Livello 2] [VINTO] [17.43s] [4/4] - 2026-06-15 10:32:05
[Giulia] [Livello 3] [PERSO] [40.00s] [2/5] - 2026-06-15 10:33:48
```

Il separatore decimale è sempre il punto (`.`), indipendentemente dalla lingua
del sistema operativo, grazie a `CultureInfo.InvariantCulture`.

---

## Tabella dei Livelli

| Livello | Carte | Coppie | Anteprima | Tempo limite | Colonne griglia | Cella px |
|---------|-------|--------|-----------|--------------|-----------------|----------|
| 1       | 8     | 4      | 5 s       | 30 s         | 3               | 160      |
| 2       | 8     | 4      | 3 s       | 25 s         | 3               | 160      |
| 3       | 10    | 5      | 5 s       | 40 s         | 4               | 160      |
| 4       | 10    | 5      | 3 s       | 35 s         | 4               | 160      |
| 5       | 12    | 6      | 5 s       | 50 s         | 4               | 160      |
| 6       | 12    | 6      | 3 s       | 45 s         | 4               | 160      |
| 7       | 14    | 7      | 5 s       | 60 s         | 5               | 135      |
| 8       | 14    | 7      | 3 s       | 55 s         | 5               | 135      |
| 9       | 16    | 8      | 5 s       | 70 s         | 6               | 115      |
| 10      | 16    | 8      | 3 s       | 65 s         | 6               | 115      |

Le icone cambiano ogni coppia di livelli: livelli 1-2 usano `iconsLiv1_2`,
livelli 3-4 usano `iconsLiv3_4`, e così via.

---

## Layout UI

### Griglia Carte (`GrigliaCarte`)

- Posizione: `(0, -20)`, dimensione: `1050 × 580 px`
- `GridLayoutGroup` con `FixedColumnCount`
- Il numero di colonne è calcolato a runtime: `cols = ceil(numCards / 3)`
- Garantisce sempre **massimo 3 righe** di carte
- La griglia si espande verticalmente (aggiunge righe), non orizzontalmente

### Popup Risultato (`Popup_Window`)

- Dimensione: `700 × 500 px`
- `Title_Text`: `650 × 70 px`, posizione `y = +195`
- `Message_Text`: `650 × 200 px`, posizione `y = +40`
- `Btn_Next` (Avanti): `220 × 65 px`, posizione `(+155, -185)`
- `Btn_Retry` (Riprova): `220 × 65 px`, posizione `(-155, -185)`

### Colori del Timer

| Percentuale di tempo rimanente | Colore |
|-------------------------------|--------|
| > 50%                         | Bianco |
| 25% – 50%                     | Arancione `(1.0, 0.6, 0.0)` |
| < 25%                         | Rosso |

---

## Come Aprire ed Eseguire il Progetto

### Requisiti

- **Unity Hub** con Unity **6000.3.5f2** installato
- **Visual Studio 2022** o **VS Code** con estensione C#

### Apertura

1. Aprire **Unity Hub**
2. Cliccare **Add → Add project from disk**
3. Selezionare la cartella `My project`
4. Aprire il progetto e attendere il completamento dell'import

### Avvio in Editor

1. Aprire `Assets/Scenes/MenuScene.unity` nel Project panel
2. Premere il tasto **Play** (▶) nell'Editor
3. Inserire un nome nel campo e cliccare **Inizia**

### Build per Windows

1. **File → Build Settings**
2. Piattaforma: **PC, Mac & Linux Standalone** → **Windows**
3. Cliccare **Build** e scegliere una cartella di output
4. Il file `.exe` e i salvataggi/log verranno creati nella stessa cartella

### Repository GitHub

Il progetto è pubblicato su: `https://github.com/LayfPhoenix/GiocoGiulia`

```bash
git clone https://github.com/LayfPhoenix/GiocoGiulia.git
```


## Improvements

- Aggiungere pulsate d'uscita
- modificare immagini senza sfondo