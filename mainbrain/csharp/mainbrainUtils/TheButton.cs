using CoreOSC;
using Main;
using Stride.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main;

public class TheButton : BombModule
{
    public int[] lights = new int[8];

    public int stepA;

    enum Mode
    {
        A,
        B,
        C
    }

    int[][] stepALights =
    [
        new int[] { 6 }, // right "LEFT" light
        new int[] { 1, 3 }, // two square lights
        new int[] { 0 }, // Any of the 
        new int[] { 4 }, //            two leftmost lights
        new int[] { 7 }, // The only button without text or a shape
        new int[] {  } // Otherwise
    ];

    Mode[] stepAMode = new Mode[]
    {
        Mode.A,
        Mode.C,
        Mode.A,
        Mode.A,
        Mode.B,
        Mode.C
    };

    public TheButton(Random rng)
    {
        stepA = rng.Next(0, 6);
        for (int i = 0; i < stepA; i++) // Disable lights for steps up to the chosen one
        {
            for (int n = 0; n < stepALights[i].Length; n++)
            {
                lights[stepALights[i][n]] = -1; // -1 are lights that must not be lit
            }
        }
        for (int n = 0; n < stepALights[stepA].Length; n++) // Enable lights for the chosen step
        {
            lights[stepALights[stepA][n]] = 1; // 1 are lights that must be lit
        }

        lights[2] = 1; // For mode C this needs to be on, but it isn't used by anything else so it can be on all the time

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
        if (address == "/button"){
            if (value is OscTrue)
            {
                switch (stepAMode[stepA]) // Button pressed
                {
                    case Mode.A:
                        Debug.WriteLine("AA");
                        break;
                    case Mode.B:
                        Debug.WriteLine("bb");
                        if (bomb.GetTimeDigits().Any(d => d == 5))
                        {
                            defused = true;
                        }
                        else
                        {
                            bomb.AddStrike();
                        }
                        break;
                    case Mode.C:
                        break;
                }
            }
            else
            {
                switch (stepAMode[stepA]) // Button released
                {
                    case Mode.A:
                    case Mode.C:
                        Debug.WriteLine("AC");
                        if (lights[5] == 1)
                        {
                            Debug.WriteLine("yadda");
                            Debug.WriteLine(bomb.GetTimeDigits()[3] % 2);
                            if (bomb.GetTimeDigits()[3] % 2 == 0)
                            {
                                defused = true;
                            }
                            else
                            {
                                bomb.AddStrike();
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Time digits: {string.Join("", bomb.GetTimeDigits())}");
                            if (bomb.GetTimeDigits().SubArray(0, 2).All(d => d != 3)){
                                defused = true;
                            }
                            else
                            {
                                bomb.AddStrike();
                            }
                        }
                        break;
                    case Mode.B:
                        break;
                }
            }
        }
    }

    public override void Sync(Bomb bomb)
    {
        List<Object> o = new();
        for(int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == 1)
            {
                o.Add(new OscTrue());
            }
            else
            {
                o.Add(new OscFalse());
            }
        }
        bomb.QueueMessage(new OscMessage(new CoreOSC.Address("/button/lights"), o));
        /*for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == 1)
            {
                bomb.QueueMessage(new OscMessage(new CoreOSC.Address($"/button/light/on"), [i]));
            }
            else
            {
                bomb.QueueMessage(new OscMessage(new CoreOSC.Address($"/button/light/off"), [i]));
            }
        }*/
    }
}