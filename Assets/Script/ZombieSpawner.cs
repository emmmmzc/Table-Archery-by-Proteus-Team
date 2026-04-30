using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject zombiePrefab;
    public Transform bossTransform;

    [Header("Spawn Area")]
    public float horizontalRange = 12f;
    public float depthRange = 5f;
    public float verticalOffset = 0f;

    [Header("Wave Settings")]
    public int totalWaves = 3;
    public int minZombiesPerWave = 2;
    public int maxZombiesPerWave = 5;
    public float timeBetweenWaves = 3f;
    public float spawnDelayWithinWave = 0.5f;

    [Header("Animation")]
    public float pointingAnimationDuration = 1.5f; // how long to wait after pointing before spawning

    private int currentWave = 0;
    private List<GameObject> spawnedZombies = new List<GameObject>();
    private Animator bossAnimator;
    private string pointingTrigger;

    public void StartWaves(Animator bossAnim, string pointingTrig)
    {
        bossAnimator = bossAnim;
        pointingTrigger = pointingTrig;
        currentWave = 0;
        StartCoroutine(WaveRoutine());
    }

    IEnumerator WaveRoutine()
    {
        while (currentWave < totalWaves)
        {
            currentWave++;
            Debug.Log($"Wave {currentWave} starting");

            // Play pointing animation on boss
            if (bossAnimator != null && !string.IsNullOrEmpty(pointingTrigger))
                bossAnimator.SetTrigger(pointingTrigger);

            // Wait for pointing animation to play (or fixed duration)
            yield return new WaitForSeconds(pointingAnimationDuration);

            // Spawn the wave
            yield return StartCoroutine(SpawnWave(currentWave));
            
            // Wait until all zombies in this wave are dead
            while (GetRemainingZombieCount() > 0)
                yield return null;
            
            if (currentWave < totalWaves)
            {
                Debug.Log($"Wave {currentWave} complete. Next wave in {timeBetweenWaves}s");
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }
        Debug.Log("All waves complete!");
        OnAllWavesComplete();
    }

    IEnumerator SpawnWave(int waveNumber)
    {
        int count = Random.Range(minZombiesPerWave, maxZombiesPerWave + 1);
        for (int i = 0; i < count; i++)
        {
            SpawnSingleZombie();
            yield return new WaitForSeconds(spawnDelayWithinWave);
        }
    }

    void SpawnSingleZombie()
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-horizontalRange, horizontalRange),
            verticalOffset,
            Random.Range(-depthRange, depthRange)
        );
        Vector3 spawnPos = bossTransform.position + randomOffset;

        // Snap to NavMesh
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            spawnPos = hit.position;

        Quaternion spawnRotation = Quaternion.Euler(0, 180, 0);
        GameObject zombie = Instantiate(zombiePrefab, spawnPos, spawnRotation);
        spawnedZombies.Add(zombie);
    }

    public int GetRemainingZombieCount()
    {
        spawnedZombies.RemoveAll(z => z == null);
        return spawnedZombies.Count;
    }

    void OnAllWavesComplete()
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.OnZombiesDefeated();
    }
}