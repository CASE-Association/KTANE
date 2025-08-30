#include <Arduino.h>
#include <WiFi.h>
#include <OSCMessage.h>
#include <WiFiUdp.h>

#include "FastLED.h"

//specifics for lcd display
#include <Wire.h>
#include <Adafruit_RGBLCDShield.h>
#include <utility/Adafruit_MCP23017.h>

// for the 14 segment display
#include <Adafruit_GFX.h>
#include <Adafruit_LEDBackpack.h>

#ifndef _BV
  #define _BV(bit) (1<<(bit))
#endif

Adafruit_LEDBackpack segDisplay = Adafruit_LEDBackpack();

#define ALPHANUM_SEG_A 0b0000000000000001 ///&lt; Alphanumeric segment A
#define ALPHANUM_SEG_B 0b0000000000000010 ///&lt; Alphanumeric segment B
#define ALPHANUM_SEG_C 0b0000000000000100 ///&lt; Alphanumeric segment C
#define ALPHANUM_SEG_D 0b0000000000001000 ///&lt; Alphanumeric segment D
#define ALPHANUM_SEG_E 0b0000000000010000 ///&lt; Alphanumeric segment E
#define ALPHANUM_SEG_F 0b0000000000100000 ///&lt; Alphanumeric segment F
#define ALPHANUM_SEG_G1 0b0000000001000000 ///&lt; Alphanumeric segment G1
#define ALPHANUM_SEG_G2 0b0000000010000000 ///&lt; Alphanumeric segment G2
#define ALPHANUM_SEG_H 0b0000000100000000 ///&lt; Alphanumeric segment H
#define ALPHANUM_SEG_J 0b0000001000000000 ///&lt; Alphanumeric segment J
#define ALPHANUM_SEG_K 0b0000010000000000 ///&lt; Alphanumeric segment K
#define ALPHANUM_SEG_L 0b0000100000000000 ///&lt; Alphanumeric segment L
#define ALPHANUM_SEG_M 0b0001000000000000 ///&lt; Alphanumeric segment M
#define ALPHANUM_SEG_N 0b0010000000000000 ///&lt; Alphanumeric segment N
#define ALPHANUM_SEG_DP 0b0100000000000000 ///&lt; Alphanumeric segment DP



int numbers[10] = {
    // 0
    ALPHANUM_SEG_A | ALPHANUM_SEG_B | ALPHANUM_SEG_C | ALPHANUM_SEG_D |
    ALPHANUM_SEG_E | ALPHANUM_SEG_F,
    
    // 1
    ALPHANUM_SEG_B | ALPHANUM_SEG_C,
    
    // 2
    ALPHANUM_SEG_A | ALPHANUM_SEG_B | ALPHANUM_SEG_D | ALPHANUM_SEG_E |
    ALPHANUM_SEG_G1 | ALPHANUM_SEG_G2,
    
    // 3
    ALPHANUM_SEG_A | ALPHANUM_SEG_B | ALPHANUM_SEG_C | ALPHANUM_SEG_D |
    ALPHANUM_SEG_G1 | ALPHANUM_SEG_G2,
    
    // 4
    ALPHANUM_SEG_B | ALPHANUM_SEG_C | ALPHANUM_SEG_F | ALPHANUM_SEG_G1 |
    ALPHANUM_SEG_G2,
    
    // 5
    ALPHANUM_SEG_A | ALPHANUM_SEG_C | ALPHANUM_SEG_D | ALPHANUM_SEG_F |
    ALPHANUM_SEG_G1 | ALPHANUM_SEG_G2,
    
    // 6
    ALPHANUM_SEG_A | ALPHANUM_SEG_C | ALPHANUM_SEG_D | ALPHANUM_SEG_E |
    ALPHANUM_SEG_F | ALPHANUM_SEG_G1 | ALPHANUM_SEG_G2,
    
    // 7
    ALPHANUM_SEG_A | ALPHANUM_SEG_B | ALPHANUM_SEG_C,
    
    // 8
    ALPHANUM_SEG_A | ALPHANUM_SEG_B | ALPHANUM_SEG_C | ALPHANUM_SEG_D |
    ALPHANUM_SEG_E | ALPHANUM_SEG_F | ALPHANUM_SEG_G1 | ALPHANUM_SEG_G2,
    
    // 9
    ALPHANUM_SEG_A | ALPHANUM_SEG_B | ALPHANUM_SEG_C | ALPHANUM_SEG_D |
    ALPHANUM_SEG_F | ALPHANUM_SEG_G1 | ALPHANUM_SEG_G2
};



// WIFI LOGIC STUFF, YOU MAY NEED TO CHANGE THIS
const int BOMBID = 1337;
const char* ssid = "bombnet";
const char* password = "sprangnollan";

IPAddress localIp;

unsigned int recvPort = 5000;
unsigned int targetPort = 4000;

IPAddress targetIp = IPAddress(192, 168, 1, 223); // does not matter

WiFiUDP udp;

char* recvbuf = new char[1024];

