#include <Arduino.h>
#include <WiFi.h>
#include <OSCMessage.h>
#include <WiFiUdp.h>

#include "FastLED.h"


// WIFI LOGIC STUFF, YOU MAY NEED TO CHANGE THIS
const int BOMBID = 1337;
const char* ssid = "bombnet";
const char* password = "sprangnollan";

IPAddress localIp;

unsigned int recvPort = 5000;
unsigned int targetPort = 4000;

IPAddress targetIp = IPAddress(192, 168, 1, 223); // does not matter

WiFiUDP udp;

char* recvbuf = new char[8192];
;
#define LED_PIN 17
#define NUM_LEDS 300
CRGB leds[NUM_LEDS];

void setupLEDs() {
  FastLED.addLeds<NEOPIXEL, LED_PIN>(leds, NUM_LEDS);
  FastLED.setBrightness(255);
}


void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  Serial.println("Hello, world!");

  setupLEDs();
  //light up all the leds in red
  for (int i = 0; i < NUM_LEDS; i++) {
    leds[i] = CRGB::Green * (i%2);
  }
  FastLED.show();

  delay(5000);

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
      delay(20);
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


void sendOscCommand(const char *address) {
  udp.beginPacket(targetIp, targetPort);
  OSCMessage msg(address);
  msg.send(udp);
  udp.endPacket();
  msg.empty();
}

bool overridden = false;

void LEDAnim(){
  for(int i = 0; i < NUM_LEDS; i++) {
    int val = (int)((sin((float)i/30.0f + (float)millis()/1000.0f) / 2.0f + 0.5f) * 100.0f);
    leds[i] = CRGB(val, val / 3, 0);
  }
  FastLED.show();
}


void handleOSCMessage(OSCMessage &msg) {
  if (msg.fullMatch("/fx/lights/override")) {
    int r = msg.getInt(0);
    int g = msg.getInt(1);
    int b = msg.getInt(2);

    if(r == 0 && g == 0 && b == 0){
      overridden = false;
      return;
    }
    overridden = true;

    for(int i = 0; i < NUM_LEDS; i++) {
      leds[i] = CRGB(r, g, b);
    }
    FastLED.show();
  }
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

    handleOSCMessage(msgrecv);
    
  }

  if(!overridden){
    LEDAnim();
  }
  
  delay(30);
}


