using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MotorSettingsMenu : MonoBehaviour
{
    private const string PrefBaseForce = "MotorBaseForce";
    private const string PrefDistance = "MotorDistance";

    [Header("Sliders")]
    public Slider baseForceSlider;
    public Slider distanceSlider;

    [Header("Labels")]
    public TMP_Text baseForceText;
    public TMP_Text distanceText;

    [Header("Behavior")]
    public bool autoSaveOnChange = false;
    public bool forceDefaultsOnStart = false;

    [Header("Defaults")]
    public int defaultBaseForce = 300;
    public int defaultDistance = 30;

    void Start()
    {
        if (forceDefaultsOnStart)
            ApplyDefaultsToUI();
        else
            LoadToUI();
    }

    public void OnBaseForceChanged(float value)
    {
        UpdateLabel(baseForceText, value);
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
        ApplyToLiveMotorIfPresent();
    }

    public void Save()
    {
        if (baseForceSlider != null)
            PlayerPrefs.SetInt(PrefBaseForce, Mathf.RoundToInt(baseForceSlider.value));
        if (distanceSlider != null)
            PlayerPrefs.SetInt(PrefDistance, Mathf.RoundToInt(distanceSlider.value));

        PlayerPrefs.Save();
    }

    private static void ApplyToLiveMotorIfPresent()
    {
        MotorController motor = FindAnyObjectByType<MotorController>();
        if (motor == null)
            return;

        motor.ReloadSettingsFromPrefs();
        motor.ApplySpringSettings();
    }

    public void LoadToUI()
    {
        if (baseForceSlider != null)
        {
            int value = PlayerPrefs.GetInt(PrefBaseForce, Mathf.RoundToInt(baseForceSlider.value));
            baseForceSlider.SetValueWithoutNotify(value);
            UpdateLabel(baseForceText, value);
        }

        if (distanceSlider != null)
        {
            int value = PlayerPrefs.GetInt(PrefDistance, Mathf.RoundToInt(distanceSlider.value));
            distanceSlider.SetValueWithoutNotify(value);
            UpdateLabel(distanceText, value);
        }
    }

    private void ApplyDefaultsToUI()
    {
        if (baseForceSlider != null)
        {
            baseForceSlider.SetValueWithoutNotify(defaultBaseForce);
            UpdateLabel(baseForceText, defaultBaseForce);
        }

        if (distanceSlider != null)
        {
            distanceSlider.SetValueWithoutNotify(defaultDistance);
            UpdateLabel(distanceText, defaultDistance);
        }

        Save();
    }

    private void UpdateLabel(TMP_Text label, float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value).ToString();
    }
}
