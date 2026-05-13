using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
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
    public bool sendDiscoveryHello = true;
    public string discoveryMessage = "IMU_HELLO";
    public float discoveryIntervalSeconds = 1f;

    [Header("Debug")]
    public bool logPackets = false;
    public bool logErrors = true;
    public bool logDiscovery = false;

    [Header("Latest Packet (Read Only)")]
    public bool hasPacket;
    public uint timestampMs;
    public Vector3 acceleration;
    public Vector3 gyroscope;
    public float lastPacketUnityTime;
    public float lastDiscoveryUnityTime;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;
    private float nextDiscoveryTime;
    private readonly List<IPAddress> discoveryTargets = new List<IPAddress>();
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
        SendDiscoveryHelloIfNeeded();

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

    private void SendDiscoveryHelloIfNeeded()
    {
        if (!sendDiscoveryHello || udpClient == null || Time.unscaledTime < nextDiscoveryTime)
            return;

        nextDiscoveryTime = Time.unscaledTime + Mathf.Max(0.1f, discoveryIntervalSeconds);

        try
        {
            byte[] data = Encoding.ASCII.GetBytes(discoveryMessage);
            RefreshDiscoveryTargets();
            foreach (IPAddress target in discoveryTargets)
                udpClient.Send(data, data.Length, new IPEndPoint(target, listenPort));

            lastDiscoveryUnityTime = Time.unscaledTime;

            if (logDiscovery)
                Debug.Log($"[IMU] Sent discovery hello to {discoveryTargets.Count} targets on UDP port {listenPort}");
        }
        catch (Exception ex)
        {
            if (logErrors)
                Debug.LogWarning($"[IMU] Discovery hello failed: {ex.Message}");
        }
    }

    private void RefreshDiscoveryTargets()
    {
        discoveryTargets.Clear();
        discoveryTargets.Add(IPAddress.Broadcast);

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            IPInterfaceProperties properties = networkInterface.GetIPProperties();
            foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
            {
                if (addressInfo.Address.AddressFamily != AddressFamily.InterNetwork || addressInfo.IPv4Mask == null)
                    continue;

                IPAddress broadcast = GetBroadcastAddress(addressInfo.Address, addressInfo.IPv4Mask);
                if (!discoveryTargets.Contains(broadcast))
                    discoveryTargets.Add(broadcast);
            }
        }
    }

    private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ipBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        byte[] broadcastBytes = new byte[ipBytes.Length];

        for (int i = 0; i < broadcastBytes.Length; i++)
            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

        return new IPAddress(broadcastBytes);
    }

    public void StartReceiver()
    {
        if (running)
            return;

        try
        {
            udpClient = new UdpClient(listenPort);
            udpClient.EnableBroadcast = true;
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
