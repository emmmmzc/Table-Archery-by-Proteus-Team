using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    public float attackPopupDuration = 0.8f;

    [Header("Boss Reference")]
    public BossHealth bossHealth;

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
            bossHealth.TakeSpecialAttack();
            Debug.Log("Special attack automatically launched!");
            ShowAttackPopup(showExcellent: true);
        }
        else
        {
            ShowAttackPopup(showExcellent: false);
        }
    }

    public void ShowAttackPopup(bool showExcellent)
    {
        if (panelAttack == null) return;

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        panelAttack.SetActive(true);
        if (excellentObject != null) excellentObject.SetActive(showExcellent);
        if (missObject != null) missObject.SetActive(!showExcellent);

        popupRoutine = StartCoroutine(HidePopupAfterDelay());
    }

    private IEnumerator HidePopupAfterDelay()
    {
        yield return new WaitForSecondsRealtime(attackPopupDuration);
        if (panelAttack != null) panelAttack.SetActive(false);
        if (excellentObject != null) excellentObject.SetActive(false);
        if (missObject != null) missObject.SetActive(false);
    }

    void UpdateUI()
    {
        if (energyBarFill != null)
            energyBarFill.fillAmount = (float)currentEnergy / maxEnergy;
        if (specialAttackIndicator != null)
            specialAttackIndicator.SetActive(isSpecialReady);
    }
}