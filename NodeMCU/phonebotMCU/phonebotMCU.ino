#include <ESP8266WiFi.h>

#ifndef STASSID
#define STASSID ""
#define STAPSK  ""
#endif



const char* ssid     = STASSID;
const char* password = STAPSK;

const char* host = "fileserver";
const uint16_t port = 8778;

String Number = "";

WiFiClient client;

void setup() {
  Serial.begin(115200);
  pinMode(D1, INPUT_PULLUP);
  // We start by connecting to a WiFi network

  Serial.println();
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);

  /* Explicitly set the ESP8266 to be a WiFi-client, otherwise, it by default,
     would try to act as both a client and an access-point and could cause
     network-issues with your other WiFi-devices on your WiFi-network. */
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());

}


void loop() {
  Serial.println("looping...");
  if (!client.connected()) {
    Serial.print("Connecting to ");
    Serial.print(host);
    Serial.print(':');
    Serial.println(port);

    if (!client.connect(host, port)) {
      Serial.println("Connection failed");
      delay(5000);
      return;
    }
    else {
      Serial.println("Connected!");
    }
  }
  else
  {
    Serial.println("Still connected...");
  }

  // wait for data to be available
  while (client.available() == 0) {
    //delay(1000);
    if (!client.connected()) {
      Serial.println("Reconnecting...");
      return;
    }
  }

  if (client.available()) {
    Number = client.readStringUntil('$');
    Serial.println(Number);
  }

  unsigned long timeout = millis();

  while (millis() - timeout < 1000 * 30) {
    if (digitalRead(D1) == LOW) {
      if (client.connected() && Number.length() > 0) {
        Serial.println("Button pressed!");
        client.println(Number + "$");
        Number = "";
        break;
        }
    }
  }
}
