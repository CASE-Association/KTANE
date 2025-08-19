# KTANE

## Folders
- MainBrain: PC server for the bombs, running all logic
- BombNode: ESP32 nodes, taking input and showing lights, text, sounds
- assets: Manuals, images etc.


# Modules
- WordMaze (homofoner)

## Architecture

```
       / Node <------------> /-----------\
Bomb 1 |                     |           |
       \ Node <------------> | MainBrain |
                             |           |
Bomb 2 - Node <------------> \-----------/
````


Nodes and MainBrain are connected on the same network (/24 subnet) and communicate via OSC. The MainBrain runs all logic and holds all state of the bombs.

Nodes are "stupid", they only receive and send OSC commands to the MainBrain. They tell the server when buttons are pushed and light lights when told so. 

Several bombs can be run from the same MainBrain, and several nodes can belong to each bomb. This allows for instance to separate the bomb unit and an room lighting unit.

On startup each node connects to the WiFi network and starts sending OSC messages to all devices on the /24 subnet containing their pre-assigned ID. When the MainBrain receives such a message, it registers the IP and ID of the node, and if no bomb already exists with that ID, it creates one. The MainBrain then sends a message back to the node so it knows the MainBrain's IP address.

All nodes (belonging to the same bomb ID) receive the same commands but only use those they are interested in. 


## Programming a bomb module
New bomb modules are added in the MainBrain in `mainbrain/csharp/mainbrainUtils/Modules.cs`. Modules implement three methods:
- `List<OscMessage> Update(Bomb bomb)`: Called at regular intervals for blinking lights or other realtime updates.
- `List<OscMessage> OnMessage(Bomb bomb, OscMessage msg)`: Called when a message is received from a node.
- `List<OscMessage> Sync(Bomb bomb)`: Called when a bomb is started, a new node connects and at regular intervals. This should send all current state of the bomb, so that any desynced nodes can catch up.

All functions can return a list of OSC messages that will be send to all nodes of the bomb.

Scope all OSC messages to the module, eg `/labyrinth/led1`.
