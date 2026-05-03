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
    public int sessionHitCount;
    public int sessionPullCount;
}

public class FitnessManager : MonoBehaviour
{
    public static FitnessManager Instance { get; private set; }

    [Header("Dependencies")]
    public MotorController motorController;

    [Header("Scoring")]
    public int passGrade = 60;
    public bool useRealMotor = false;
    public int defaultMotorScore = 80;

    [Header("IMU")]
    public bool useImuScore = false;
    public int imuScoreOverride = 0;

    [Header("Session (Read Only)")]
    public int sessionScore;
    public int sessionHitCount;
    public int sessionPullCount;

    [Header("Lifetime (Read Only)")]
    public int lifetimeScore;
    public int lifetimeHits;
    public int lifetimePulls;

    [Header("JSON")]
    public string jsonFileName = "FitnessData.json";

    private string jsonPath;
    private LifetimeData lifetime = new LifetimeData();

    [Serializable]
    private class LifetimeData
    {
        public int totalScore;
        public int totalHits;
        public int totalPulls;
    }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        jsonPath = Path.Combine(Application.dataPath, jsonFileName);
        LoadLifetime();
        ApplyLifetimeToPublic();
    }

    public FitnessHitResult OnHit()
    {
        int motorScore = 0;
        int imuScore = 0;
        int pullCount = 0;

        if (useRealMotor)
        {
            if (motorController != null)
            {
                motorScore = motorController.GetMotorScore();
                pullCount = motorController.GetPullCount();
            }
        }
        else
        {
            motorScore = defaultMotorScore;
        }

        if (useImuScore)
            imuScore = GetImuScore();

        int totalScore = motorScore + imuScore;
        bool isExcellent = totalScore >= passGrade;
        string grade = isExcellent ? "Excellent" : "Miss";

        sessionScore += totalScore;
        sessionHitCount += 1;
        sessionPullCount = pullCount;

        return new FitnessHitResult
        {
            motorScore = motorScore,
            imuScore = imuScore,
            totalScore = totalScore,
            pullCount = pullCount,
            grade = grade,
            isExcellent = isExcellent
        };
    }

    public FitnessSessionResult GetSessionResult()
    {
        return new FitnessSessionResult
        {
            sessionScore = sessionScore,
            sessionHitCount = sessionHitCount,
            sessionPullCount = sessionPullCount
        };
    }

    public void CommitSessionToLifetime()
    {
        lifetime.totalScore += sessionScore;
        lifetime.totalHits += sessionHitCount;
        lifetime.totalPulls = sessionPullCount;

        SaveLifetime();
        ApplyLifetimeToPublic();
        ClearSession();
    }

    public void ClearSession()
    {
        sessionScore = 0;
        sessionHitCount = 0;
        sessionPullCount = 0;
    }

    private int GetImuScore()
    {
        return imuScoreOverride;
    }

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
