using UnityEngine;

public class BossShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject enemyProjectilePrefab;
    [SerializeField] private Transform player;
    [SerializeField] private Transform boss;

    [Header("Shooting Settings")]
    [SerializeField] private float shootInterval = 1.5f;
    [SerializeField] private float projectileSpeed = 5f;

    [Header("Spawn Randomization (Horizontal Area)")]
    [SerializeField] private float horizontalRange = 5f;   // Wide horizontal spread (left/right from boss)
    [SerializeField] private float verticalRange = 2f;     // Small vertical variation
    [SerializeField] private float depthRange = 3f;        // Forward/backward variation

    [Header("Audio")]
    [SerializeField] private AudioClip bossShootSound;

    void Start()
    {
        if (boss == null) boss = transform;
        InvokeRepeating("Shoot", 1f, shootInterval);
    }

    void Shoot()
    {
        // 1. Random spawn position within a box around the boss
        Vector3 randomOffset = new Vector3(
            Random.Range(-horizontalRange, horizontalRange),
            Random.Range(-verticalRange, verticalRange),
            Random.Range(-depthRange, depthRange)
        );
        Vector3 spawnPosition = boss.position + randomOffset;

        // 2. Direction from spawn point TO the player (exactly aimed)
        Vector3 direction = (player.position - spawnPosition).normalized;

        // 3. Create projectile and set its velocity toward player
        Quaternion projectileRotation = Quaternion.Euler(180f, 0f, 0f);
        GameObject proj = Instantiate(enemyProjectilePrefab, spawnPosition, projectileRotation);
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = direction * projectileSpeed;
        }
        //sound effect
        AudioManager.Instance.Play("BossShoot");    

        Destroy(proj, 8f);
    }
}