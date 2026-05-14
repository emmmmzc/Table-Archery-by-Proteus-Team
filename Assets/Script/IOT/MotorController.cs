using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
using System.IO.Ports;
#endif

public class MotorController : MonoBehaviour
{
    private const int StatusFrameLength = 40;
    private const byte FrameHeader = 0x64;

    private const byte EndFlag1 = 0x0D;
    private const byte EndFlag2 = 0x0A;
    private const int FixedBaudRate = 57600;

    [Header("Connection")]
    public string portName = "COM7";
    public float staleSeconds = 3f;

    [Header("Motor Config")]
    public ushort springBaseForce = 100;
    public ushort springPullLimit = 100;
    public byte springDistance = 150;
    public byte springModeCode = 0x02;
    public byte springSpeedCoeff = 0;
    public bool clearPullCountOnInit = true;
    public byte pullCountClearFlag = 1;

    [Header("Raw Commands")]
    public bool useRawCommands = true;
    public string powerOnRawHex = "640002AA00000000000000000000000000000000000000000000000000BD0D0A";
    public string powerOffRawHex = "6400025500000000000000000000000000000000000000000000000000590D0A";
    public string defaultSpringRawHex = "64000102012C07D0641E00000000000000000000000000000000000000470D0A";
    public string releaseProtectionRawHex = "6400A00102030000000000000000000000000000000000000000000000A70D0A";
    // public string clearPullCountRawHex = "";

    [Header("Startup Delay")]
    public float powerOnDelaySeconds = 0.2f;
    public float defaultModeDelaySeconds = 0.1f;
    public float releaseProtectionDelaySeconds = 0.3f;

    [Header("Enable Package")]
    public float enablePackageIntervalSeconds = 1f;

    [Header("Motor Trigger Input")]
    public bool useMotorDistanceTrigger = true;
    public float motorChargeStartDistance = 80f;
    public float motorFireReleaseDistance = 25f;
    public PlayerInputHandler inputHandler;

    [Header("Scoring")]
    public int disconnectedMotorScore = 20;
    [Tooltip("Maps integrated work (force × speed × Δt) to score.")]
    public float workToScoreMultiplier = 10f;
    public int maxWorkScore = 800;
    public bool useQualityScore = false;
    public int maxQualityScore = 1200;
    public float qualityForceMax = 45f;
    public float qualitySpeedMax = 12f;
    public float qualityDistanceMax = 35f;
    public float qualityForceWeight = 0.45f;
    public float qualitySpeedWeight = 0.35f;
    public float qualityDistanceWeight = 0.2f;

    [Header("Debug")]
    public bool logLifecycle = true;
    public bool logRawTx = true;
    public bool logRawRx = true;
    public bool logDecodedStatus = true;

#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
    private SerialPort serialPort;
#endif
    private bool initialized;
    private float lastReceiveTime;
    private readonly List<byte> receiveBuffer = new List<byte>(256);

    private float motorForceKg;
    private float motorSpeedCmPerSec;
    private float motorDistanceCm;
    private int motorPullCount;
    private bool motorChargeReady;

    private bool isTracking;
    private float trackingStartTime;
    private float accumulatedWork;
    private float lastTrackingTime;
    private float totalQualityScore;

    private Coroutine enablePackageRoutine;
    private Coroutine resumeAfterPauseRoutine;
    private Coroutine applySpringSettingsRoutine;
    private Coroutine enablePackageSendRoutine;

    public float MotorForceKg => motorForceKg;
    public float MotorSpeedCmPerSec => motorSpeedCmPerSec;
    public float MotorDistanceCm => motorDistanceCm;
    public int MotorPullCount => motorPullCount;

    /* ----------------------------- Lifecycle ----------------------------- */
    void Start()
    {
        if (logLifecycle)
            Debug.Log($"[IOT][Motor] Start on {gameObject.name} (port={portName})");
        ReloadSettingsFromPrefs();
        Initialize(portName);
    }

    void Update()
    {
        PollIncoming();
        UpdateTracking();
    }

