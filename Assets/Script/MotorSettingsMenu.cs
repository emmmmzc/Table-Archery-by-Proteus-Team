using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MotorSettingsMenu : MonoBehaviour
{
    private const string PrefBaseForce = "MotorBaseForce";
    private const string PrefPullLimit = "MotorPullLimit";
    private const string PrefDistance = "MotorDistance";

    [Header("Sliders")]
    public Slider baseForceSlider;
    public Slider pullLimitSlider;
    public Slider distanceSlider;

    [Header("Labels")]
    public TMP_Text baseForceText;
    public TMP_Text pullLimitText;
    public TMP_Text distanceText;

    [Header("Behavior")]
    public bool autoSaveOnChange = false;

    void Start()
    {
        LoadToUI();
    }

    public void OnBaseForceChanged(float value)
    {
        UpdateLabel(baseForceText, value);
        if (autoSaveOnChange)
            Save();
    }

    public void OnPullLimitChanged(float value)
    {
        UpdateLabel(pullLimitText, value);
        if (autoSaveOnChange)
            Save();
    }

    public void OnDistanceChanged(float value)
    {
        UpdateLabel(distanceText, value);
        if (autoSaveOnChange)
            Save();
    }

    public void Apply()
    {
        Save();
        MotorController motorController = FindAnyObjectByType<MotorController>();
        if (motorController != null)
            motorController.ApplySpringSettings();
    }

    public void Save()
    {
        if (baseForceSlider != null)
            PlayerPrefs.SetInt(PrefBaseForce, Mathf.RoundToInt(baseForceSlider.value));
        if (pullLimitSlider != null)
            PlayerPrefs.SetInt(PrefPullLimit, Mathf.RoundToInt(pullLimitSlider.value));
        if (distanceSlider != null)
            PlayerPrefs.SetInt(PrefDistance, Mathf.RoundToInt(distanceSlider.value));

        PlayerPrefs.Save();
    }

    public void LoadToUI()
    {
        if (baseForceSlider != null)
        {
            int value = PlayerPrefs.GetInt(PrefBaseForce, Mathf.RoundToInt(baseForceSlider.value));
            baseForceSlider.SetValueWithoutNotify(value);
            UpdateLabel(baseForceText, value);
        }

        if (pullLimitSlider != null)
        {
            int value = PlayerPrefs.GetInt(PrefPullLimit, Mathf.RoundToInt(pullLimitSlider.value));
            pullLimitSlider.SetValueWithoutNotify(value);
            UpdateLabel(pullLimitText, value);
        }

        if (distanceSlider != null)
        {
            int value = PlayerPrefs.GetInt(PrefDistance, Mathf.RoundToInt(distanceSlider.value));
            distanceSlider.SetValueWithoutNotify(value);
            UpdateLabel(distanceText, value);
        }
    }

    private void UpdateLabel(TMP_Text label, float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value).ToString();
    }
}
