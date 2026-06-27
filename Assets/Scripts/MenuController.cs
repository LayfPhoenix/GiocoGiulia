using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestisce il menu principale del gioco.
/// Si occupa di leggere il nome del paziente e avviare la partita.
/// </summary>
public class MenuController : MonoBehaviour
{
    // Campo di testo dove il terapista o il paziente inserisce il proprio nome
    public InputField nameInput;

    /// <summary>
    /// Chiamata dal bottone "Inizia" nella scena del menu.
    /// Salva il nome del giocatore e carica la scena di gioco.
    /// </summary>
    public void StartGame()
    {
        // Legge il testo inserito nel campo nome
        string playerName = nameInput.text;

        // Se il campo è vuoto, assegna un nome di default
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Paziente";
        }

        // Salva il nome nei PlayerPrefs così la scena di gioco può recuperarlo
        PlayerPrefs.SetString("SavedPlayerName", playerName);
        PlayerPrefs.Save();

        // Carica la scena principale del gioco
        SceneManager.LoadScene("GameScene");
    }
}
