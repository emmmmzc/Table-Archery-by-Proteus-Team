using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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
    public TMP_Text winSummaryText;

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

        if (currentHits < hitsToDefeat)
            AudioManager.Instance.Play("BossDamage");

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
        AudioManager.Instance.Play("BossDeath");

        if (bossAnimator != null && !string.IsNullOrEmpty(specialDeathTrigger))
            bossAnimator.SetTrigger(specialDeathTrigger);

        if (attackManager != null) attackManager.enabled = false;

        Invoke("GameWin", 2f);
    }

    void GameWin()
    {
        Debug.Log("BOSS DEFEATED! YOU WIN!");
        AudioManager.Instance.Play("Victory");
        if (winPanel != null)
        {
            FitnessManager fitnessManager = FindAnyObjectByType<FitnessManager>();
            if (fitnessManager != null && winSummaryText != null)
            {
                FitnessSessionResult session = fitnessManager.GetSessionResult();
                winSummaryText.text = $"Score: {session.sessionScore}\nHits: {session.sessionHitCount}\nPulls: {session.sessionPullCount}";
            }
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

        MotorController motorController = FindAnyObjectByType<MotorController>();
        if (motorController != null)
            motorController.SendPowerOff();

        Time.timeScale = 1f;
        Application.Quit();
    }
}
