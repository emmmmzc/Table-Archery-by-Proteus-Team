using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject infoPanel;

    [Header("Navigation")]
    public string gameSceneName = "SampleScene";

    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic("BGM");

        if (infoPanel != null)
            infoPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(true);
    }

    public void CloseInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void ExitGame()
    {
        MotorController motorController = FindAnyObjectByType<MotorController>();
        if (motorController != null)
            motorController.SendPowerOff();

        Application.Quit();
    }
}
