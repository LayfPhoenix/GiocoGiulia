using UnityEngine;
using UnityEngine.UI; // Serve per l'Input Field (Legacy)
using UnityEngine.SceneManagement; // Serve per cambiare scena
public class MenuController : MonoBehaviour
{
    public InputField nameInput; // Il campo dove scrivi il nome
    public void StartGame()
    {
        // 1. Prendi il testo scritto
        string playerName = nameInput.text;

        // 2. Se è vuoto, usa un nome di default
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Paziente";
        }
        // 3. Salva il nome nella memoria di Unity
        PlayerPrefs.SetString("SavedPlayerName", playerName);
        PlayerPrefs.Save();
        // 4. Carica la scena del gioco (assicurati che si chiami ESATTAMENTE cos�)
        SceneManager.LoadScene("GameScene");
    }

    public void QuitGame()
    {
        // Chiudi l'applicazione
        Application.Quit();
    }
}