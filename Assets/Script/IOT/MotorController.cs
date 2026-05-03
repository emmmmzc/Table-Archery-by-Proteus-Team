using System;
using System.Collections.Generic;
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
    public string portName = "COM3";
    public bool autoConnect = true;
    public float staleSeconds = 3f;

    [Header("Motor Config")]
    public bool autoPowerOn = true;
    public ushort springBaseForce = 50;
    public ushort springPullLimit = 100;
    public byte springDistance = 150;
    public bool clearPullCountOnInit = true;
    public byte pullCountClearFlag = 1;

    [Header("Scoring")]
    public int disconnectedMotorScore = 20;

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

    public float MotorForceKg => motorForceKg;
    public float MotorSpeedCmPerSec => motorSpeedCmPerSec;
    public float MotorDistanceCm => motorDistanceCm;
    public int MotorPullCount => motorPullCount;

    void Start()
    {
        if (autoConnect)
            Initialize(portName);
    }

    void Update()
    {
        PollIncoming();
    }

    public void Initialize(string port)
    {
        if (initialized)
            return;

        portName = port;

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

            if (autoPowerOn)
            {
                SendPowerOn();
                if (clearPullCountOnInit)
                    SendSpringModeWithClear(springBaseForce, springPullLimit, springDistance, pullCountClearFlag);
                else
                    SendSpringMode(springBaseForce, springPullLimit, springDistance);
            }
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

    public void SendPowerOn()
    {
        SendCommand(MotorCommandPacket.CreatePowerOnPacket());
    }

    public void SendPowerOff()
    {
        SendCommand(MotorCommandPacket.CreatePowerOffPacket());
    }

    public void SendSpringMode(ushort baseForce, ushort pullLimit, byte distance)
    {
        SendCommand(MotorCommandPacket.CreateSpringModePacket(baseForce, pullLimit, distance));
    }

    public void SendSpringModeWithClear(ushort baseForce, ushort pullLimit, byte distance, byte clearFlag)
    {
        SendCommand(MotorCommandPacket.CreateSpringModePacket(baseForce, pullLimit, distance, clearFlag));
    }

    public int GetMotorScore()
    {
        if (!IsConnected())
            return disconnectedMotorScore;

        return Mathf.Max(0, Mathf.RoundToInt(motorForceKg));
    }

    public int GetPullCount()
    {
        return motorPullCount;
    }

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
    }

    private void SendCommand(MotorCommandPacket packet)
    {
#if USE_SERIAL_PORTS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
        if (!initialized || serialPort == null || !serialPort.IsOpen)
            return;

        try
        {
            byte[] bytes = packet.GetBytes();
            serialPort.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IOT][Motor] Send failed: {ex.Message}");
        }
#endif
    }

    void OnDestroy()
    {
        Shutdown();
    }

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

        public static MotorCommandPacket CreateSpringModePacket(ushort baseForce, ushort pullLimit, byte distance)
        {
            var cmd = new MotorCommandPacket();
            cmd.packet[1] = DefaultTargetMotor1;
            cmd.packet[2] = 0x01;
            cmd.packet[3] = 0x02;

            cmd.packet[4] = (byte)(baseForce >> 8);
            cmd.packet[5] = (byte)(baseForce & 0xFF);
            cmd.packet[6] = (byte)(pullLimit >> 8);
            cmd.packet[7] = (byte)(pullLimit & 0xFF);
            cmd.packet[8] = 100;
            cmd.packet[9] = distance;

            return cmd;
        }

        public static MotorCommandPacket CreateSpringModePacket(ushort baseForce, ushort pullLimit, byte distance, byte pullCountClearFlag)
        {
            var cmd = CreateSpringModePacket(baseForce, pullLimit, distance);
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
