using CoreOSC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main;

public class WordMaze : BombModule
{
    public int x;
    public int y;

    public int targetX;
    public int targetY;

    int directionChoice = 0;

    public int mazeSize = 4;

    string[] maze;

    public WordMaze(Random rng)
    {

        string mazeSrc = "de,dem,dom,,vänta,,,,vad\r\nsa du,,,va,,fel,,nej,,tvärtom,,,inget,börja om,,,inget,,,,,sig,säg,sej,,,bakåt,,,stop,,stopp,vänta,,,,,skäl,själ,stjäl,,,höger,vänster,jord,,hjord,gjord,åter,,,ja,sett,,sätt,set";
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

        if (changed) bomb.QueueMessage(DisplayMessage(bomb));

        if (changed)
        {
            Console.WriteLine($"WordMaze: x={x}, y={y}, targetX={targetX}, targetY={targetY}, directionChoice={directionChoice}");
            Console.WriteLine($"{GetWord(x, y, directionChoice)}");
        }
    }

    int lastStrikes = -1;

    public override void Update(Bomb bomb)
    {
        if (bomb.strikes != lastStrikes)
        {
            lastStrikes = bomb.strikes;
            Sync(bomb);
        }
    }

    private string FixString(string s)
    {
        return s.Replace('å', '(').Replace('ä', '{').Replace('ö', '[');
    }

    private OscMessage DisplayMessage(Bomb bomb)
    {
        // å (
        // ä {
        // ö [
        string line1;
        string line2;
        if (defused)
        {
            line1 = "";
            line2 = "MAZE DEFUSED";
        }
        else
        {
            line1 = ("TARGET: " + ("ABCDEFG"[targetX]) + (targetY + 1));
            
            string word = GetWord(x, y, directionChoice);
            word.PadLeft((16 - word.Length) / 2 + word.Length);
            line2 = word;
        }

        line1 = line1.PadRight(13);
        for (int i = 0; i < 3; i++)
        {
            if (i < bomb.strikes)
            {
                line1 += "X";
            }
            else
            {
                line1 += ".";
            }
        }

        return new OscMessage(new CoreOSC.Address("/wordmaze/display"), [FixString(line1), FixString(line2)]);
    }

    public override void Sync(Bomb bomb)
    {
        bomb.QueueMessage(DisplayMessage(bomb));
    }
}

