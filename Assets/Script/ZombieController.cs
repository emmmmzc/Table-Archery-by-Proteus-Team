using UnityEngine;
using UnityEngine.AI;

public class ZombieController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Animator animator;
    public NavMeshAgent agent;

    [Header("Attack")]
    public float attackRange = 2.5f;      // Increased from 1.5
    public int attackDamage = 1;
    public float attackCooldown = 1f;
    private float nextAttackTime = 0f;

    [Header("Health")]
    public int maxHealth = 1;
    private int currentHealth;
    public GameObject bloodEffect;

    [Header("Audio")]
    public AudioClip zombieCreateSound;
    public AudioClip zombieAttackSound;
    public AudioClip zombieDamageSound;
    public AudioClip zombieDeathSound;

    private bool isDead = false;

    void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player").transform;
        if (animator == null) animator = GetComponent<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        currentHealth = maxHealth;
        AudioManager.Instance.Play("ZombieCreate");
        agent.updateRotation = true;
        agent.stoppingDistance = attackRange * 0.8f; // Agent stops just before attack range
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        
        // DEBUG: See distance in console (remove after testing)
        // if (distance < 3f) Debug.Log($"Zombie distance: {distance}");

        if (distance <= attackRange)
        {
            // Stop moving
            if (agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;
            animator.SetBool("isWalking", false);

            // Face player
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);

            // Attack with cooldown
            if (Time.time >= nextAttackTime)
            {
                animator.SetTrigger("Attack");
                Invoke(nameof(DealDamage), 0.3f);
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            // Move toward player
            if (agent.isOnNavMesh && agent.isStopped) agent.isStopped = false;
            agent.SetDestination(player.position);
            animator.SetBool("isWalking", true);
        }
    }

    void DealDamage()
    {
        if (isDead) return;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.TakeDamage(attackDamage);
        AudioManager.Instance.Play("ZombieAttack");
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;
        AudioManager.Instance.Play("ZombieDamage");
        if (bloodEffect != null)
            Instantiate(bloodEffect, transform.position + Vector3.up, Quaternion.identity);
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        isDead = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        if (animator != null) animator.SetTrigger("Die");
        GetComponent<Collider>().enabled = false;
        AudioManager.Instance.Play("ZombieDeath");
        Destroy(gameObject, 2f);
    }

    // Visualize attack range in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}