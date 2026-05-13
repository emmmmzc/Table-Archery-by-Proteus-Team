using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem; // Needed for new Input System
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public ZombieSpawner zombieSpawner;
    public BossAttackManager bossAttackManager;
    public Animator bossAnimator;
    public GameObject startScreenPanel;   // Drag the StartScreenPanel here

    [Header("Animation Triggers")]
    public string pointingTrigger = "Pointing";
    public string furiousTrigger = "Furious";

    [Header("Timing")]
    public float initialDelay = 1.5f;

    [Header("Flow")]
    public bool waitForAnyKeyToStart = false;

    private bool gameStarted = false;

    void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic("BGM");

        // Disable boss attack initially
        if (bossAttackManager != null)
            bossAttackManager.enabled = false;

        if (waitForAnyKeyToStart)
        {
            // Show start screen, hide game logic initially
            if (startScreenPanel != null)
                startScreenPanel.SetActive(true);

            // Start the "wait for any key" routine
            StartCoroutine(WaitForAnyKey());
        }
        else
        {
            if (startScreenPanel != null)
                startScreenPanel.SetActive(false);

            gameStarted = true;
            StartCoroutine(StartGameSequence());
        }
    }

    IEnumerator WaitForAnyKey()
    {
        // Wait until any key is pressed (using new Input System)
        while (!gameStarted)
        {
            // Check for any key on keyboard or any button on gamepad
            bool anyKeyPressed = false;

            // Check keyboard
            if (Keyboard.current != null)
            {
                foreach (var key in Keyboard.current.allKeys)
                {
                    if (key.wasPressedThisFrame)
                    {
                        anyKeyPressed = true;
                        break;
                    }
                }
            }

            // Check any button on gamepad (optional)
            if (Gamepad.current != null)
            {
                foreach (var button in Gamepad.current.allControls)
                {
                    if (button is UnityEngine.InputSystem.Controls.ButtonControl btn && btn.wasPressedThisFrame)
                    {
                        anyKeyPressed = true;
                        break;
                    }
                }
            }

            // Also check mouse click (optional)
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            {
                anyKeyPressed = true;
            }

            if (anyKeyPressed)
            {
                gameStarted = true;
                break;
            }

            yield return null;
        }

        // Hide start screen
        if (startScreenPanel != null)
            startScreenPanel.SetActive(false);

        // Begin the game sequence (initial delay, then zombies)
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        yield return new WaitForSeconds(initialDelay);

        if (SceneManager.GetActiveScene().name == "BOSSONLY")
            yield break;
        
        // Begin zombie waves (ZombieSpawner will handle pointing before each wave)
        if (zombieSpawner != null)
            zombieSpawner.StartWaves(bossAnimator, pointingTrigger);
    }

    public void OnZombiesDefeated()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("Bossangry");

        // Play furious animation
        if (bossAnimator != null)
            bossAnimator.SetTrigger(furiousTrigger);

        // Enable boss attack
        if (bossAttackManager != null)
            bossAttackManager.enabled = true;

        Debug.Log("All waves cleared. Boss is furious!");
    }
}
