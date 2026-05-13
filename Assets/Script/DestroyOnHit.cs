using UnityEngine;

public class DestroyOnHit : MonoBehaviour
{
    [Header("Tags that destroy this projectile")]
    public string[] tagsThatDestroyThis;
    
    [Header("Effects")]
    public GameObject explosionEffect;
    public GameObject bloodEffect;
    
    [Header("Energy")]
    public int energyGain = 1;           // How much energy per destroyed enemy projectile

    [Header("Boss")]
    public int bossDamage = 1;
    
    private PlayerEnergy playerEnergy;
    
    void Start()
    {
        playerEnergy = FindAnyObjectByType<PlayerEnergy>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        BossHealth boss = collision.gameObject.GetComponentInParent<BossHealth>();
        if (boss != null)
        {
            boss.TakeDamage(bossDamage);
            if (bloodEffect != null)
                Instantiate(bloodEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
            return;
        }

        foreach (string tag in tagsThatDestroyThis)
        {
            if (collision.gameObject.CompareTag(tag))
            {
                if (tag == "EnemyProjectile")
                {
                    if (explosionEffect != null)
                        Instantiate(explosionEffect, transform.position, Quaternion.identity);
                    if (playerEnergy != null)
                        playerEnergy.AddEnergy(energyGain);
                }
                else if (tag == "Zombie")
                {
                    ZombieController zombie = collision.gameObject.GetComponent<ZombieController>();
                    if (zombie != null) zombie.TakeDamage(1);
                    if (bloodEffect != null)
                        Instantiate(bloodEffect, transform.position, Quaternion.identity);
                }
                
                Destroy(gameObject);
                break;
            }
        }
    }
}
