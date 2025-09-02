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

    private bool lastRunning = false;
    private int lastNumDefused = 0;

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
        lastNumDefused = 0;
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
        if(lastRunning != running)
        {
            if (running)
            {
                foreach(var module in modules)
                {
                    module.OnStart(this);
                }
            }
            else
            {
                foreach (var module in modules)
                {
                    module.OnStop(this);
                }
            }
            lastRunning = running;
        }

        if (running)
        {
            secondsRemaining -= delta;
        }
        deltaTime = delta;

        bool allDefused = true;
        int numDefused = 0;
        foreach (var module in modules)
        {
            module.Update(this);
            if (!module.defused)
            {
                allDefused = false;
            }
            else
            {
                numDefused++;
            }
        }

        

        if (allDefused && !defused)
        {
            defused = true;
            running = false;
            foreach (var module in modules)
            {
                module.OnBombDefused(this);
            }
            Console.WriteLine("Bomb defused!");
        }
        else if (numDefused != lastNumDefused)
        {
            lastNumDefused = numDefused;
            foreach (var module in modules)
            {
                module.OnModuleDefused(this);
            }
        } else if (secondsRemaining <= 0 && !defused)
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
        else
        {
            foreach (var module in modules)
            {
                module.OnStrike(this);
            }
        }
    }

    private void Explode()
    {
        // TODO explode
        running = false;
        foreach (var module in modules)
        {
            module.OnExplode(this);
        }
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

    int blinkLightPriority = 0;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="colour">3 int array containing the colour to blink.</param>
    /// <param name="sequence">A list of steps. Each step has two floats: one for brightness, one for step length</param>
    /// <param name="bomb"></param>
    /// <returns></returns>
    public async Task BlinkLights(int[] colour, float[][] sequence, string address, float fadeSpeed, int priority)
    {
        if (priority < blinkLightPriority && priority != -1)
        {
            return;
        }
        if (priority != -1)
        {
            blinkLightPriority = priority;
        }
        CoreOSC.Address addr = new CoreOSC.Address(address);
        float val = 0.0f;
        for (int i = 0; i < sequence.Length; i++)
        {
            if (priority < blinkLightPriority && priority != -1)
            {
                return;
            }
            val = sequence[i][0];
            this.QueueMessage(new CoreOSC.OscMessage(addr, [(int)(colour[0] * val), (int)(colour[1] * val), (int)(colour[2] * val)]));
            await Task.Delay((int)sequence[i][1]);
        }
        while (val > 0.0f)
        {
            if (priority < blinkLightPriority && priority != -1)
            {
                return;
            }
            val -= fadeSpeed;
            if (val < 0.0f) val = 0.0f;
            this.QueueMessage(new CoreOSC.OscMessage(addr, [(int)(colour[0] * val), (int)(colour[1] * val), (int)(colour[2] * val)]));
            await Task.Delay(50);
        }

        blinkLightPriority = 0;
    }
}

public abstract class BombModule
{
    //void Initialize(Random rng);
    public virtual void OnMessage(Bomb bomb, string address, object value) { }
    public virtual void Update(Bomb bomb) { }
    public virtual void Sync(Bomb bomb) { }
    public bool defused = false;

    public virtual void OnStrike(Bomb bomb) { }
    public virtual void OnModuleDefused(Bomb bomb) { }
    public virtual void OnStart(Bomb bomb) { }
    public virtual void OnStop(Bomb bomb) { }
    public virtual void OnExplode(Bomb bomb) { }
    public virtual void OnBombDefused(Bomb bomb) { }
}