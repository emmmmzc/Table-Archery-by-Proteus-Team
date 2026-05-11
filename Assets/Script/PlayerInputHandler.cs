using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Action Asset")] 
    [SerializeField] private InputActionAsset playerControls;

    [Header("Action Map Name Reference")] 
    [SerializeField] private string actionMapName = "Player";

    [Header("Action Name References")] 
    [SerializeField] private string movement = "Movement";
    [SerializeField] private string rotation = "Rotation";
    [SerializeField] private string jump = "jump";
    [SerializeField] private string sprint = "Sprint";
    [SerializeField] private string fire = "Fire";
    [SerializeField] private string aim = "Aim";
    [SerializeField] private string restart = "Restart";

    private InputAction movementAction;
    private InputAction rotationAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction fireAction;
    private InputAction aimAction;
    private InputAction restartAction;

    public Vector2 MovementInput { get; private set; }
    public Vector2 rotationInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool SprintTriggered { get; private set; }
    public bool FireTriggered { get; private set; }
    public bool AimTriggered { get; private set; }
    public bool RestartTriggered { get; private set; }

    private void Awake()
    {
        InputActionMap mapReference = playerControls.FindActionMap(actionMapName);

        movementAction = mapReference.FindAction(movement);
        rotationAction = mapReference.FindAction(rotation);
        jumpAction = mapReference.FindAction(jump);
        sprintAction = mapReference.FindAction(sprint);
        fireAction = mapReference.FindAction(fire);
        aimAction = mapReference.FindAction(aim);
        restartAction = mapReference.FindAction(restart);

        SubscribeActionValuesToInputEvents();
    }

    private void SubscribeActionValuesToInputEvents()
    {
        movementAction.performed += inputInfo => MovementInput = inputInfo.ReadValue<Vector2>();
        movementAction.canceled += inputInfo => MovementInput = Vector2.zero;

        rotationAction.performed += inputInfo => rotationInput = inputInfo.ReadValue<Vector2>();
        rotationAction.canceled += inputInfo => rotationInput = Vector2.zero;

        jumpAction.performed += inputInfo => JumpTriggered = true;
        jumpAction.canceled += inputInfo => JumpTriggered = false;

        sprintAction.performed += inputInfo => SprintTriggered = true;
        sprintAction.canceled += inputInfo => SprintTriggered = false;

        fireAction.performed += inputInfo => FireTriggered = true;
        fireAction.canceled += inputInfo => FireTriggered = false; 

        aimAction.performed += inputInfo => AimTriggered = true;
        aimAction.canceled += inputInfo => AimTriggered = false;

        restartAction.performed += inputInfo => RestartTriggered = true;
        restartAction.canceled += inputInfo => RestartTriggered = false;
    }

    public void ConsumeFire()
    {
        FireTriggered = false;
    }

    public void ConsumeAim()
    {
        AimTriggered = false;
    }

    public void InjectFire()
    {
        FireTriggered = true;
    }

    public void InjectAim()
    {
        AimTriggered = true;
    }

    private void OnEnable()
    {
        playerControls.FindActionMap(actionMapName).Enable();
    }

    private void OnDisable()
    {
        playerControls.FindActionMap(actionMapName).Disable();
    }
}
