using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("UI References")]
    public Image[] heartImages;
    public Sprite heartFull;
    public Sprite heartEmpty;

    [Header("Red Flash")]
    public Image redFlashImage;
    public float flashDuration = 0.2f;
    public Color flashColor = new Color(1f, 0f, 0f, 0.6f);

    [Header("Lose UI")]
    public GameObject losePanel;
    public string menuSceneName = "menu";

    [Header("Legacy Fallback")]
    public Image gameOverImage;
    public float gameOverFadeDuration = 1f;

    [Header("Input")]
    public PlayerInputHandler inputHandler;

    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHeartsUI();

        if (redFlashImage != null)
            redFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);

        if (losePanel != null)
            losePanel.SetActive(false);

        if (gameOverImage != null)
            gameOverImage.color = new Color(1, 1, 1, 0);
    }

    void Update()
    {
        if (isDead && inputHandler != null && inputHandler.RestartTriggered)
            RestartCurrentScene();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("EnemyProjectile"))
        {
            TakeDamage(1);
            Destroy(collision.gameObject);
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHeartsUI();
        TriggerRedFlash();

        if (currentHealth <= 0)
            Die();
    }

    private void UpdateHeartsUI()
    {
        for (int i = 0; i < heartImages.Length; i++)
            heartImages[i].sprite = (i < currentHealth) ? heartFull : heartEmpty;
    }

    private void TriggerRedFlash()
    {
        if (redFlashImage != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashCoroutine(redFlashImage, flashColor, flashDuration));
        }
    }

    private IEnumerator FlashCoroutine(Image image, Color targetColor, float duration)
    {
        float elapsed = 0;
        Color startColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0);

        while (elapsed < duration / 2)
        {
            float t = elapsed / (duration / 2);
            image.color = Color.Lerp(startColor, targetColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        image.color = targetColor;
        elapsed = 0;

        while (elapsed < duration / 2)
        {
            float t = elapsed / (duration / 2);
            image.color = Color.Lerp(targetColor, startColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        image.color = startColor;
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Player died - YOU SUCK!");

        if (losePanel != null)
        {
            losePanel.SetActive(true);
            Time.timeScale = 0f;
        }
        else if (gameOverImage != null)
        {
            StartCoroutine(FadeInGameOver());
        }

        FirstPersonController controller = GetComponent<FirstPersonController>();
        if (controller != null) controller.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator FadeInGameOver()
    {
        float elapsed = 0;
        Color startColor = new Color(1, 1, 1, 0);
        Color endColor = new Color(1, 1, 1, 1);

        while (elapsed < gameOverFadeDuration)
        {
            float t = elapsed / gameOverFadeDuration;
            gameOverImage.color = Color.Lerp(startColor, endColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        gameOverImage.color = endColor;
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}