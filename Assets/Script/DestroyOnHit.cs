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
    private FitnessManager fitnessManager;
    
    void Start()
    {
        playerEnergy = FindAnyObjectByType<PlayerEnergy>();
        fitnessManager = FindAnyObjectByType<FitnessManager>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        foreach (string tag in tagsThatDestroyThis)
        {
            if (collision.gameObject.CompareTag(tag))
            {
                if (tag == "EnemyProjectile" || tag == "Zombie")
                {
                    if (explosionEffect != null)
                        Instantiate(explosionEffect, transform.position, Quaternion.identity);
                }
                {
                    ZombieController zombie = collision.gameObject.GetComponent<ZombieController>();
                    if (zombie != null) zombie.TakeDamage(1);
                    if (bloodEffect != null)
                        Instantiate(bloodEffect, transform.position, Quaternion.identity);

                    if (playerEnergy != null)
                    {
                        playerEnergy.AddEnergy(energyGain);
                        FitnessHitResult hitResult = fitnessManager != null
                            ? fitnessManager.OnHit()
                            : new FitnessHitResult { isExcellent = true, totalScore = 0, grade = "Excellent" };

                        playerEnergy.ShowAttackPopup(showExcellent: hitResult.isExcellent);
                        Debug.Log($"Energy gained! Grade: {hitResult.grade}, Score: {hitResult.totalScore}");
                    }
                }
                
                Destroy(gameObject);
                break;
            }
        }
    }
}