    /* ---------------------------- Connection ----------------------------- */
    public void Initialize(string port)
    {
        if (initialized)
        {
            if (logLifecycle)
                Debug.Log("[IOT][Motor] Initialize ignored: already initialized.");
            return;
        }

        portName = port;

        if (logLifecycle)
            Debug.Log($"[IOT][Motor] Initialize requested (port={portName}).");

#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        try
        {
            serialPort = new SerialPort(portName, FixedBaudRate)
            {
                ReadTimeout = 5,
                WriteTimeout = 100,
                NewLine = "\n"
            };
            serialPort.Open();
            initialized = true;
            lastReceiveTime = 0f;
            Debug.Log($"[IOT][Motor] Connected: {portName} @ {FixedBaudRate}");

            StartCoroutine(SendStartupSequence());
                
        }
        catch (Exception ex)
        {
            initialized = false;
            Debug.LogWarning($"[IOT][Motor] Connect failed ({portName}): {ex.Message}");
        }
#else
        initialized = false;
        Debug.LogWarning("[IOT][Motor] Serial disabled. Define USE_SERIAL_PORTS to enable System.IO.Ports.");
#endif
    }

    public void Shutdown()
    {
        initialized = false;
        StopEnablePackageLoop();
        CancelApplySpringSettingsRoutine();
        CancelResumeAfterPauseRoutine();

#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        if (serialPort == null)
            return;

        try
        {
            if (serialPort.IsOpen)
                serialPort.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IOT][Motor] Close failed: {ex.Message}");
        }
        finally
        {
            serialPort.Dispose();
            serialPort = null;
        }
#endif
    }

    /* ------------------------------ Commands ----------------------------- */
    public void SendPowerOn()
    {
        if (string.IsNullOrWhiteSpace(powerOnRawHex))
        {
            Debug.LogWarning("[IOT][Motor] powerOnRawHex is empty; send aborted.");
            return;
        }

        SendRawHex(powerOnRawHex);
    }

    public void SendPowerOff()
    {
        if (string.IsNullOrWhiteSpace(powerOffRawHex))
        {
            Debug.LogWarning("[IOT][Motor] powerOffRawHex is empty; send aborted.");
            return;
        }

        StopEnablePackageLoop();
        CancelResumeAfterPauseRoutine();
        CancelApplySpringSettingsRoutine();

        SendRawHex(powerOffRawHex);
    }

    public void SendRawHex(string hex)
    {
        if (!TryParseHex(hex, out byte[] bytes))
        {
            Debug.LogWarning("[IOT][Motor] Raw hex invalid; send aborted.");
            return;
        }

        SendBytes(bytes);
    }

    public void SendSpringMode(ushort baseForce, ushort pullLimit, byte distance)
    {
        SendCommand(MotorCommandPacket.CreateSpringModePacket(baseForce, pullLimit, distance, springModeCode, springSpeedCoeff));
    }

    public void SendSpringModeWithClear(ushort baseForce, ushort pullLimit, byte distance, byte clearFlag)
    {
        SendCommand(MotorCommandPacket.CreateSpringModePacket(baseForce, pullLimit, distance, springModeCode, springSpeedCoeff, clearFlag));
    }

    public void SendReleaseProtection()
    {
        if (string.IsNullOrWhiteSpace(releaseProtectionRawHex))
        {
            Debug.LogWarning("[IOT][Motor] releaseProtectionRawHex is empty; send aborted.");
            return;
        }

        SendRawHex(releaseProtectionRawHex);
    }

    public void ApplySpringSettings()
    {
        if (!initialized)
            return;

        if (applySpringSettingsRoutine != null)
            StopCoroutine(applySpringSettingsRoutine);

        applySpringSettingsRoutine = StartCoroutine(ApplySpringSettingsRoutine());
    }

    /// <summary>
    /// Stop delayed resume packets so they cannot fire after the player opens pause again (race with SendPowerOff).
    /// </summary>
    public void CancelResumeAfterPauseRoutine()
    {
        if (resumeAfterPauseRoutine == null)
            return;

        StopCoroutine(resumeAfterPauseRoutine);
        resumeAfterPauseRoutine = null;
    }

    private void CancelApplySpringSettingsRoutine()
    {
        if (applySpringSettingsRoutine == null)
            return;

        StopCoroutine(applySpringSettingsRoutine);
        applySpringSettingsRoutine = null;
    }

    public void ResumeAfterPause()
    {
        if (!initialized)
            return;

        // Match SendStartupSequence timing: motor firmware often ignores back-to-back packets.
        if (resumeAfterPauseRoutine != null)
        {
            StopCoroutine(resumeAfterPauseRoutine);
            resumeAfterPauseRoutine = null;
        }

        resumeAfterPauseRoutine = StartCoroutine(ResumeAfterPauseRoutine());
    }

    private IEnumerator ResumeAfterPauseRoutine()
    {
        ReloadSettingsFromPrefs();

        if (powerOnDelaySeconds > 0f)
            yield return new WaitForSeconds(powerOnDelaySeconds);

        SendPowerOn();

        if (defaultModeDelaySeconds > 0f)
            yield return new WaitForSeconds(defaultModeDelaySeconds);

        SendSpringMode(springBaseForce, springPullLimit, springDistance);

        if (releaseProtectionDelaySeconds > 0f)
            yield return new WaitForSeconds(releaseProtectionDelaySeconds);

        SendReleaseProtection();

        // If startup sequence never ran this scene (e.g. scene name gate), keep-alive may never have started.
        StartEnablePackageLoop();

        resumeAfterPauseRoutine = null;
    }

    public void ReloadSettingsFromPrefs()
    {
        springBaseForce = (ushort)PlayerPrefs.GetInt("MotorBaseForce", 300);
        springDistance = (byte)PlayerPrefs.GetInt("MotorDistance", 30);
    }

    /* ------------------------- Scoring & Tracking ------------------------ */
    public int GetMotorScore()
    {
        if (!IsConnected())
            return disconnectedMotorScore;

        if (useQualityScore)
        {
            float forceScore = Mathf.Clamp01(motorForceKg / qualityForceMax) * 100f;
            float speedScore = Mathf.Clamp01(motorSpeedCmPerSec / qualitySpeedMax) * 90f;
            float romScore = Mathf.Clamp01(motorDistanceCm / qualityDistanceMax) * 85f;
            float quality = forceScore * qualityForceWeight + speedScore * qualitySpeedWeight + romScore * qualityDistanceWeight;
            return Mathf.RoundToInt(Mathf.Clamp(quality, 0f, 100f));
        }

        return Mathf.Max(0, Mathf.RoundToInt(motorForceKg));
    }

    public void BeginForceWindow()
    {
        isTracking = true;
        trackingStartTime = Time.time;
        lastTrackingTime = Time.time;
        accumulatedWork = 0f;
    }

    public int EndForceWindow()
    {
        if (!isTracking)
            return 0;

        UpdateTracking();
        isTracking = false;

        if (useQualityScore)
        {
            int pullQuality = GetMotorScore();
            totalQualityScore += pullQuality * (motorForceKg / 10f);

            int qualityFinalScore = Mathf.RoundToInt(totalQualityScore);
            qualityFinalScore = Mathf.Clamp(qualityFinalScore, 0, maxQualityScore);

            totalQualityScore = 0f;
            return qualityFinalScore;
        }

        int finalScore = Mathf.RoundToInt(accumulatedWork * workToScoreMultiplier);
        finalScore = Mathf.Clamp(finalScore, 0, maxWorkScore);
        accumulatedWork = 0f;
        return finalScore;
    }

    public int GetPullCount()
    {
        return motorPullCount;
    }

    /* ---------------------------- Serial RX ------------------------------ */
    private bool IsConnected()
    {
        if (!initialized)
            return false;

#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        if (serialPort == null || !serialPort.IsOpen)
            return false;
#endif
        return Time.time - lastReceiveTime <= staleSeconds;
    }

    private void PollIncoming()
    {
#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        if (!initialized || serialPort == null || !serialPort.IsOpen)
            return;

        try
        {
            int available = serialPort.BytesToRead;
            if (available <= 0)
                return;

            byte[] chunk = new byte[available];
            int read = serialPort.Read(chunk, 0, chunk.Length);
            if (read <= 0)
                return;

            for (int i = 0; i < read; i++)
                receiveBuffer.Add(chunk[i]);

            ParseStatusFrames();
        }
        catch (TimeoutException)
        {
            // Keep latest valid value.
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IOT][Motor] Read failed: {ex.Message}");
        }
