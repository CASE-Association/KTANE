using CoreOSC;
using ServiceWire.TcpIp;
using Stride.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using static Stride.Core.Diagnostics.VTuneProfiler;
using static System.Runtime.InteropServices.JavaScript.JSType;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Main;

public class WordMaze : BombModule
{
    int x;
    int y;

    int targetX;
    int targetY;

    int directionChoice = 0;

    int mazeSize = 4;

    bool defused = false;

    string[] maze;

    public WordMaze(Random rng)
    {

        string mazeSrc = "de,dem,,dom,vad sa du,va,,,sig,säg,sej,,fel,,nej,,vänta,,,,,inget,,börja om,,tvärtom,,,stop,stopp,,,,,,inget,,skäl,själ,stjäl,höger,,vänster,,,,,bakåt,jord,,hjord,gjord,sett,,sätt,set,,,,vänta,,,åter,ja";
        maze = mazeSrc.Split(',');


        targetX = rng.Next(0, 4);
        targetY = rng.Next(0, 4);

        x = (targetX + 2 + rng.Next(0, 1)) % mazeSize;
        y = (targetY + 2 + rng.Next(0, 1)) % mazeSize;
    }

    private string GetWord(int x, int y, int dir)
    {
        Console.WriteLine($"GetWord: x={x}, y={y}, dir={dir}");
        return maze[x * mazeSize * 4 + y * 4 + dir];
    }

    public override void OnMessage(Bomb bomb, string address, object msg)
    {
        if (defused) return;
        bool changed = false;
        if (address == "/wordmaze/left")
        {
            do
            {
                directionChoice = (directionChoice + 3) % 4;
            }
            while (GetWord(x, y, directionChoice) == "");
            changed = true;

        }
        if (address == "/wordmaze/right")
        {
            do
            {
                directionChoice = (directionChoice + 1) % 4;
            }

            while (GetWord(x, y, directionChoice) == "");
            changed = true;
        }
        if (address == "/wordmaze/ok")
        {
            if (directionChoice == 0)
            {
                x = (x + 1) % mazeSize;
            }
            else if (directionChoice == 1)
            {
                y = (y + 1) % mazeSize;
            }
            else if (directionChoice == 2)
            {
                x = (x - 1 + mazeSize) % mazeSize;
            }
            else if (directionChoice == 3)
            {
                y = (y - 1 + mazeSize) % mazeSize;
            }
            changed = true;
            do
            {
                directionChoice = (directionChoice + 1) % 4;
            }
            while (GetWord(x, y, directionChoice) == "");
        }
        if (x == targetX && y == targetY)
        {
            defused = true;
        }

        if (changed) bomb.QueueMessage(DisplayMessage());

        if (changed)
        {
            Console.WriteLine($"WordMaze: x={x}, y={y}, targetX={targetX}, targetY={targetY}, directionChoice={directionChoice}");
            Console.WriteLine($"{GetWord(x, y, directionChoice)}");
        }
    }

    private OscMessage DisplayMessage()
    {
        string msg;
        if (defused)
        {
            msg = "DEFUSED DEFUSED\nDEFUSED DEFUSED";
        }
        else
        {
            msg = "TARGET: " + ("ABCDEFG"[targetX]) + (targetY + 1) + "\n" + GetWord(x, y, directionChoice);
        }
        
        return new OscMessage(new CoreOSC.Address("/wordmaze/display"), [msg]);
    }

    public override void Sync(Bomb bomb)
    {
        bomb.QueueMessage(DisplayMessage());
    }
}

public class Wires : BombModule
{
    public Wire[] wires;

    public bool defused = false;
    public int correctAction = -1;

    public string activatedRule = "";
    string currentRuleset = "5wires";

    public Wires(Random rng)
    {
        wires = new Wire[5];
        for (int i = 0; i < wires.Length; i++)
        {
            wires[i] = (Wire)rng.Next(1, 5); // Randomly assign non-cut wires
        }
        ComputeNextAction();
    }

