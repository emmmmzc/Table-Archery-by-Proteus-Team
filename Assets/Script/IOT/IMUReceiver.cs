using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public struct ImuPacket
{
    public uint timestampMs;
    public Vector3 acceleration;
    public Vector3 gyroscope;
}

public class IMUReceiver : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 5005;
    public bool startOnAwake = true;

    [Header("Debug")]
    public bool logPackets = false;
    public bool logErrors = true;

    [Header("Latest Packet (Read Only)")]
    public bool hasPacket;
    public uint timestampMs;
    public Vector3 acceleration;
    public Vector3 gyroscope;
    public float lastPacketUnityTime;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;
    private readonly object packetLock = new object();
    private ImuPacket latestPacket;
    private bool hasPendingPacket;

    public ImuPacket LatestPacket
    {
        get
        {
            lock (packetLock)
            {
                return latestPacket;
            }
        }
    }

    void Awake()
    {
        if (startOnAwake)
            StartReceiver();
    }

    void Update()
    {
        ImuPacket packet;
        bool shouldApply;

        lock (packetLock)
        {
            shouldApply = hasPendingPacket;
            packet = latestPacket;
            hasPendingPacket = false;
        }

        if (!shouldApply)
            return;

        hasPacket = true;
        timestampMs = packet.timestampMs;
        acceleration = packet.acceleration;
        gyroscope = packet.gyroscope;
        lastPacketUnityTime = Time.time;

        if (logPackets)
            Debug.Log($"[IMU] t={timestampMs} acc={acceleration} gyro={gyroscope}");
    }

    public void StartReceiver()
    {
        if (running)
            return;

        try
        {
            udpClient = new UdpClient(listenPort);
            running = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "IMU UDP Receiver"
            };
            receiveThread.Start();
            Debug.Log($"[IMU] Listening on UDP port {listenPort}");
        }
        catch (Exception ex)
        {
            running = false;
            if (logErrors)
                Debug.LogWarning($"[IMU] Failed to start UDP receiver: {ex.Message}");
        }
    }

    public void StopReceiver()
    {
        running = false;

        try
        {
            udpClient?.Close();
        }
        catch (Exception ex)
        {
            if (logErrors)
                Debug.LogWarning($"[IMU] UDP close failed: {ex.Message}");
        }

        udpClient = null;
        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, listenPort);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string text = Encoding.ASCII.GetString(data).Trim();

                if (!TryParsePacket(text, out ImuPacket packet))
                    continue;

                lock (packetLock)
                {
                    latestPacket = packet;
                    hasPendingPacket = true;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                if (running && logErrors)
                    Debug.LogWarning("[IMU] UDP socket receive failed.");
            }
            catch (Exception ex)
            {
                if (logErrors)
                    Debug.LogWarning($"[IMU] Receive failed: {ex.Message}");
            }
        }
    }

    private static bool TryParsePacket(string text, out ImuPacket packet)
    {
        packet = default;

        string[] parts = text.Split(',');
        if (parts.Length != 8 || parts[0] != "IMU")
            return false;

        NumberStyles style = NumberStyles.Float;
        CultureInfo culture = CultureInfo.InvariantCulture;

        if (!uint.TryParse(parts[1], NumberStyles.Integer, culture, out uint timestamp))
            return false;
        if (!float.TryParse(parts[2], style, culture, out float ax))
            return false;
        if (!float.TryParse(parts[3], style, culture, out float ay))
            return false;
        if (!float.TryParse(parts[4], style, culture, out float az))
            return false;
        if (!float.TryParse(parts[5], style, culture, out float gx))
            return false;
        if (!float.TryParse(parts[6], style, culture, out float gy))
            return false;
        if (!float.TryParse(parts[7], style, culture, out float gz))
            return false;

        packet = new ImuPacket
        {
            timestampMs = timestamp,
            acceleration = new Vector3(ax, ay, az),
            gyroscope = new Vector3(gx, gy, gz)
        };
        return true;
    }

    void OnDestroy()
    {
        StopReceiver();
    }
}