#endif
    }

    private void UpdateTracking()
    {
        if (!isTracking)
            return;

        float now = Time.time;
        float deltaTime = now - lastTrackingTime;
        lastTrackingTime = now;

        if (deltaTime <= 0f)
            return;

        float workThisTick = motorForceKg * motorSpeedCmPerSec * deltaTime;
        accumulatedWork += workThisTick;
    }

    private void ParseStatusFrames()
    {
        while (receiveBuffer.Count >= StatusFrameLength)
        {
            int headerIndex = receiveBuffer.IndexOf(FrameHeader);
            if (headerIndex < 0)
            {
                receiveBuffer.Clear();
                return;
            }

            if (headerIndex > 0)
                receiveBuffer.RemoveRange(0, headerIndex);

            if (receiveBuffer.Count < StatusFrameLength)
                return;

            if (receiveBuffer[38] != EndFlag1 || receiveBuffer[39] != EndFlag2)
            {
                receiveBuffer.RemoveAt(0);
                continue;
            }

            byte[] frame = receiveBuffer.GetRange(0, StatusFrameLength).ToArray();
            receiveBuffer.RemoveRange(0, StatusFrameLength);
            ApplyStatusFrame(frame);
        }
    }

    private void ApplyStatusFrame(byte[] frame)
    {
        if (logRawRx)
            Debug.Log($"[IOT][Motor] RX: {ToHex(frame)}");

        float forceKg = (frame[3] << 8) | frame[4];

        int speedRaw = (frame[5] << 8) | frame[6];
        if (speedRaw > 60000)
            speedRaw = 65535 - speedRaw;

        int distanceRaw = (frame[7] << 8) | frame[8];
        if (distanceRaw > 60000)
            distanceRaw = 65535 - distanceRaw;

        int countRaw = (frame[9] << 8) | frame[10];

        motorForceKg = forceKg;
        motorSpeedCmPerSec = speedRaw;
        motorDistanceCm = distanceRaw;
        motorPullCount = countRaw;
        lastReceiveTime = Time.time;

        if (logDecodedStatus)
            Debug.Log($"[IOT][Motor] Status force={motorForceKg} speed={motorSpeedCmPerSec} dist={motorDistanceCm} count={motorPullCount}");

        UpdateMotorInputTriggers();
    }

    /* -------------------------- Motor -> Input --------------------------- */
    private void UpdateMotorInputTriggers()
    {
        if (!useMotorDistanceTrigger)
            return;

        if (inputHandler == null)
            inputHandler = FindAnyObjectByType<PlayerInputHandler>();

        if (inputHandler == null)
            return;

        if (!motorChargeReady && motorDistanceCm >= motorChargeStartDistance)
        {
            motorChargeReady = true;
            inputHandler.InjectAim();
        }
        else if (motorChargeReady && motorDistanceCm <= motorFireReleaseDistance)
        {
            motorChargeReady = false;
            // Drive the same trigger path as the mouse input.
            inputHandler.InjectFire();
        }
    }

    private IEnumerator SendStartupSequence()
    {
        if (SceneManager.GetActiveScene().name == "menu")
            yield break;

        if (powerOnDelaySeconds > 0f)
            yield return new WaitForSeconds(powerOnDelaySeconds);

        SendPowerOn();

        if (defaultModeDelaySeconds > 0f)
            yield return new WaitForSeconds(defaultModeDelaySeconds);

        SendSpringMode(springBaseForce, springPullLimit, springDistance);

        if (releaseProtectionDelaySeconds > 0f)
            yield return new WaitForSeconds(releaseProtectionDelaySeconds);

        SendReleaseProtection();

        StartEnablePackageLoop();
    }

    private void SendEnablePackage()
    {
        if (enablePackageSendRoutine != null)
            return;

        enablePackageSendRoutine = StartCoroutine(SendEnablePackageRoutine());
    }

    private IEnumerator ApplySpringSettingsRoutine()
    {
        SendSpringMode(springBaseForce, springPullLimit, springDistance);
        applySpringSettingsRoutine = null;
        yield break;
    }

    private IEnumerator SendEnablePackageRoutine()
    {
        SendSpringMode(springBaseForce, springPullLimit, springDistance);
        enablePackageSendRoutine = null;
        yield break;
    }

    private void StartEnablePackageLoop()
    {
        if (enablePackageRoutine != null)
            return;

        enablePackageRoutine = StartCoroutine(EnablePackageLoop());
    }

    private void StopEnablePackageLoop()
    {
        if (enablePackageRoutine == null)
        {
            if (enablePackageSendRoutine != null)
            {
                StopCoroutine(enablePackageSendRoutine);
                enablePackageSendRoutine = null;
            }
            return;
        }
        else
        {
            StopCoroutine(enablePackageRoutine);
            enablePackageRoutine = null;
        }

        if (enablePackageSendRoutine != null)
        {
            StopCoroutine(enablePackageSendRoutine);
            enablePackageSendRoutine = null;
        }
    }

    private IEnumerator EnablePackageLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(enablePackageIntervalSeconds);
            SendEnablePackage();
        }
    }

    /* ------------------------------ Helpers ------------------------------ */
    private void SendCommand(MotorCommandPacket packet)
    {
#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        try
        {
            byte[] bytes = packet.GetBytes();
            SendBytes(bytes);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IOT][Motor] Send failed: {ex.Message}");
        }
