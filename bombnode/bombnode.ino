#include <MicroOscUdp.h>
#include <WiFi.h>
const char* ssid = "CASELAB";
const char* password = "CaseLocalNet";

#include <WiFiUdp.h>
WiFiUDP myUdp;

unsigned int recvPort = 5000;

MicroOscUdp<1024> myMicroOsc(&myUdp, myDestinationIp, myDestinationPort);

IPAddress localAddr;

int ipToTry = 1;

void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  Serial.println("Alive");
  
  
  WiFi.mode(WIFI_STA); //Optional
  WiFi.begin(ssid, password);
  Serial.println("\nConnecting");
  Serial.println(ssid);

  while(WiFi.status() != WL_CONNECTED){
      Serial.print(".");
      delay(100);
  }

  Serial.println("\nConnected to the WiFi network");
  Serial.print("Local ESP32 IP: ");
  localAddr = WiFi.localIP();
  Serial.println(localAddr);
  // Find host
}

void loop() {
  // put your main code here, to run repeatedly:

}