//---------------------------------------------------------
struct debouncedInput {
  int pin;
  bool state;         // debounced state
  bool lastState = state;     // last stable state
  bool lastReading;   // last raw reading
  unsigned long lastDebounceTime;
};

void initInput(debouncedInput &input, int pin, bool startingState) {
  input.pin = pin;
  pinMode(pin, INPUT);
  input.state = startingState;
  input.lastReading = startingState;
  input.lastDebounceTime = millis();
}

void updateInput(debouncedInput &input, unsigned long debounceDelay = 100) {
  bool reading = digitalRead(input.pin);

  if (reading != input.lastReading) {
    input.lastDebounceTime = millis();  // reset timer on change

  }

  if ((millis() - input.lastDebounceTime) > debounceDelay) {
    // update state if stable long enough
    input.lastState = input.state;
    input.state = reading;
  }

  input.lastReading = reading;
}



// LCD display
Adafruit_RGBLCDShield lcd = Adafruit_RGBLCDShield();
#define WHITE 0x7
#define GREEN 0x3
uint8_t buttons = lcd.readButtons();
uint8_t lastButtons = 0;
bool allWiresCut = false;

// wires
const int WIRE_PINS[5] = {12, 14, 27, 26, 25};
bool wireStates[5] = {true, true, true, true, true};
debouncedInput wireInputs[5];



// LED lights
#define LED_PIN 16
#define NUM_LEDS 8
CRGB leds[NUM_LEDS];
int logicalLEDNumberToPhysicalPosition[NUM_LEDS] = {6, 7, 0, 1, 5, 4, 3, 2}; // mapping from logical led number to physical led index


void setupLEDs() {
  FastLED.addLeds<NEOPIXEL, LED_PIN>(leds, NUM_LEDS);
  FastLED.setBrightness(50);
}


// "DO NOT PRESS" BUTTON LOGIC
debouncedInput doNotPressButton;


#define BUTTON_PIN 13 // Change this to the actual pin number
bool buttonPressed = false;
bool lastButtonState = false;
//interrupt





// put function declarations here:

void sendOscCommand(const char *address);
void display(String msg, int x, int y, bool clear);
void createSweCharacters(); 
String replaceSpecialChars(String str);
void handleOSCMessage(OSCMessage &msg);
void handleWordMaze(OSCMessage &msg);
void LEDmanager(int LEDnNumber, CRGB color);
void handleLED(OSCMessage &msg);
void handleTimer(OSCMessage &msg);
void handleStrikes(OSCMessage &msg);

