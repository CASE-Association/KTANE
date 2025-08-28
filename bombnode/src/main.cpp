#include <Arduino.h>
#include <WiFi.h>
#include <OSCMessage.h>

#include <WiFiUdp.h>


//specifics for lcd display
#include <Wire.h>
#include <Adafruit_RGBLCDShield.h>
#include <utility/Adafruit_MCP23017.h>

Adafruit_RGBLCDShield lcd = Adafruit_RGBLCDShield();
#define WHITE 0x7

uint8_t buttons = lcd.readButtons();

uint8_t lastButtons = 0;


// put function declarations here:

void sendOscCommand(const char *address);
void display(String msg, int x, int y, bool clear);

void createSweCharacters(); 
String replaceSpecialChars(String str);
void displayOscMessage(OSCMessage &msg);



const int BOMBID = 1337;

const char* ssid = "bombnet";
const char* password = "sprangnollan";

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
  Wire.begin(22, 19);
  Serial.println(SDA);
  Serial.println(SCL);
  lcd.begin(16, 2);

  createSweCharacters();

  lcd.setCursor(0, 0);
  lcd.print("SLAVSOFT");
  lcd.setCursor(0, 1);
  lcd.print("INDUSTRIES");
  lcd.setBacklight(HIGH);

  delay(500);

  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);

  Serial.println("\nConnecting");
  Serial.println(ssid);

  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Connecting...");
  lcd.setCursor(0, 1);
  lcd.print(ssid);

  while(WiFi.status() != WL_CONNECTED){
      Serial.print(".");
      delay(100);
  }

  Serial.println("\nConnected to the WiFi network");
  Serial.print("Local ESP32 IP: ");
  localIp = WiFi.localIP();
  Serial.println(localIp);

  lcd.clear();
  lcd.print("ESP IP: ");
  lcd.print(localIp);
  

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

      //only refresh lcd every tenth iteration
      if (i % 10 == 0) {
        lcd.clear();
        lcd.setCursor(0, 0);
        lcd.print("Connecting to:");
        lcd.setCursor(0, 1);
        lcd.print(targetIp);
      }

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

  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Final IP: ");
  lcd.setCursor(0, 1);
  lcd.print(targetIp);

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

    displayOscMessage(msgrecv);
  }


  // if button left is pressed, send osc
  uint8_t buttons = lcd.readButtons();
  if ((buttons & BUTTON_LEFT) && !(lastButtons & BUTTON_LEFT)) {
    sendOscCommand("/wordmaze/left");
  }
  if ((buttons & BUTTON_RIGHT) && !(lastButtons & BUTTON_RIGHT)) {
    sendOscCommand("/wordmaze/right");
  }
  if ((buttons & BUTTON_SELECT) && !(lastButtons & BUTTON_SELECT)) {
    sendOscCommand("/wordmaze/ok");
  }

  lastButtons = buttons;



  



  // put your main code here, to run repeatedly:
}







void sendOscCommand(const char *address) {
  udp.beginPacket(targetIp, targetPort);
  OSCMessage msg(address);
  msg.send(udp);
  udp.endPacket();
  msg.empty();
}


void display(String msg, int x, int y, bool clear){
  if(clear == true){
    lcd.clear();
  }
  Serial.println(msg);
  lcd.setCursor(x,y);
  msg = replaceSpecialChars(msg);
  lcd.print(msg);

  
}


void createSweCharacters(){

 byte AwithRing[8] = {
  B00100,
  B01010,
  B01110,
  B00001,
  B01111,
  B10001,
  B01111,
  };
  
  byte AwithDots[8] = {
  B01010,
  B00000,
  B01110,
  B00001,
  B01111,
  B10001,
  B01111,
  };
  
  byte OwithDots[8] = {
  B01010,
  B00000,
  B01110,
  B10001,
  B10001,
  B10001,
  B01110,
  };
  
  byte CapitalAwithRing[8] = {
  B00100,
  B01010,
  B01110,
  B10001,
  B11111,
  B10001,
  B10001,
  };
  
  byte CapitalAwithDots[8] = {
  B01010,
  B00000,
  B01110,
  B10001,
  B11111,
  B10001,
  B10001,
  };
  
  byte CapitalOwithDots[8] = {
  B01010,
  B00000,
  B01110,
  B10001,
  B10001,
  B10001,
  B01110,
  };
  
  lcd.createChar(1, AwithRing);
  lcd.createChar(2, AwithDots);
  lcd.createChar(3, OwithDots);
  lcd.createChar(4, CapitalAwithRing);
  lcd.createChar(5, CapitalAwithDots);
  lcd.createChar(6, CapitalOwithDots);

}

String replaceSpecialChars(String str) {
  str.replace("(", String((char)1));
  str.replace("{", String((char)2));
  str.replace("[", String((char)3));
  str.replace("Å", String((char)4));
  str.replace("Ä", String((char)5));
  str.replace("Ö", String((char)6));
  return str;
}

void displayOscMessage(OSCMessage &msg) {   // this functions displays the words for the word maze
  if (msg.fullMatch("/wordmaze/display") && msg.isString(0)) { // check if the message is for displaying the first word
    char buf[64]; 
    msg.getString(0, buf, sizeof(buf)); 
    display(buf, 0, 0, true); //clear display and show first argument/message on first row
  }
  if (msg.fullMatch("/wordmaze/display") && msg.isString(1)) { //is there a second argument? if so, print it on the second rw
    char buf[64];
    msg.getString(1, buf, sizeof(buf));
    display(buf, 0, 1, false);
  }

}