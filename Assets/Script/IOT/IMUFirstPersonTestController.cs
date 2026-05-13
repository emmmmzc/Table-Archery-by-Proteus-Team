using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class IMUFirstPersonTestController : MonoBehaviour
{
    public enum ImuAxis
    {
        X,
        Y,
        Z
    }

    [Header("References")]
    public IMUReceiver imuReceiver;
    public Camera targetCamera;
    public CharacterController characterController;

    [Header("IMU Look")]
    public bool useImuLook = true;
    public float gyroSensitivity = 1f;
    public float gyroDeadZone = 0.3f;
    [Range(0f, 1f)] public float gyroSmoothing = 0.25f;
    public ImuAxis yawAxis = ImuAxis.Y;
    public ImuAxis pitchAxis = ImuAxis.X;
    public ImuAxis rollAxis = ImuAxis.Z;
    public float yawSign = 1f;
    public float pitchSign = -1f;
    public float rollSign = 1f;
    public bool applyRollToCamera = false;
    public float pitchLimit = 80f;
    public float rollLimit = 45f;
    public float maxPacketDeltaSeconds = 0.05f;

    [Header("Drift Correction")]
    public bool calibrateGyroOnStart = true;
    public float gyroCalibrationSeconds = 1f;
    public float calibrationStillGyroThreshold = 3f;
    public float calibrationAccelerationTolerance = 0.25f;
    public float maxGyroDegreesPerSecond = 250f;
    public bool correctPitchDriftWhileStill = true;
    public float pitchDriftStillGyroThreshold = 1.5f;
    public float pitchDriftStillSeconds = 0.6f;
    public float pitchBiasCorrectionSpeed = 0.15f;

    [Header("Keyboard Movement")]
    public bool allowKeyboardMove = true;
    public float moveSpeed = 3f;
    public float sprintMultiplier = 2f;
    public float gravity = -20f;

    [Header("Debug")]
    public bool logState;

    private uint lastTimestampMs;
    private bool hasLastTimestamp;
    private float yaw;
    private float pitch;
    private float roll;
    private float verticalVelocity;
    private Vector3 filteredGyro;
    private Vector3 gyroBias;
    private Vector3 gyroCalibrationSum;
    private float gyroCalibrationTimer;
    private int gyroCalibrationSamples;
    private bool gyroCalibrated;
    private float pitchStillTimer;

    void Start()
    {
        if (imuReceiver == null)
            imuReceiver = FindAnyObjectByType<IMUReceiver>();
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;

        if (targetCamera != null)
        {
            pitch = NormalizeAngle(targetCamera.transform.localEulerAngles.x);
            roll = NormalizeAngle(targetCamera.transform.localEulerAngles.z);
        }

        gyroCalibrated = !calibrateGyroOnStart;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (useImuLook)
            UpdateImuLook();

        if (allowKeyboardMove)
            UpdateKeyboardMovement();

        if (WasRecenterPressed())
            Recenter();
    }

    private void UpdateImuLook()
    {
        if (imuReceiver == null || !imuReceiver.hasPacket)
            return;

        ImuPacket packet = imuReceiver.LatestPacket;
        if (hasLastTimestamp && packet.timestampMs == lastTimestampMs)
            return;

        float deltaSeconds = GetPacketDeltaSeconds(packet.timestampMs);
        lastTimestampMs = packet.timestampMs;
        hasLastTimestamp = true;

        if (!gyroCalibrated)
        {
            UpdateStartupGyroCalibration(packet.gyroscope, packet.acceleration, deltaSeconds);
            return;
        }

        UpdatePitchDriftCorrection(packet.gyroscope, packet.acceleration, deltaSeconds);

        Vector3 gyro = ClampGyro(packet.gyroscope - gyroBias);
        gyro = ApplyDeadZone(gyro);
        filteredGyro = Vector3.Lerp(filteredGyro, gyro, 1f - gyroSmoothing);
        gyro = filteredGyro;

        yaw += GetAxisValue(gyro, yawAxis) * gyroSensitivity * yawSign * deltaSeconds;
        pitch += GetAxisValue(gyro, pitchAxis) * gyroSensitivity * pitchSign * deltaSeconds;
        roll += GetAxisValue(gyro, rollAxis) * gyroSensitivity * rollSign * deltaSeconds;
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
        roll = Mathf.Clamp(roll, -rollLimit, rollLimit);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (targetCamera != null)
        {
            float cameraRoll = applyRollToCamera ? roll : 0f;
            targetCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, cameraRoll);
        }

        if (logState)
            Debug.Log($"[IMU FPS] gyro={gyro} bias={gyroBias} yaw={yaw:F1} pitch={pitch:F1} roll={roll:F1}");
    }

    private void UpdateStartupGyroCalibration(Vector3 rawGyro, Vector3 acceleration, float deltaSeconds)
    {
        bool gyroIsStill = rawGyro.magnitude <= calibrationStillGyroThreshold;
        bool accelerationLooksLikeGravity = Mathf.Abs(acceleration.magnitude - 1f) <= calibrationAccelerationTolerance;

        if (!gyroIsStill || !accelerationLooksLikeGravity)
        {
            gyroCalibrationSum = Vector3.zero;
            gyroCalibrationTimer = 0f;
            gyroCalibrationSamples = 0;
            return;
        }

        gyroCalibrationSum += rawGyro;
        gyroCalibrationTimer += deltaSeconds;
        gyroCalibrationSamples++;

        if (gyroCalibrationTimer < gyroCalibrationSeconds)
            return;

        gyroBias = gyroCalibrationSum / Mathf.Max(1, gyroCalibrationSamples);
        gyroCalibrated = true;
        filteredGyro = Vector3.zero;

        if (logState)
            Debug.Log($"[IMU FPS] gyro calibrated, bias={gyroBias}");
    }

    private void UpdatePitchDriftCorrection(Vector3 rawGyro, Vector3 acceleration, float deltaSeconds)
    {
        if (!correctPitchDriftWhileStill)
            return;

        bool gyroIsStill = rawGyro.magnitude <= pitchDriftStillGyroThreshold;
        bool accelerationLooksLikeGravity = Mathf.Abs(acceleration.magnitude - 1f) <= calibrationAccelerationTolerance;

        if (!gyroIsStill || !accelerationLooksLikeGravity)
        {
            pitchStillTimer = 0f;
            return;
        }

        pitchStillTimer += deltaSeconds;
        if (pitchStillTimer < pitchDriftStillSeconds)
            return;

        float rawPitchGyro = GetAxisValue(rawGyro, pitchAxis);
        float currentPitchBias = GetAxisValue(gyroBias, pitchAxis);
        float correctedPitchBias = Mathf.MoveTowards(
            currentPitchBias,
            rawPitchGyro,
            pitchBiasCorrectionSpeed * deltaSeconds
        );

        SetAxisValue(ref gyroBias, pitchAxis, correctedPitchBias);
    }

    private float GetPacketDeltaSeconds(uint timestampMs)
    {
        if (!hasLastTimestamp || timestampMs < lastTimestampMs)
            return Time.deltaTime;

        float delta = (timestampMs - lastTimestampMs) / 1000f;
        if (delta <= 0f)
            return Time.deltaTime;

        return Mathf.Min(delta, maxPacketDeltaSeconds);
    }

    private Vector3 ApplyDeadZone(Vector3 value)
    {
        value.x = Mathf.Abs(value.x) < gyroDeadZone ? 0f : value.x;
        value.y = Mathf.Abs(value.y) < gyroDeadZone ? 0f : value.y;
        value.z = Mathf.Abs(value.z) < gyroDeadZone ? 0f : value.z;
        return value;
    }

    private Vector3 ClampGyro(Vector3 value)
    {
        value.x = Mathf.Clamp(value.x, -maxGyroDegreesPerSecond, maxGyroDegreesPerSecond);
        value.y = Mathf.Clamp(value.y, -maxGyroDegreesPerSecond, maxGyroDegreesPerSecond);
        value.z = Mathf.Clamp(value.z, -maxGyroDegreesPerSecond, maxGyroDegreesPerSecond);
        return value;
    }

    private void UpdateKeyboardMovement()
    {
        if (characterController == null)
            return;

        Vector2 input = ReadMoveInput();
        float speed = IsSprintHeld() ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        characterController.Move(move * Time.deltaTime);
    }

    public void Recenter()
    {
        yaw = transform.eulerAngles.y;
        pitch = 0f;
        roll = 0f;
        filteredGyro = Vector3.zero;
        gyroBias = Vector3.zero;
        gyroCalibrationSum = Vector3.zero;
        gyroCalibrationTimer = 0f;
        gyroCalibrationSamples = 0;
        pitchStillTimer = 0f;
        gyroCalibrated = !calibrateGyroOnStart;
        hasLastTimestamp = false;

        if (targetCamera != null)
            targetCamera.transform.localRotation = Quaternion.identity;
    }

    private static float GetAxisValue(Vector3 value, ImuAxis axis)
    {
        switch (axis)
        {
            case ImuAxis.X:
                return value.x;
            case ImuAxis.Y:
                return value.y;
            case ImuAxis.Z:
                return value.z;
            default:
                return 0f;
        }
    }

    private static void SetAxisValue(ref Vector3 value, ImuAxis axis, float axisValue)
    {
        switch (axis)
        {
            case ImuAxis.X:
                value.x = axisValue;
                break;
            case ImuAxis.Y:
                value.y = axisValue;
                break;
            case ImuAxis.Z:
                value.z = axisValue;
                break;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;
        return angle;
    }

    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return Vector2.zero;

        Vector2 input = Vector2.zero;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        return Vector2.ClampMagnitude(input, 1f);
#else
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
    }

    private static bool IsSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }

    private static bool WasRecenterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
