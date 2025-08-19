#include <Arduino.h>
#include <WiFi.h>
#include <OSCMessage.h>

#include <WiFiUdp.h>

// put function declarations here:

const int BOMBID = 1337;

const char* ssid = "CASELAB";
const char* password = "CaseLocalNet";

IPAddress localIp;

unsigned int recvPort = 5000;
unsigned int targetPort = 4000;

IPAddress targetIp = IPAddress(192, 168, 1, 223);

WiFiUDP udp;

char* recvbuf = new char[1024];

void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  Serial.println("Hello, world!");

  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);
  Serial.println("\nConnecting");
  Serial.println(ssid);

  while(WiFi.status() != WL_CONNECTED){
      Serial.print(".");
      delay(100);
  }

  Serial.println("\nConnected to the WiFi network");
  Serial.print("Local ESP32 IP: ");
  localIp = WiFi.localIP();
  Serial.println(localIp);

  udp.begin(recvPort);
  
  targetIp[0] = localIp[0];
  targetIp[1] = localIp[1];
  targetIp[2] = localIp[2];
  
  bool connected = false;
  

  while (!connected){
    for(int i = 1; i < 254; i++){
      targetIp[3] = i;
      Serial.print("Sending to ");
      Serial.println(targetIp);
      udp.beginPacket(targetIp, targetPort);

      OSCMessage msg = OSCMessage("/connect");
      msg.add(i);
      msg.add(BOMBID);
      msg.send(udp);
      udp.endPacket();
      msg.empty();
      delay(10);
      if(udp.parsePacket() > 0){
        OSCMessage msgrecv;
        while (udp.available() > 0) {
          msgrecv.fill(udp.read());
        }

        if(strcmp(msgrecv.getAddress(), "/ok") == 0){
          Serial.println("Connected to target!");
          targetIp[3] = msgrecv.getInt(0);
          connected = true;
          break;
        }
        Serial.print("Received: '");
        Serial.print(msgrecv.getAddress());
        Serial.println("'");
      }
    }
  }

  Serial.print("Final target IP: ");  
  Serial.println(targetIp);

}

void loop() {
  if (udp.parsePacket() > 0)
  {
    OSCMessage msgrecv;
    while (udp.available() > 0)
    {
      msgrecv.fill(udp.read());
    }
    Serial.print("Received: '");
    Serial.print(msgrecv.getAddress());
    Serial.println("'");
  }
  // put your main code here, to run repeatedly:
}