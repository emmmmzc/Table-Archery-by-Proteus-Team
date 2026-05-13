using TMPro;
using UnityEngine;

public class SessionScoreText : MonoBehaviour
{
    public enum ScoreSource
    {
        [InspectorName("Total Score")]
        SessionTotal,
        [InspectorName("Total Pull Score")]
        SessionMotor,
        [InspectorName("Total Stability Score")]
        SessionImu,
        [InspectorName("Last Shot Score")]
        LastTotal,
        [InspectorName("Last Pull Score")]
        LastMotor,
        [InspectorName("Last Stability Score")]
        LastImu,
        [InspectorName("Last Stability Average")]
        LastImuAverageGyro,
        [InspectorName("Hits")]
        Hits,
        [InspectorName("Pulls")]
        Pulls
    }

    public TMP_Text scoreText;
    public FitnessManager fitnessManager;
    public ScoreSource scoreSource = ScoreSource.SessionTotal;
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

        scoreText.text = string.Format(format, GetValue());
    }

    private object GetValue()
    {
        switch (scoreSource)
        {
            case ScoreSource.SessionMotor:
                return fitnessManager.sessionMotorScore;
            case ScoreSource.SessionImu:
                return fitnessManager.sessionImuScore;
            case ScoreSource.LastTotal:
                return fitnessManager.lastTotalScore;
            case ScoreSource.LastMotor:
                return fitnessManager.lastMotorScore;
            case ScoreSource.LastImu:
                return fitnessManager.lastImuScore;
            case ScoreSource.LastImuAverageGyro:
                return fitnessManager.lastImuAverageGyro;
            case ScoreSource.Hits:
                return fitnessManager.sessionHitCount;
            case ScoreSource.Pulls:
                return fitnessManager.sessionPullCount;
            case ScoreSource.SessionTotal:
            default:
                return fitnessManager.sessionScore;
        }
    }
}
