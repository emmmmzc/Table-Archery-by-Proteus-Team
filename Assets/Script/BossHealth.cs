using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BossHealth : MonoBehaviour
{
    [Header("Health")]
    public int hitsToDefeat = 5;
    private int currentHits = 0;

    [Header("Animations")]
    public Animator bossAnimator;
    public string hitTrigger = "Hit";
    public string specialDeathTrigger = "SpecialDeath";

    [Header("References")]
    public BossAttackManager attackManager;

    [Header("Boss Health UI")]
    public Image bossHealthBarFill;

    [Header("Win UI")]
    public GameObject winPanel;

    [Header("Scene Names")]
    public string nextSceneName = "SampleScene";
    public string menuSceneName = "menu";

    private bool isDefeated = false;

    void Start()
    {
        currentHits = 0;
        if (bossAnimator == null) bossAnimator = GetComponent<Animator>();
        if (attackManager == null) attackManager = GetComponent<BossAttackManager>();
        if (winPanel != null) winPanel.SetActive(false);
        UpdateHealthUI();
    }

    public void TakeSpecialAttack()
    {
        if (isDefeated) return;

        currentHits++;
        Debug.Log($"Special attack hit! {currentHits}/{hitsToDefeat}");
        UpdateHealthUI();

        if (currentHits < hitsToDefeat && bossAnimator != null)
            bossAnimator.SetTrigger(hitTrigger);

        if (currentHits >= hitsToDefeat)
            DefeatBoss();
    }

    private void UpdateHealthUI()
    {
        if (bossHealthBarFill != null)
            bossHealthBarFill.fillAmount = (float)currentHits / hitsToDefeat;
    }

    void DefeatBoss()
    {
        isDefeated = true;

        if (bossAnimator != null && !string.IsNullOrEmpty(specialDeathTrigger))
            bossAnimator.SetTrigger(specialDeathTrigger);

        if (attackManager != null) attackManager.enabled = false;

        Invoke("GameWin", 2f);
    }

    void GameWin()
    {
        Debug.Log("BOSS DEFEATED! YOU WIN!");
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void BackToMenu()
    {
        FitnessManager fitnessManager = FindAnyObjectByType<FitnessManager>();
        if (fitnessManager != null)
            fitnessManager.CommitSessionToLifetime();

        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    public void NextLevel()
    {
        FitnessManager fitnessManager = FindAnyObjectByType<FitnessManager>();
        if (fitnessManager != null)
            fitnessManager.CommitSessionToLifetime();

        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }

    public void ExitGame()
    {
        FitnessManager fitnessManager = FindAnyObjectByType<FitnessManager>();
        if (fitnessManager != null)
            fitnessManager.CommitSessionToLifetime();

        Time.timeScale = 1f;
        Application.Quit();
    }
}
