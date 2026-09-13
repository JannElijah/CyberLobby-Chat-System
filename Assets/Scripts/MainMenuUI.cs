using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void OnPlayButtonClicked()
    {
        // Loads the offline scene, which likely handles the host/join logic
        SceneManager.LoadScene("0_OfflineScene");
    }

    public void OnOptionsButtonClicked()
    {
        // TODO: Implement opening an options menu/panel
        Debug.Log("Options button clicked! (Not yet implemented)");
    }

    public void OnCreditsButtonClicked()
    {
        // TODO: Implement opening a credits menu/panel
        Debug.Log("Credits button clicked! (Not yet implemented)");
    }

    public void OnQuitButtonClicked()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}
