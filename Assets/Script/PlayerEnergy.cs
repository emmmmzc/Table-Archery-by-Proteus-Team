using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy Settings")]
    public int maxEnergy = 10;
    private int currentEnergy = 0;

    [Header("UI")]
    public Image energyBarFill;
    public GameObject specialAttackIndicator;

    [Header("Attack Popup")]
    public GameObject panelAttack;
    public GameObject excellentObject;
    public GameObject missObject;
    public TMP_Text scoreText;
    public float attackPopupDuration = 0.8f;

    [Header("Special Attack Projectile")]
    public GameObject specialAttackProjectilePrefab;
    public Transform specialAttackSpawn;
    public float specialAttackSpeedZ = 12f;
    public float specialAttackSpeedY = 1.5f;
    public float specialAttackLifetime = 5f;

    [Header("Boss Reference")]
    public BossHealth bossHealth;

    [Header("Audio")]
    public AudioClip specialAttackSound;
    public AudioClip excellentSound;
    public AudioClip missSound;

    private bool isSpecialReady = false;
    private Coroutine popupRoutine;

    void Start()
    {
        currentEnergy = 0;
        UpdateUI();

        if (panelAttack != null) panelAttack.SetActive(false);
        if (excellentObject != null) excellentObject.SetActive(false);
        if (missObject != null) missObject.SetActive(false);
    }

    public void AddEnergy(int amount)
    {
        if (isSpecialReady) return;

        currentEnergy += amount;
        if (currentEnergy >= maxEnergy)
        {
            currentEnergy = maxEnergy;
            isSpecialReady = true;
            UpdateUI();
            PerformSpecialAttack();
        }
        else
        {
            UpdateUI();
        }
    }

    void PerformSpecialAttack()
    {
        if (!isSpecialReady) return;

        isSpecialReady = false;
        currentEnergy = 0;
        UpdateUI();

        if (bossHealth != null)
        {
            if (specialAttackProjectilePrefab != null)
            {
                Vector3 spawnPosition = specialAttackSpawn != null
                    ? specialAttackSpawn.position
                    : transform.position;

                GameObject projectile = Instantiate(specialAttackProjectilePrefab, spawnPosition, Quaternion.identity);
                Rigidbody body = projectile.GetComponent<Rigidbody>();
                if (body == null)
                    body = projectile.AddComponent<Rigidbody>();

                body.useGravity = false;
                Vector3 forward = specialAttackSpawn != null ? specialAttackSpawn.forward : transform.forward;
                Vector3 velocity = (forward * specialAttackSpeedZ) + (Vector3.up * specialAttackSpeedY);
                body.linearVelocity = velocity;

                Destroy(projectile, specialAttackLifetime);
                bossHealth.TakeSpecialAttack();
            }
            else
            {
                bossHealth.TakeSpecialAttack();
            }

            AudioManager.Instance.Play("SpecialAttack");
            Debug.Log("Special attack launched!");
            ShowAttackPopup(showExcellent: true, score: 0);
        }
        else
        {
            ShowAttackPopup(showExcellent: false, score: 0);
        }
    }

    public void ShowAttackPopup(bool showExcellent, int score)
    {
        if (panelAttack == null) return;

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        panelAttack.SetActive(true);
        if (excellentObject != null) { 
            excellentObject.SetActive(showExcellent);
            AudioManager.Instance.Play("ExcellentSound");    
        }
        if (missObject != null) {
            missObject.SetActive(!showExcellent);
            AudioManager.Instance.Play("MissSound");
        }
        if (scoreText != null)
            scoreText.text = score.ToString();

        popupRoutine = StartCoroutine(HidePopupAfterDelay());
    }

    private IEnumerator HidePopupAfterDelay()
    {
        yield return new WaitForSecondsRealtime(attackPopupDuration);
        if (panelAttack != null) panelAttack.SetActive(false);
        if (excellentObject != null) excellentObject.SetActive(false);
        if (missObject != null) missObject.SetActive(false);
        if (scoreText != null)
            scoreText.text = string.Empty;
    }

    void UpdateUI()
    {
        if (energyBarFill != null)
            energyBarFill.fillAmount = (float)currentEnergy / maxEnergy;
        if (specialAttackIndicator != null)
            specialAttackIndicator.SetActive(isSpecialReady);
    }
}