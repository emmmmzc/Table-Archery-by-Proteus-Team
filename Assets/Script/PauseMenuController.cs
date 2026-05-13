using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel;

    [Header("Scene")]
    public string menuSceneName = "menu";

    [Header("Cursor")]
    public bool manageCursor = true;
    public CursorLockMode pauseLockMode = CursorLockMode.None;
    public CursorLockMode resumeLockMode = CursorLockMode.Locked;
    public bool pauseCursorVisible = true;
    public bool resumeCursorVisible = false;

    private bool isPaused;

    private void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (IsPausePressed())
            TogglePause();
    }

    private static bool IsPausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        isPaused = true;
        MotorController motorController = FindAnyObjectByType<MotorController>();
        if (motorController != null)
            motorController.SendPowerOff();
        Time.timeScale = 0f;
        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (manageCursor)
        {
            Cursor.lockState = pauseLockMode;
            Cursor.visible = pauseCursorVisible;
        }
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        MotorController motorController = FindAnyObjectByType<MotorController>();
        if (motorController != null)
            motorController.ResumeAfterPause();
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (manageCursor)
        {
            Cursor.lockState = resumeLockMode;
            Cursor.visible = resumeCursorVisible;
        }
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}
