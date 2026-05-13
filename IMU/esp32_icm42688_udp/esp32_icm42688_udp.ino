#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include "ICM42688.h"
//
// =========================
// User configuration
// =========================
constexpr bool TEST_MODE = false;

constexpr char WIFI_SSID[] = "damnnnn";
constexpr char WIFI_PASS[] = "lsr23333";
constexpr uint32_t WIFI_CONNECT_TIMEOUT_MS = 10000;

// Fallback hotspot if STA connection fails.
constexpr char AP_SSID[] = "ESP32_IMU_AP";
constexpr char AP_PASS[] = "12345678";

constexpr uint16_t PC_PORT = 5005;

// ESP32 default I2C pins are usually SDA=21, SCL=22.
constexpr int I2C_SDA_PIN = 8;
constexpr int I2C_SCL_PIN = 9;

// 0x68 if AD0 is low, 0x69 if AD0 is high.
constexpr uint8_t IMU_I2C_ADDR = 0x69;

// Send interval in milliseconds.
constexpr uint32_t SEND_INTERVAL_MS = 10;
constexpr uint32_t TEST_SCAN_INTERVAL_MS = 5000;
constexpr int GYRO_CALIBRATION_SAMPLES = 300;
constexpr uint32_t GYRO_CALIBRATION_DELAY_MS = 5;
constexpr char DISCOVERY_HELLO[] = "IMU_HELLO";
constexpr int IMU_INIT_RETRY_COUNT = 5;
constexpr uint32_t IMU_INIT_RETRY_DELAY_MS = 300;

WiFiUDP udp;
ICM42688 imu(Wire, IMU_I2C_ADDR);

uint32_t lastSendMs = 0;
uint32_t lastTestPrintMs = 0;
uint32_t lastTestScanMs = 0;
IPAddress udpTargetIP;
IPAddress broadcastTargetIP;
IPAddress discoveredClientIP;
bool hasDiscoveredClient = false;
bool imuReady = false;
float gyroBiasX = 0.0f;
float gyroBiasY = 0.0f;
float gyroBiasZ = 0.0f;

enum class NetworkMode {
  kStation,
  kAccessPoint
};

NetworkMode networkMode = NetworkMode::kStation;

void printHexAddress(uint8_t addr) {
  Serial.print("0x");
  if (addr < 16) {
    Serial.print("0");
  }
  Serial.println(addr, HEX);
}

void scanI2CDevices() {
  Serial.println("I2C scan start");
  uint8_t foundCount = 0;

  for (uint8_t addr = 1; addr < 127; ++addr) {
    Wire.beginTransmission(addr);
    const uint8_t err = Wire.endTransmission();
    if (err == 0) {
      Serial.print("Found I2C device at ");
      printHexAddress(addr);
      ++foundCount;
    }
  }

  if (foundCount == 0) {
    Serial.println("No I2C devices found");
  }

  Serial.println("I2C scan done");
}

void printTargetAddressProbe() {
  Wire.beginTransmission(IMU_I2C_ADDR);
  const uint8_t err = Wire.endTransmission();
  Serial.print("Probe configured IMU_I2C_ADDR ");
  printHexAddress(IMU_I2C_ADDR);
  Serial.print("Probe result code: ");
  Serial.println(err);

  Wire.beginTransmission(0x68);
  const uint8_t err68 = Wire.endTransmission();
  Serial.print("Probe 0x68 result code: ");
  Serial.println(err68);

  Wire.beginTransmission(0x69);
  const uint8_t err69 = Wire.endTransmission();
  Serial.print("Probe 0x69 result code: ");
  Serial.println(err69);
}

IPAddress calcBroadcast(IPAddress ip, IPAddress subnet) {
  IPAddress broadcast;
  for (int i = 0; i < 4; ++i) {
    broadcast[i] = ip[i] | ~subnet[i];
  }
  return broadcast;
}

void startAccessPoint() {
  WiFi.disconnect(true, true);
  delay(200);
  WiFi.mode(WIFI_AP);

  if (!WiFi.softAP(AP_SSID, AP_PASS)) {
    Serial.println("Failed to start fallback AP");
    while (true) {
      delay(1000);
    }
  }

  networkMode = NetworkMode::kAccessPoint;
  broadcastTargetIP = calcBroadcast(WiFi.softAPIP(), WiFi.softAPSubnetMask());
  udpTargetIP = broadcastTargetIP;
  hasDiscoveredClient = false;

  Serial.println();
  Serial.println("WiFi STA failed, fallback to AP mode");
  Serial.print("AP SSID: ");
  Serial.println(AP_SSID);
  Serial.print("AP PASS: ");
  Serial.println(AP_PASS);
  Serial.print("AP IP: ");
  Serial.println(WiFi.softAPIP());
  Serial.print("UDP broadcast target: ");
  Serial.println(broadcastTargetIP);
}