    public override void OnMessage(Bomb bomb, string address, object value)
    {
        if (address.StartsWith("/wires/cut/"))
        {
            int wireIndex = int.Parse(address.Split('/').Last());
            if (wireIndex >= 0 && wireIndex < wires.Length)
            {
                wires[wireIndex] = Wire.Cut;
                Console.WriteLine($"Wire {wireIndex} cut.");

                if(correctAction >= 0 && !defused)
                {
                    int correctWire = correctAction % 5;
                    if (wireIndex == correctWire)
                    {
                        Console.WriteLine($"Correct wire cut");
                        if(correctAction >= (int)Op.CutAndStop)
                        {
                            defused = true;
                            Console.WriteLine("Bomb defused!");
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Wrong wire cut, expected {correctWire}");
                        bomb.AddStrike();
                    }
                }else
                {
                    Console.WriteLine($"Wire {wireIndex} cut, but no action expected.");
                    bomb.AddStrike();
                }
                ComputeNextAction();
                if(correctAction == (int)Op.Stop)
                {
                    defused = true;
                }
            }
        }
    }


    public enum Wire
    {
        Cut,
        Red,
        Blue,
        Yellow,
        Black,
    }

    enum Op
    {
        To5wires = -4,
        To3wiresWIP = -3,
        Skip = -1,
        Stop = -2,
        Wire1 = 0,
        Wire2 = 1,
        Wire3 = 2,
        Wire4 = 3,
        Wire5 = 4,
        CutAndStop = 5
    }

    

    public void ComputeNextAction()
    {
        currentRuleset = new string[]{"stop", "stop", "stop", "3wires", "4wires", "5wires"} [wires.Count(wire => wire != Wire.Cut)];
        if (currentRuleset == "stop")
        {
            defused = true;
            correctAction = (int)Op.Stop;
        }
        int instruction = 0;
        while (true)
        {
            Console.WriteLine($"Current ruleset: {currentRuleset}, instruction: {instruction}");
            int action = (int)rulesets[currentRuleset][instruction].DynamicInvoke(wires);
            if(action >= 0)
            {
                correctAction = action;
                activatedRule = currentRuleset + " rule " + instruction;
                break;
            }
            else if(action == (int)Op.To5wires)
            {
                currentRuleset = "5wires";
                instruction = 0;
                continue;
            }
            else if(action == (int)Op.To3wiresWIP)
            {
                instruction = 0;
                currentRuleset = "3wireswip";
                continue;
            }
            instruction++;
        }
    }

    Dictionary<string, List<Delegate>> rulesets = new()
    {
        { "3wires", new List<Delegate>{
            (Wire[] wires) => wires[3] == Wire.Cut && wires[4] == Wire.Cut ? (int)Op.To3wiresWIP : (int)Op.Skip, // If the bottom two wires are cut, proceed to “3 wires WIP” ruleset.
            (Wire[] wires) => wires.Any(wire => wire == Wire.Black)? wires.IndexOf(wire => wire == Wire.Black) + (int)Op.CutAndStop : (int)Op.Skip, // If there is a Black wire, cut the top black wire and stop.
            () => (int)Op.Stop,
        }},
        { "4wires", new List<Delegate>{
            (Wire[] wires) => wires.Any(wire => wire == Wire.Red) ? (int)Op.To5wires : (int)Op.Skip, // If there is a red wire, proceed to “5 wires”.
            (Wire[] wires) => wires.Count(wire => wire == Wire.Yellow) > 1 ? 4 - wires.Reverse().IndexOf(wire => wire == Wire.Yellow) : (int)Op.Skip, // If there are more than 1 Yellow wire, cut the first Yellow wire.
            // If an even number of wires are cut, cut the first uncut wire. (unreachable, since there's 5 wires in total)
            (Wire[] wires) => wires[GetNthUncutWire(wires, 1)] != Wire.Yellow ? GetNthUncutWire(wires, 1) + (int)Op.CutAndStop : (int)Op.Skip, // If the top wire is not Yellow, cut it and stop.
            (Wire[] wires) => 4 - wires.Reverse().IndexOf(wire => wire != Wire.Cut), // Otherwise cut the last wire.
        }},
        { "5wires", new List<Delegate>{
            (Wire[] wires) => wires[GetNthUncutWire(wires, 1)] == Wire.Blue || wires[GetNthUncutWire(wires, 2)] == Wire.Blue ? wires.LastIndexOf(wire => wire == Wire.Blue) : (int)Op.Skip, // If there are any blue wires in the first two positions, cut the last blue wire.
            (Wire[] wires) => wires.Count(wire => wire == Wire.Red) > 1 ? GetNthUncutWire(wires, 4) : (int)Op.Skip, // If there is more than one red wire, cut the fourth wire.
            (Wire[] wires) => wires.Count(wire => wire != Wire.Cut) == 4 ? GetNthUncutWire(wires, 1) : (int)Op.Skip, // If there are exactly 4 uncut wires, cut the topmost wire.
            (Wire[] wires) => wires.Last() == Wire.Yellow || wires.Last() == Wire.Black ? GetNthUncutWire(wires, 2) : (int)Op.Skip, // If the last wire is Yellow or Black, cut the 2nd wire. // here we can use wires.Last() because we know there are 5 wires
            (Wire[] wires) => !wires.Any(wire => wire == Wire.Red) ? GetNthUncutWire(wires, 4) : (int)Op.Skip, // If there are no red wires, cut the fourth wire.
            (Wire[] wires) => GetNthUncutWire(wires, 3), // Otherwise, cut the middle wire.
        }},
        { "3wireswip", new List<Delegate>{
            (Wire[] wires) => wires[GetNthUncutWire(wires, 3)] == Wire.Blue ? GetNthUncutWire(wires, 1) + (int)Op.CutAndStop : (int)Op.Skip, // If the last wire is blue, cut the top wire and stop. getnth is required because wires.last() might be a cut wire
            () => (int)Op.Stop, // Otherwise, stop.
        }},
    };

    private static int GetNthUncutWire(Wire[] wires, int n) // 1-indexed, so n=1 is the first uncut wire
    {
        int count = 0;
        for (int i = 0; i < wires.Length; i++)
        {
            if (wires[i] != Wire.Cut)
            {
                count++;
                if (count == n)
                {
                    return i;
                }
            }
        }
        return -1; // Out of range!
    }
}

public class TheButton: BombModule
{
    bool buttonState = false;
    int[] lights = new int[8];

