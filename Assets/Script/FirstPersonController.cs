using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintMultiplier = 2.0f;

    [Header("Jump Parameters")]
    [SerializeField] private float jumpForce = 5.0f;
    [SerializeField] private float gravityMultiplier = 1.0f;

    [Header("Look Parameters")]
    [SerializeField] private float mouseSensitivity = 0.3f;
    [SerializeField] private float upDownLookRange = 100.0f;
    [SerializeField] private bool useMouseLook = true;

    [Header("Fire Parameters")]
    [SerializeField] private float fireForce = 5.0f;
    [SerializeField] private bool requireExcellentToFire = false;

    [Header("Bow Visuals")]
    [SerializeField] private Animator bowAnimator;
    [SerializeField] private string bowAimTriggerName = "shoot";
    [SerializeField] private string bowFireTriggerName = "fire";
    [SerializeField] private string bowShotFallbackStateName = "BowArmature|Shot";
    [SerializeField] private GameObject heldArrow;
    [SerializeField] private float heldArrowReloadDelay = 1f;
    [SerializeField] private float bowResetDelay = 0.35f;
    [SerializeField] private bool autoFindBowVisuals = true;

    [Header("Aim Mode")]
    [SerializeField] private float zoomFOV = 40f;        
    [SerializeField] private float normalFOV = 60f;      
    [SerializeField] private GameObject crosshairUI;     
    [SerializeField] private float zoomSpeed = 5f;       

    private bool isAiming = false;
    private float targetFOV;

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private PlayerEnergy playerEnergy;
    [SerializeField] private FitnessManager fitnessManager;
    [SerializeField] private MotorController motorController;

    private Vector3 currentMovement;
    private float verticalRotation;
    private Coroutine bowResetRoutine;
    private Coroutine heldArrowReloadRoutine;
    private float CurrentSpeed => walkSpeed * (playerInputHandler.SprintTriggered ? sprintMultiplier : 1.0f);


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        targetFOV = normalFOV;
        if (crosshairUI != null) 
        {
            crosshairUI.SetActive(false);
        }

        if (playerEnergy == null)
            playerEnergy = FindAnyObjectByType<PlayerEnergy>();
        if (fitnessManager == null)
            fitnessManager = FindAnyObjectByType<FitnessManager>();
        if (motorController == null)
            motorController = FindAnyObjectByType<MotorController>();

        ResolveBowVisualReferences();
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
        if (useMouseLook)
            HandleRotation(); 
        HandleAiming();
        HandleShooting();
        UpdateCameraZoom();
    }

    private Vector3 CalculateWorldDirection()
    {
        Vector3 inputDirection = new Vector3(playerInputHandler.MovementInput.x, 0.0f, playerInputHandler.MovementInput.y);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        return worldDirection.normalized;
    }

    /* -------------------------------- MOVEMENT & JUMPING --------------------------------- */
    private void HandleJumping()
    {
        if (characterController.isGrounded)
        {
            currentMovement.y = -0.5f; // Small downward force to keep the player grounded
            if (playerInputHandler.JumpTriggered)
            {
                currentMovement.y = jumpForce;
            }
        }
        else
        {
            currentMovement.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        Vector3 worldDirection = CalculateWorldDirection();
        currentMovement.x = worldDirection.x * CurrentSpeed;
        currentMovement.z = worldDirection.z * CurrentSpeed;

        HandleJumping();
        characterController.Move(currentMovement * Time.deltaTime);
    }



    /* --------------------------------- ROTATION & LOOKING --------------------------------- */
    private void ApplyHorizontalRotation(float rotationAmount)
    {
        transform.Rotate(0, rotationAmount, 0);
    }

    private void ApplyVerticalRotation(float rotationAmount)
    {
        verticalRotation = Mathf.Clamp(verticalRotation - rotationAmount, -upDownLookRange, upDownLookRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    private void HandleRotation()
    {
        float mouseXRotation = playerInputHandler.rotationInput.x * mouseSensitivity;
        float mouseYRotation = playerInputHandler.rotationInput.y * mouseSensitivity;

        ApplyHorizontalRotation(mouseXRotation);
        ApplyVerticalRotation(mouseYRotation);
    }


    
    /* --------------------------------- FIRING --------------------------------- */
    private void HandleAiming()
    {
        if (playerInputHandler.AimTriggered && !isAiming)
        {
            isAiming = true;
            AudioManager.Instance.Play("Aiming");
            targetFOV = zoomFOV;
            if (crosshairUI != null) crosshairUI.SetActive(true);
            BeginBowDrawVisual();
            if (motorController != null)
                motorController.BeginForceWindow();
            if (playerInputHandler != null)
                playerInputHandler.ConsumeAim();
        }
    }

    private void HandleShooting()
    {
        if (isAiming && playerInputHandler.FireTriggered)
        {
            int motorScore = 0;
            if (fitnessManager != null && !fitnessManager.useRealMotor)
            {
                motorScore = fitnessManager.defaultMotorScore;
            }
            else if (motorController != null)
            {
                motorScore = motorController.EndForceWindow();
            }

            FitnessHitResult hitResult = fitnessManager != null
                ? fitnessManager.OnHit(motorScore)
                : new FitnessHitResult { isExcellent = true, totalScore = motorScore, grade = "Excellent" };

            if (playerEnergy != null)
                playerEnergy.ShowAttackPopup(hitResult.isExcellent, hitResult.totalScore);

            if (!hitResult.isExcellent)
            {
                if (requireExcellentToFire)
                {
                    isAiming = false;
                    targetFOV = normalFOV;
                    if (crosshairUI != null) crosshairUI.SetActive(false);
                    CancelBowDrawVisual();
                    if (playerInputHandler != null)
                        playerInputHandler.ConsumeFire();
                    return;
                }
            }

            FireBowVisual();

            // Spawn projectile (same code you already have)
            Vector3 spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * 0.5f;
            Quaternion arrowRotation = mainCamera.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            GameObject projectile = Instantiate(projectilePrefab, spawnPosition, arrowRotation);
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(mainCamera.transform.forward * fireForce, ForceMode.Impulse);
            Destroy(projectile, 5f);
            HideHeldArrowThenReload();
            ResetBowVisualAfterDelay();

            // Exit aim mode after shooting
            isAiming = false;
            targetFOV = normalFOV;
            if (crosshairUI != null) crosshairUI.SetActive(false);
            
            // Consume the fire input so it doesn't fire again next frame
            if (playerInputHandler != null)
                playerInputHandler.ConsumeFire();
        }
    }

    private void UpdateCameraZoom()
    {
        // Smoothly adjust FOV
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, zoomSpeed * Time.deltaTime);
    }

    private void ResolveBowVisualReferences()
    {
        if (!autoFindBowVisuals)
            return;

        Transform[] searchRoots =
        {
            mainCamera != null ? mainCamera.transform : null,
            transform
        };

        foreach (Transform searchRoot in searchRoots)
        {
            if (searchRoot == null)
                continue;

            if (bowAnimator == null)
            {
                Animator[] animators = searchRoot.GetComponentsInChildren<Animator>(true);
                foreach (Animator animator in animators)
                {
                    if (animator.name.Contains("Bow") ||
                        (animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.name.Contains("Bow")))
                    {
                        bowAnimator = animator;
                        break;
                    }
                }
            }

            if (heldArrow == null)
                heldArrow = FindHeldArrow(searchRoot);

            if (bowAnimator != null && heldArrow != null)
                return;
        }
    }

    private void BeginBowDrawVisual()
    {
        if (bowAnimator == null)
            return;

        if (bowResetRoutine != null)
            StopCoroutine(bowResetRoutine);

        bowAnimator.speed = 1f;
        SetAnimatorTriggerIfExists(bowAnimator, bowAimTriggerName);
    }

    private void FireBowVisual()
    {
        if (bowAnimator == null)
            return;

        if (!SetAnimatorTriggerIfExists(bowAnimator, bowFireTriggerName) &&
            !string.IsNullOrEmpty(bowShotFallbackStateName))
            bowAnimator.Play(bowShotFallbackStateName, 0, 0f);
    }

    private void CancelBowDrawVisual()
    {
        if (bowAnimator != null)
            bowAnimator.Rebind();
    }

    private void ResetBowVisualAfterDelay()
    {
        if (bowAnimator == null)
            return;

        if (bowResetRoutine != null)
            StopCoroutine(bowResetRoutine);

        bowResetRoutine = StartCoroutine(ResetBowVisualRoutine());
    }

    private void HideHeldArrowThenReload()
    {
        if (heldArrow == null)
            return;

        if (heldArrowReloadRoutine != null)
            StopCoroutine(heldArrowReloadRoutine);

        heldArrowReloadRoutine = StartCoroutine(HeldArrowReloadRoutine());
    }

    private System.Collections.IEnumerator ResetBowVisualRoutine()
    {
        yield return new WaitForSeconds(bowResetDelay);

        if (bowAnimator != null)
            bowAnimator.Rebind();

        bowResetRoutine = null;
    }

    private System.Collections.IEnumerator HeldArrowReloadRoutine()
    {
        heldArrow.SetActive(false);
        yield return new WaitForSeconds(heldArrowReloadDelay);

        if (heldArrow != null)
            heldArrow.SetActive(true);

        heldArrowReloadRoutine = null;
    }

    private static bool SetAnimatorTriggerIfExists(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
                return true;
            }
        }

        return false;
    }

    private static GameObject FindHeldArrow(Transform searchRoot)
    {
        Transform[] children = searchRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name.Contains("Arrow"))
                return child.gameObject;
        }

        return null;
    }
}
