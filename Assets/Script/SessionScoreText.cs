using TMPro;
using UnityEngine;

public class SessionScoreText : MonoBehaviour
{
    public TMP_Text scoreText;
    public FitnessManager fitnessManager;
    public string format = "{0}";

    private void Awake()
    {
        if (scoreText == null)
            scoreText = GetComponent<TMP_Text>();
        if (fitnessManager == null)
            fitnessManager = FindAnyObjectByType<FitnessManager>();
    }

    private void Update()
    {
        if (scoreText == null || fitnessManager == null)
            return;

        scoreText.text = string.Format(format, fitnessManager.sessionScore);
    }
}
