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
    
    private PlayerEnergy playerEnergy;
    
    void Start()
    {
        playerEnergy = FindAnyObjectByType<PlayerEnergy>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        foreach (string tag in tagsThatDestroyThis)
        {
            if (collision.gameObject.CompareTag(tag))
            {
                // If hitting enemy projectile, add energy
                if (tag == "EnemyProjectile" && playerEnergy != null)
                {
                    playerEnergy.AddEnergy(energyGain);
                    Debug.Log("Energy gained!");
                }
                
                // If hitting zombie
                if (tag == "Zombie")
                {
                    ZombieController zombie = collision.gameObject.GetComponent<ZombieController>();
                    if (zombie != null) zombie.TakeDamage(1);
                    if (bloodEffect != null)
                        Instantiate(bloodEffect, transform.position, Quaternion.identity);
                    // Add energy and trigger popup when hitting zombie
                    if (playerEnergy != null)
                    {
                        playerEnergy.AddEnergy(energyGain);
                        playerEnergy.ShowAttackPopup(showExcellent: true);
                        Debug.Log("Energy gained! (Zombie)");
                    }
                }
                
                Destroy(gameObject);
                break;
            }
        }
    }
}