    int stepA;

    enum Mode
    {
        A,
        B,
        C
    }

    int[][] stepALights = new int[][]
    {
        new int[] { 6 }, // right "LEFT" light
        new int[] { 1, 3 }, // two square lights
        new int[] { 0 }, // Any of the 
        new int[] { 4 }, //            two leftmost lights
        new int[] { 7 }, // The only button without text or a shape
        new int[] {  } // Otherwise
    };

    Mode[] stepAMode = new Mode[]
    {
        Mode.A,
        Mode.C,
        Mode.A,
        Mode.A,
        Mode.B,
        Mode.C
    };

    TheButton(Random rng)
    {
        int stepA = rng.Next(0, 6);
        for(int i = 0; i < stepA; i++)
        {
            for(int n = 0; n < stepALights[i].Length; n++)
            {
                lights[stepALights[i][n]] = -1; // -1 are lights that must not be lit
            }
        }
        for (int n = 0; n < stepALights[stepA].Length; n++)
        {
            lights[stepALights[stepA][n]] = 1; // 1 are lights that must be lit
        }
        for (int i = 0; i < lights.Length; i++) // Fill the rest with random lights
        {
            if (lights[i] == 0)
            {
                lights[i] = rng.Next(0, 2);
            }
        }
    }

    public override void OnMessage(Bomb bomb, string address, object value)
    {
        if (address == "/button/press")
        {
            switch(stepAMode[stepA])
            {
                case Mode.A:
                    break;
                case Mode.B:
                    break;
                case Mode.C:
                    break;
            }
        }
    }

    public override void Sync(Bomb bomb)
    {
        for(int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == 1)
            {
                bomb.QueueMessage(new OscMessage(new CoreOSC.Address($"/button/light/{i}"), [true]));
            }
            else
            {
                bomb.QueueMessage(new OscMessage(new CoreOSC.Address($"/button/light/{i}"), [false]));
            }
        }
    }
}