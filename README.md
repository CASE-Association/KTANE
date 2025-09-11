# KTANE
Bomb Defusal game

## Folders
- MainBrain: PC server for the bombs, running all logic
- nodes: I/O devices connecting to the mainbrain.
  - BombNode (ESP32): The actual bomb with wires, buttons and lights.
  - audioplayer (vvvv/PC): Node for playing music, sound effects and maybe blinking a screen if no other lights are available.
  - lightnode (ESP32): Blinks a Neopixel LED strip when defusing, exploding etc.
- assets: Manual, images, source for modules

# Setting up a game / Crash Course
- Create a WiFi AP with SSID "bombnet" and passkey "sprangnollan". Connect the MainBrain PC to it, and any ESP32 nodes will automatically connect.
- Open mainbrain/mainbrain.vl in vvvv. A window should pop up - this will be blank until a bomb is registered.
- Once the nodes are connected, they should start scanning and eventually find the mainbrain. The bomb should now be visible in the previously blank window.
In the mainbrain GUI, you can start and pause the bomb by checking "Running". You can also set the seed (the puzzles will always be the same, given the same seed) and timer. To restart the bomb or update the seed/timer, click the "Reset" button.

The Wires module is the only that needs physical setup/resetting. Look in the GUI and find the Wires module. There you can see what colours the wires should have and in what order. 

## Architecture

```
       / Bomb Node <-------> /-----------\
Bomb 1 |                     |           |
       \ Light Node <------> | MainBrain |
                             |           |
Bomb 2 - Node <------------> \-----------/
````

Nodes and MainBrain are connected on the same network (/24 subnet) and communicate via OSC. The MainBrain runs all logic and holds all state of the bombs.

Nodes are "stupid", they only receive and send OSC commands to the MainBrain. They tell the server when buttons are pushed and light lights when told to.

Several bombs can be run from the same MainBrain, and several nodes can belong to each bomb. This means you can have one node for the bomb, one for audio playback, one for light FX, etc.

## Discovery
There is a discovery protocol for nodes to find and register to the main brain. Currently only implemented for the ESP32 nodes, as the audio node that runs on a PC is pretty easy to set the IP for either way. 

On startup each node connects to the WiFi network and starts sending OSC messages to all devices on the /24 subnet with the following arguments:
- (int) pre-assigned bomb ID
- (int) Last byte of IPv4 address that the message is being sent to. This is for the MainBrain to know what address the node thinks the MainBrain has.
When the MainBrain receives such a message, it registers the IP and ID of the node, and if no bomb already exists with that ID, it creates one. The MainBrain then sends back the last byte of the address (int) back to the node so it knows the MainBrain's IP address. The node has now found the MainBrain, and the MainBrain has registered that the node wants to receive updates.

All nodes (belonging to the same bomb ID) receive the same commands but only use those they are interested in. While somewhat inefficient, it makes the system very simple and robust.


# Adding a new bomb module
New bomb modules are added in the `mainbrain/csharp/mainbrainutils/modules` directory as separate `modulename.cs` files. 

- Make sure to register any new bomb modules in the `Init` function in `Bomb.cs`!
- If you need a random source, pass the `rng` object to you module initializer. This `Random` is seeded with the GUI, so the bomb generated is deterministic.
- Add `namespace Main;` in the beginning of your file.

A module inherits the `BombModule` class. The following functions can be overridden (with `public override void OnXyz(...){...}`):
```cs
public virtual void OnMessage(Bomb bomb, string address, object value) // Called whenever a OSC message is received from a node. You can only receive one argument with this method.
public virtual void Update(Bomb bomb) // Called at 60Hz
public virtual void Sync(Bomb bomb) // Called when a node connects and at regular intervals to mitigate desyncing

public virtual void OnStrike(Bomb bomb) // Called when an error is made and a strike is registered
public virtual void OnModuleDefused(Bomb bomb) // Called when any module is defused
public virtual void OnStart(Bomb bomb) // Called the timer is started
public virtual void OnStop(Bomb bomb) // Called when timer is paused
public virtual void OnExplode(Bomb bomb) // Called when timer runs out or 3 strikes are accrued
public virtual void OnBombDefused(Bomb bomb) // Called when all modules are defused 
```

You have access to the `Bomb` the module belongs to in all the callbacks. `Bomb`s have the following methods:
```cs
public void QueueMessage(OscMessage message) // Sends an OscMessage to all nodes connected to this bomb
public void Beep(float time) // Beep the BombNode's buzzer for time seconds.
public void AddStrike() // Adds a strike, runs callbacks, explodes if strikes >= 3
public void Explode() // Instantly explode and lose the game, should probably use AddStrike() instead
public int[] GetTimeDigits() // Get the current timer digits of the bomb as [minuteTens, minuteOnes, secondTens, secondOnes]. Do not do your own maths to calculate this, as there are some rounding particularities.
public async Task BlinkLights(int[]colour, float[][] sequence, string address, float fadeSpeed, int priority) // Check the in-project docs for details. Used to program flash sequences, such as when the bomb explodes.
```

A module begins its lifetime when the bomb is reset or started for the first time, at which point the module is instantiated. When the bomb is reset, the module is disposed and a new one is created. 

Good practice: Scope all your module's OSC messages. Eg. for the first led of the "symbols" module, `/symbols/led1`. This way modules will not interfere with each other.
