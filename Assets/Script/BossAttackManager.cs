using UnityEngine;
using System.Collections;

public class BossAttackManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject enemyProjectilePrefab;
    [SerializeField] private Transform player;

    [Header("Attack Timing")]
    [SerializeField] private float minInterval = 3f;
    [SerializeField] private float maxInterval = 6f;

    [Header("Burst Spawn Settings")]
    [SerializeField] private int projectileCount = 5;          // How many projectiles per attack
    [SerializeField] private float horizontalRange = 12f;      // Wide horizontal area
    [SerializeField] private float verticalRange = 3f;
    [SerializeField] private float spawnHeightOffset = 2f;
    [SerializeField] private float projectileSpeed = 8f;

    private bool isAttacking = false;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        while (true)
        {
            // Wait random time between attacks
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            if (!isAttacking)
            {
                PerformAttack();
            }
        }
    }

    void PerformAttack()
    {
        isAttacking = true;
        // Randomly choose which attack animation (1, 2, or 3)
        int attackIndex = Random.Range(1, 4);
        animator.SetInteger("AttackIndex", attackIndex);
        animator.SetTrigger("Attack");
    }

    // This method will be called by Animation Event (see Part 3)
    public void SpawnProjectileBurst()
    {
        for (int i = 0; i < projectileCount; i++)
        {
            SpawnSingleProjectile();
        }
    }

    void SpawnSingleProjectile()
    {
        // Random position within a box around the boss
        Vector3 randomOffset = new Vector3(
            Random.Range(-horizontalRange, horizontalRange),
            Random.Range(-verticalRange, verticalRange) + spawnHeightOffset,  // ← ADD OFFSET HERE
            Random.Range(-2f, 2f)
        );
        Vector3 spawnPos = transform.position + randomOffset;

        // Direction from spawn point to player
        Vector3 direction = (player.position - spawnPos).normalized;

        Quaternion projectileRotation = Quaternion.Euler(180f, 0f, 0f);
        GameObject proj = Instantiate(enemyProjectilePrefab, spawnPos,projectileRotation);
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = direction * projectileSpeed;

        Destroy(proj, 8f);
    }

    // Called by animation at the end to allow next attack
    public void OnAttackEnd()
    {
        isAttacking = false;
    }
}