using System;
using System.IO;
using UnityEngine;

public struct FitnessHitResult
{
    public int motorScore;
    public int imuScore;
    public int totalScore;
    public int pullCount;
    public string grade;
    public bool isExcellent;
}

public struct FitnessSessionResult
{
    public int sessionScore;
    public int sessionMotorScore;
    public int sessionImuScore;
    public int sessionHitCount;
    public int sessionPullCount;
}

public class FitnessManager : MonoBehaviour
{
    public static FitnessManager Instance { get; private set; }

    [Header("Dependencies")]
    public MotorController motorController;
    public IMUReceiver imuReceiver;

    [Header("Scoring")]
    public int passGrade = 60;
    public bool useRealMotor = false;
    public int defaultMotorScore = 80;

    [Header("IMU")]
    public bool useImuScore = false;
    public int imuScoreOverride = 0;
    public float excellentGyroAverage = 1f;
    public float goodGyroAverage = 2f;
    public float okGyroAverage = 4f;
    public float shakyGyroAverage = 7f;
    public float veryShakyGyroAverage = 10f;

    [Header("Session (Read Only)")]
    public int sessionScore;
    public int sessionMotorScore;
    public int sessionImuScore;
    public int sessionHitCount;
    public int sessionPullCount;

    [Header("Last Hit (Read Only)")]
    public int lastMotorScore;
    public int lastImuScore;
    public int lastTotalScore;
    public float lastImuAverageGyro;

    [Header("Lifetime (Read Only)")]
    public int lifetimeScore;
    public int lifetimeHits;
    public int lifetimePulls;

    [Header("JSON")]
    public string jsonFileName = "FitnessData.json";

    private string jsonPath;
    private LifetimeData lifetime = new LifetimeData();
    private bool isTrackingImuScore;
    private float imuGyroMagnitudeSum;
    private int imuSampleCount;

    [Serializable]
    private class LifetimeData
    {
        public int totalScore;
        public int totalHits;
        public int totalPulls;
    }

    /* ----------------------------- Lifecycle ----------------------------- */
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        jsonPath = Path.Combine(Application.dataPath, jsonFileName);
        LoadLifetime();
        ApplyLifetimeToPublic();

        if (imuReceiver == null)
            imuReceiver = FindAnyObjectByType<IMUReceiver>();
    }

    /* ------------------------------ Scoring ------------------------------ */
    public FitnessHitResult OnHit(int motorScore)
    {
        int imuScore = 0;

        if (useImuScore)
            imuScore = GetImuScore();
        else
            isTrackingImuScore = false;

        // Combine motor + IMU, then compare with passGrade.
        int totalScore = motorScore + imuScore;
        bool isExcellent = totalScore >= passGrade;
        string grade = isExcellent ? "Excellent" : "Miss";

        lastMotorScore = motorScore;
        lastImuScore = imuScore;
        lastTotalScore = totalScore;

        sessionScore += totalScore;
        sessionMotorScore += motorScore;
        sessionImuScore += imuScore;
        sessionPullCount += 1;
        if (isExcellent)
            sessionHitCount += 1;

        return new FitnessHitResult
        {
            motorScore = motorScore,
            imuScore = imuScore,
            totalScore = totalScore,
            pullCount = sessionPullCount,
            grade = grade,
            isExcellent = isExcellent
        };
    }

    /* --------------------------- Session Summary ------------------------- */
    public FitnessSessionResult GetSessionResult()
    {
        return new FitnessSessionResult
        {
            sessionScore = sessionScore,
            sessionMotorScore = sessionMotorScore,
            sessionImuScore = sessionImuScore,
            sessionHitCount = sessionHitCount,
            sessionPullCount = sessionPullCount
        };
    }

    /* ------------------------------ Lifetime ----------------------------- */
    public void CommitSessionToLifetime()
    {
        lifetime.totalScore += sessionScore;
        lifetime.totalHits += sessionHitCount;
        lifetime.totalPulls += sessionPullCount;

        SaveLifetime();
        ApplyLifetimeToPublic();
        ClearSession();
    }

    public void ClearSession()
    {
        sessionScore = 0;
        sessionMotorScore = 0;
        sessionImuScore = 0;
        sessionHitCount = 0;
        sessionPullCount = 0;
    }

    /* ------------------------------- IMU --------------------------------- */
    public void BeginImuScoreWindow()
    {
        isTrackingImuScore = true;
        imuGyroMagnitudeSum = 0f;
        imuSampleCount = 0;
    }

    void Update()
    {
        if (!isTrackingImuScore || !useImuScore || imuReceiver == null || !imuReceiver.hasPacket)
            return;

        imuGyroMagnitudeSum += imuReceiver.gyroscope.magnitude;
        imuSampleCount++;
    }

    private int GetImuScore()
    {
        isTrackingImuScore = false;

        if (imuSampleCount <= 0)
        {
            lastImuAverageGyro = 0f;
            return Mathf.Clamp(imuScoreOverride, 0, 10);
        }

        float averageGyro = imuGyroMagnitudeSum / imuSampleCount;
        lastImuAverageGyro = averageGyro;

        if (averageGyro <= excellentGyroAverage)
            return 10;
        if (averageGyro <= goodGyroAverage)
            return 8;
        if (averageGyro <= okGyroAverage)
            return 6;
        if (averageGyro <= shakyGyroAverage)
            return 4;
        if (averageGyro <= veryShakyGyroAverage)
            return 2;

        return 0;
    }

    /* ------------------------------ Storage ------------------------------ */
    private void LoadLifetime()
    {
        if (!File.Exists(jsonPath))
        {
            lifetime = new LifetimeData();
            return;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            lifetime = JsonUtility.FromJson<LifetimeData>(json) ?? new LifetimeData();
        }
        catch (Exception ex)
        {
            lifetime = new LifetimeData();
            Debug.LogWarning($"[Fitness] Load failed: {ex.Message}");
        }
    }

    private void SaveLifetime()
    {
        try
        {
            string json = JsonUtility.ToJson(lifetime, true);
            File.WriteAllText(jsonPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Fitness] Save failed: {ex.Message}");
        }
    }

    private void ApplyLifetimeToPublic()
    {
        lifetimeScore = lifetime.totalScore;
        lifetimeHits = lifetime.totalHits;
        lifetimePulls = lifetime.totalPulls;
    }
}