void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  Serial.println("Hello, world!");

  setupLEDs();
  //light up all the leds in red
  for (int i = 0; i < NUM_LEDS; i++) {
    leds[logicalLEDNumberToPhysicalPosition[i]] = CRGB::Red;
    delay(100);
    FastLED.show();
  }
  //turn off all LEDs
  for (int i = 0; i < NUM_LEDS; i++) {
    leds[logicalLEDNumberToPhysicalPosition[i]] = CRGB::Black;
    delay(100);
    FastLED.show();
  }


  // Initialize I2C for LCD
  Wire.begin(18, 23);
  Serial.println(SDA);
  Serial.println(SCL);
  lcd.begin(16, 2);

  createSweCharacters(); // custom chars for lcd

  segDisplay.begin(0x70);
  segDisplay.setBrightness(10);
  // segDisplay.displaybuffer[0] = numbers[0];
  // segDisplay.writeDisplay();
  //write all numbers 1-10 on the display
  for (int i = 0; i < 10; i++) {
    segDisplay.displaybuffer[0] = numbers[i];
    segDisplay.displaybuffer[1] = numbers[i];
    segDisplay.displaybuffer[2] = numbers[i];
    segDisplay.displaybuffer[3] = numbers[i];
    segDisplay.writeDisplay();
    delay(100);
  }
  segDisplay.clear();

  lcd.setCursor(0, 0); // for style only
  lcd.print("SLAVSOFT");
  lcd.setCursor(0, 1);
  lcd.print("INDUSTRIES");
  lcd.setBacklight(GREEN);

  for (int i = 0; i < 5; i++) {
    initInput(wireInputs[i], WIRE_PINS[i], true);
  }
  initInput(doNotPressButton, BUTTON_PIN, false);

  delay(1000);


  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);

  Serial.println("\nConnecting");
  Serial.println(ssid);


  display("Connecting...", 0, 0, true);
  display(ssid, 0, 1, false);

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
  
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("scanning...");
  lcd.setCursor(0, 1);
  lcd.print(targetIp);

  while (!connected){

      
    for(int i = 1; i < 254; i++){
      targetIp[3] = i;
      Serial.print("Sending to ");
      Serial.println(targetIp);
      udp.beginPacket(targetIp, targetPort);

  

      //do all the fancy stuff with lights and 14 seg
      //limit to 8 leds
      LEDmanager(i%8, CRGB::Green);
      FastLED.show();

      
      segDisplay.displaybuffer[0] = numbers[(i/100)%10];
      segDisplay.displaybuffer[1] = numbers[(i/10)%10];
      segDisplay.displaybuffer[2] = numbers[i%10];
      segDisplay.displaybuffer[3] = numbers[0];
      segDisplay.writeDisplay();
      delay(20);

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

  segDisplay.clear();

  LEDmanager(0, CRGB::Black);
  LEDmanager(1, CRGB::Black);
  LEDmanager(2, CRGB::Black);
  LEDmanager(3, CRGB::Black);
  LEDmanager(4, CRGB::Black);
  LEDmanager(5, CRGB::Black);
  LEDmanager(6, CRGB::Black);
  LEDmanager(7, CRGB::Black);

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


  //wire cutting logic, 
  if(!allWiresCut){
    for (int i = 0; i < 5; i++) {

      updateInput(wireInputs[i]);

      if (wireInputs[i].state == LOW && wireStates[i] == true) {
        udp.beginPacket(targetIp, targetPort);
        OSCMessage msg("/wires/cut/"); // 
        msg.add(i);
        msg.send(udp);
        udp.endPacket();
        msg.empty();
        wireStates[i] = false;
        Serial.println("Cut wire " + String(i));
      }
      
      //check if all wires are cut, set allwirescut to true
      allWiresCut = true;
      for (int j = 0; j < 5; j++) {
        if (wireStates[j] == true) {
          allWiresCut = false;
          break;
        }
      }
    }


    //"DO NOT PRESS" button logic
    updateInput(doNotPressButton, 10);
    //Serial.println("Button state: " + String(doNotPressButton.state) + " last state: " + String(doNotPressButton.lastState));
    if (doNotPressButton.state == HIGH && lastButtonState == false) {
      // Send OSC message
      udp.beginPacket(targetIp, targetPort);
      OSCMessage msg("/button");
      msg.add(true);
      msg.send(udp);
      udp.endPacket();
      msg.empty();

      lastButtonState = true;
      Serial.println("Button pressed!");
    }
    else if (doNotPressButton.state == LOW && lastButtonState == true) {
      // Send OSC message
      udp.beginPacket(targetIp, targetPort);
      OSCMessage msg("/button");
      msg.add(false);
      msg.send(udp);
      udp.endPacket();
      msg.empty();

      lastButtonState = false;
      Serial.println("Button released!");
    }


  }





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

void handleOSCMessage(OSCMessage &msg) {
  if (msg.fullMatch("/wordmaze/display")) {
    handleWordMaze(msg);
  } 
  else if (msg.fullMatch("/button/lights")) {
    handleLED(msg);
  } 
  // else if (msg.fullMatch("/button/light/off")) {
  //   handleLED(msg, false);
  // } 

  else if (msg.fullMatch("/timer")){
    handleTimer(msg);
  }

  else if (msg.fullMatch("/strikes")){
    handleStrikes(msg);
  }


}

void handleStrikes(OSCMessage &msg) {
  // Handle strikes message, set cursor to 16-7 on first row and display
  if (msg.isInt(0)) {
    display(String(msg.getInt(0)), 15, 0, false);
  }
}

void handleTimer (OSCMessage &msg) {
  // we recieve four different ints, 10min 1 min 10 sec 1 sec, unpack and display them on 14 seg
  for(int i = 0; i < 4; i++) {
    if (msg.isInt(i)) {
      int value = msg.getInt(i);
      // Display the value on the 14-segment display
      segDisplay.displaybuffer[i] = numbers[value];
    }
  }
  // light up decimal point
  segDisplay.displaybuffer[1] |= ALPHANUM_SEG_DP*(msg.getInt(3)%2);
  segDisplay.writeDisplay();
}

void handleWordMaze(OSCMessage &msg) {   // this functions displays the words for the word maze
  if (msg.isString(0)) { // check if the message is for displaying the first word
    char buf[64]; 
    msg.getString(0, buf, sizeof(buf)); 
    display(buf, 0, 0, true); //clear display and show first argument/message on first row
  }
  if (msg.isString(1)) { //is there a second argument? if so, print it on the second rw
    char buf[64];
    msg.getString(1, buf, sizeof(buf));
    display(buf, 0, 1, false);
  }

} 


// void handleLED(OSCMessage &msg, bool onOrOff) {   // this functions displays the words for the word maze
//   if (msg.isInt(0)) { // check if the message is for displaying the first word
//     int addressedLed = msg.getInt(0);
//     Serial.println("LED " + String(addressedLed) + (onOrOff ? " ON" : " OFF"));
//     LEDmanager(logicalLEDNumberToPhysicalPosition[addressedLed], onOrOff ? CRGB::Red : CRGB::Black);
//   }

// }
void handleLED(OSCMessage &msg) {   // this functions displays the words for the word maze

  // we recieve 8 booleans corrsponding to each light. loop over and light up if true
  for(int i = 0; i < 8; i++) {
    if (msg.getBoolean(i)) {
      LEDmanager(logicalLEDNumberToPhysicalPosition[i], CRGB::Red);
    }
  }

}

void LEDmanager(int LEDnNumber, CRGB color){
  // Set the color of the specified LED
  leds[LEDnNumber] = color;
  FastLED.show();

}