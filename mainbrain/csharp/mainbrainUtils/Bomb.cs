using CoreOSC;
using Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VL.Lib.Animation;

namespace Main;

public class Bomb
{
    public BombConnectionManager connectionManager;
    public List<BombModule> modules = [];

    public int strikes = 1;

    public float secondsRemaining;
    public float timerLength = 60 * 10;

    public bool running = false;
    public bool defused = false;

    public int seed = 0;

    public Queue<string> latestMessages = new();

    public float deltaTime = 0.0f;

    float beepTime = 0.0f;

    public Bomb()
    {
        Init();
    }

    public void Init()
    {
        modules.Clear();
        secondsRemaining = timerLength;
        running = false;
        defused = false;
        strikes = 0;
        Random rng = new(seed);
        modules.Add(new WordMaze(rng));
        modules.Add(new Wires(rng));
        modules.Add(new TheButton(rng));
        modules.Add(new BackgroundModule());
        Sync();
    }

    List<OscMessage> messagequeue = [];


    public void OnMessage(OscMessage message)
    {

        foreach (var module in modules)
        {
            module.OnMessage(this, message.Address.Value.ToString(), message.Arguments.FirstOrDefault());
        }
    }

    public void Sync()
    {
        foreach (var module in modules)
        {
            module.Sync(this);
        }
    }



    public List<OscMessage> Update(float delta)
    {
        if (running)
        {
            secondsRemaining -= delta;
        }
        deltaTime = delta;

        bool allDefused = true;
        foreach (var module in modules)
        {
            module.Update(this);
            if (!module.defused)
            {
                allDefused = false;
            }

        }

        if (allDefused && !defused)
        {
            defused = true;
            running = false;
            Console.WriteLine("Bomb defused!");
        }

        if (secondsRemaining <= 0 && !defused)
        {
            Explode();
        }

        if (beepTime > 0.0f)
        {
            beepTime -= delta;
            if (beepTime <= 0.0f)
            {
                QueueMessage(new OscMessage(new CoreOSC.Address("/beep"), [new OscFalse()]));
            }
        }

        if (messagequeue.Count > 0)
        {
            List<OscMessage> temp = new(messagequeue);
            messagequeue.Clear();
            return temp;
        }
        return null;
    }

    public void QueueMessage(OscMessage message)
    {
        messagequeue.Add(message);
        latestMessages.Enqueue($"{message.Address.Value.ToString()} [{string.Join(", ", message.Arguments)}]");
        if (latestMessages.Count > 5)
        {
            latestMessages.Dequeue();
        }
    }

    public void Beep(float time)
    {
        beepTime = Math.Max(time, beepTime);
        QueueMessage(new OscMessage(new CoreOSC.Address("/beep"), [new OscTrue()]));
    }

    public void AddStrike()
    {
        strikes++;
        if (strikes >= 3)
        {
            Explode();
        }
    }

    private void Explode()
    {
        // TODO explode
        running = false;
        Console.WriteLine("Bomb exploded!");
    }

    public int[] GetTimeDigits()
    {
        int s = (int)Math.Ceiling(secondsRemaining);

        int minutes = (int)(s / 60);
        int seconds = (int)(s % 60);

        return new int[] {
            minutes / 10,
            minutes % 10,
            seconds / 10,
            seconds % 10
        };
    }
}

public abstract class BombModule
{
    //void Initialize(Random rng);
    public virtual void OnMessage(Bomb bomb, string address, object value) { }
    public virtual void Update(Bomb bomb) { }
    public virtual void Sync(Bomb bomb) { }
    public bool defused = false;
}