#endif
    }

    private void SendBytes(byte[] bytes)
    {
#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        if (!initialized || serialPort == null || !serialPort.IsOpen)
            return;

        if (bytes == null || bytes.Length == 0)
            return;

        if (logRawTx)
            Debug.Log($"[IOT][Motor] TX: {ToHex(bytes)}");

        serialPort.Write(bytes, 0, bytes.Length);
#endif
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        string cleaned = hex.Replace(" ", string.Empty)
            .Replace("\t", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        if (cleaned.Length % 2 != 0)
            return false;

        bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            string token = cleaned.Substring(i * 2, 2);
            if (!byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                return false;
        }

        return true;
    }

    private static string ToHex(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        string[] parts = new string[data.Length];
        for (int i = 0; i < data.Length; i++)
            parts[i] = data[i].ToString("X2");

        return string.Join(" ", parts);
    }

    void OnDestroy()
    {
        Shutdown();
    }

    /* --------------------------- Packet Builder -------------------------- */
    private class MotorCommandPacket
    {
        private const byte DefaultTargetMotor1 = 0x00;
        private readonly byte[] packet = new byte[32];

        private MotorCommandPacket()
        {
            packet[0] = 0x64;
            packet[30] = 0x0D;
            packet[31] = 0x0A;
        }

        public static MotorCommandPacket CreatePowerOnPacket()
        {
            var cmd = new MotorCommandPacket();
            cmd.packet[1] = DefaultTargetMotor1;
            cmd.packet[2] = 0x02;
            cmd.packet[3] = 0xAA;
            return cmd;
        }

        public static MotorCommandPacket CreatePowerOffPacket()
        {
            var cmd = new MotorCommandPacket();
            cmd.packet[1] = DefaultTargetMotor1;
            cmd.packet[2] = 0x02;
            cmd.packet[3] = 0x55;
            return cmd;
        }

        public static MotorCommandPacket CreateSpringModePacket(ushort baseForce, ushort pullLimit, byte distance, byte modeCode, byte speedCoeff)
        {
            var cmd = new MotorCommandPacket();
            cmd.packet[1] = DefaultTargetMotor1;
            cmd.packet[2] = 0x01;
            cmd.packet[3] = modeCode;

            cmd.packet[4] = (byte)(baseForce >> 8);
            cmd.packet[5] = (byte)(baseForce & 0xFF);
            cmd.packet[6] = (byte)(pullLimit >> 8);
            cmd.packet[7] = (byte)(pullLimit & 0xFF);
            cmd.packet[8] = (byte)Mathf.Clamp(speedCoeff, 0, 255);
            cmd.packet[9] = distance;

            return cmd;
        }

        public static MotorCommandPacket CreateSpringModePacket(ushort baseForce, ushort pullLimit, byte distance, byte modeCode, byte speedCoeff, byte pullCountClearFlag)
        {
            var cmd = CreateSpringModePacket(baseForce, pullLimit, distance, modeCode, speedCoeff);
            cmd.packet[11] = pullCountClearFlag;
            return cmd;
        }

        private void CalculateCRC()
        {
            byte crc = 0;
            for (int i = 0; i < 29; i++)
            {
                byte inByte = packet[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    byte mix = (byte)((crc ^ inByte) & 0x01);
                    crc >>= 1;
                    if (mix != 0)
                        crc ^= 0x8C;
                    inByte >>= 1;
                }
            }
            packet[29] = crc;
        }

        public byte[] GetBytes()
        {
            CalculateCRC();
            return packet;
        }
    }
}
