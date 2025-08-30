using Stride.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main;

public class Wires : BombModule
{
    public Wire[] wires;

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
        if (address.StartsWith("/wires/cut"))
        {
            int wireIndex = (int)value;
            if (wireIndex >= 0 && wireIndex < wires.Length)
            {
                wires[wireIndex] = Wire.Cut;
                Console.WriteLine($"Wire {wireIndex} cut.");

                if (correctAction >= 0 && !defused)
                {
                    int correctWire = correctAction % 5;
                    if (wireIndex == correctWire)
                    {
                        Console.WriteLine($"Correct wire cut");
                        if (correctAction >= (int)Op.CutAndStop)
                        {
                            defused = true;
                            Console.WriteLine("Wires defused!");
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Wrong wire cut, expected {correctWire}");
                        bomb.AddStrike();
                    }
                }
                else
                {
                    Console.WriteLine($"Wire {wireIndex} cut, but no action expected.");
                    bomb.AddStrike();
                }
                ComputeNextAction();
                if (correctAction == (int)Op.Stop)
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
        currentRuleset = new string[] { "stop", "stop", "stop", "3wires", "4wires", "5wires" }[wires.Count(wire => wire != Wire.Cut)];
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
            if (action >= 0)
            {
                correctAction = action;
                activatedRule = currentRuleset + " rule " + instruction;
                break;
            }
            else if (action == (int)Op.To5wires)
            {
                currentRuleset = "5wires";
                instruction = 0;
                continue;
            }
            else if (action == (int)Op.To3wiresWIP)
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
            (Wire[] wires) => (int)Op.Stop, // Otherwise, stop.
        }},
        { "stop", new List<Delegate>{
            (Wire[] wires) => (int)Op.Stop
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