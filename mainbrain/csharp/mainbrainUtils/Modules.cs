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

    public List<OscMessage> OnMessage(Bomb bomb, string address, object msg)
    {
        if (defused) return new List<OscMessage>();
        List<OscMessage> ret = new List<OscMessage>();
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

        if (changed) ret.Add(DisplayMessage());

        if (changed)
        {
            Console.WriteLine($"WordMaze: x={x}, y={y}, targetX={targetX}, targetY={targetY}, directionChoice={directionChoice}");
            Console.WriteLine($"{GetWord(x, y, directionChoice)}");
        }

        return ret;
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

    public List<OscMessage> Sync(Bomb bomb)
    {
        return new List<OscMessage>
        {
            DisplayMessage(),
        };
    }
}

public class Wires : BombModule
{
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
        To3wires2 = -3,
        Skip = -1,
        Stop = -2,
        Wire1 = 0,
        Wire2 = 1,
        Wire3 = 2,
        Wire4 = 3,
        Wire5 = 4,
        CutAndStop = 5
    }

    public Wire[] wires;

    public bool defused = false;
    public int correctAction = -1;

    public string activatedRule = "";

    public void ComputeNextAction()
    {
        currentRuleset = new string[]{"", "", "", "3wires", "4wires", "5wires"} [wires.Count(wire => wire != Wire.Cut)];
        int instruction = 0;
        while (true)
        {
            Console.WriteLine($"Current ruleset: {currentRuleset}, instruction: {instruction}");
            int res = (int)rulesets[currentRuleset][instruction].DynamicInvoke(wires);
            if(res >= 0)
            {
                correctAction = res;
                activatedRule = currentRuleset + " rule " + instruction;
                break;
            }
            else if(res == (int)Op.To5wires)
            {
                currentRuleset = "5wires";
                instruction = 0;
                continue;
            }
            else if(res == (int)Op.To3wires2)
            {
                instruction = 0;
                currentRuleset = "3wireswip";
                continue;
            }
            instruction++;
        }
    }

    string currentRuleset = "5wires";
    Dictionary<string, List<Delegate>> rulesets = new Dictionary<string, List<Delegate>>()
    {
        { "3wires", new List<Delegate>{
            (Wire[] wires) => wires[3] == Wire.Cut && wires[4] == Wire.Cut ? (int)Op.To3wires2 : (int)Op.Skip, // If the bottom two wires are cut, proceed to “3 wires WIP” ruleset.
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
            (Wire[] wires) => wires.Count(wire => wire == Wire.Red) == 0 ? GetNthUncutWire(wires, 4) : (int)Op.Skip, // If there are no red wires, cut the fourth wire.
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