void connectNetwork() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);

  Serial.print("Connecting WiFi");
  const uint32_t startMs = millis();
  while (WiFi.status() != WL_CONNECTED &&
         millis() - startMs < WIFI_CONNECT_TIMEOUT_MS) {
    delay(500);
    Serial.print(".");
  }

  if (WiFi.status() == WL_CONNECTED) {
    networkMode = NetworkMode::kStation;
    broadcastTargetIP = calcBroadcast(WiFi.localIP(), WiFi.subnetMask());
    udpTargetIP = broadcastTargetIP;
    hasDiscoveredClient = false;

    Serial.println();
    Serial.print("WiFi connected, ESP32 IP: ");
    Serial.println(WiFi.localIP());
    Serial.print("Subnet mask: ");
    Serial.println(WiFi.subnetMask());
    Serial.print("UDP broadcast target: ");
    Serial.println(broadcastTargetIP);
    return;
  }

  startAccessPoint();
}

void setupImu() {
  Wire.begin(I2C_SDA_PIN, I2C_SCL_PIN);
  Wire.setClock(400000);

  if (TEST_MODE) {
    scanI2CDevices();
  }

  int status = -1;
  for (int attempt = 1; attempt <= IMU_INIT_RETRY_COUNT; ++attempt) {
    status = imu.begin();
    if (status >= 0) {
      break;
    }

    Serial.print("ICM42688 init failed, attempt ");
    Serial.print(attempt);
    Serial.print("/");
    Serial.print(IMU_INIT_RETRY_COUNT);
    Serial.print(", status = ");
    Serial.println(status);
    delay(IMU_INIT_RETRY_DELAY_MS);
  }

  if (status < 0) {
    Serial.print("ICM42688 init failed after retries, status = ");
    Serial.println(status);
    imuReady = false;
    return;
  }

  imuReady = true;
  Serial.println("ICM42688 ready");
  calibrateGyro();
}

void calibrateGyro() {
  if (!imuReady) {
    return;
  }

  float sumX = 0.0f;
  float sumY = 0.0f;
  float sumZ = 0.0f;

  Serial.println("Keep IMU still, calibrating gyro...");

  for (int i = 0; i < GYRO_CALIBRATION_SAMPLES; ++i) {
    imu.getAGT();
    sumX += imu.gyrX();
    sumY += imu.gyrY();
    sumZ += imu.gyrZ();
    delay(GYRO_CALIBRATION_DELAY_MS);
  }

  gyroBiasX = sumX / GYRO_CALIBRATION_SAMPLES;
  gyroBiasY = sumY / GYRO_CALIBRATION_SAMPLES;
  gyroBiasZ = sumZ / GYRO_CALIBRATION_SAMPLES;

  Serial.print("Gyro bias: ");
  Serial.print(gyroBiasX, 6);
  Serial.print(", ");
  Serial.print(gyroBiasY, 6);
  Serial.print(", ");
  Serial.println(gyroBiasZ, 6);
}

void sendImuPacket() {
  if (!imuReady) {
    return;
  }

  imu.getAGT();

  const uint32_t ts = millis();
  const float ax = imu.accX();
  const float ay = imu.accY();
  const float az = imu.accZ();
  const float gx = imu.gyrX() - gyroBiasX;
  const float gy = imu.gyrY() - gyroBiasY;
  const float gz = imu.gyrZ() - gyroBiasZ;

  char packet[128];
  snprintf(
    packet,
    sizeof(packet),
    "IMU,%lu,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f\n",
    static_cast<unsigned long>(ts),
    ax,
    ay,
    az,
    gx,
    gy,
    gz
  );

  udp.beginPacket(udpTargetIP, PC_PORT);
  udp.write(reinterpret_cast<const uint8_t *>(packet), strlen(packet));
  udp.endPacket();

  Serial.print(packet);
}

void processUdpDiscovery() {
  const int packetSize = udp.parsePacket();
  if (packetSize <= 0) {
    return;
  }

  char message[32];
  const int length = udp.read(message, sizeof(message) - 1);
  if (length <= 0) {
    return;
  }

  message[length] = '\0';
  if (strcmp(message, DISCOVERY_HELLO) != 0) {
    return;
  }

  discoveredClientIP = udp.remoteIP();
  udpTargetIP = discoveredClientIP;
  hasDiscoveredClient = true;

  Serial.print("Unity client discovered: ");
  Serial.println(discoveredClientIP);
}

void runTestMode() {
  const uint32_t now = millis();

  if (now - lastTestScanMs >= TEST_SCAN_INTERVAL_MS) {
    lastTestScanMs = now;
    scanI2CDevices();
    printTargetAddressProbe();
  }

  if (now - lastTestPrintMs < 1000) {
    return;
  }

  lastTestPrintMs = now;
  Serial.print("TEST_MODE heartbeat, millis = ");
  Serial.println(now);

  if (!imuReady) {
    Serial.println("IMU not ready. Check SDA/SCL, 3V3/GND, CS pin state, and 0x68/0x69 address.");
  }
}

void setup() {
  Serial.begin(115200);
  delay(1000);

  connectNetwork();
  udp.begin(PC_PORT);
  setupImu();
}

void loop() {
  if (TEST_MODE) {
    runTestMode();
    return;
  }

  const uint32_t now = millis();
  processUdpDiscovery();

  if (now - lastSendMs >= SEND_INTERVAL_MS) {
    lastSendMs = now;
    sendImuPacket();
  }
}
