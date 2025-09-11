using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main;

public class BackgroundModule: BombModule
{
    int lastSeconds = -1;

    public BackgroundModule(Bomb bomb)
    {
        defused = true;
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/postgame_happy"), [new CoreOSC.OscFalse()]));
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/postgame_sad"), [new CoreOSC.OscFalse()]));
    }

    public override void Update(Bomb bomb)
    {
        int t = (int)Math.Ceiling(bomb.secondsRemaining);
        bool sync = false;
        if (t < lastSeconds)
        {
            sync = true;
            bomb.Beep(0.01f);
        }
        ;
        lastSeconds = t;

        if (sync)
        {
            Sync(bomb);
        }
    }

    public override void Sync(Bomb bomb)
    {
        var x = bomb.GetTimeDigits();
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/timer"), [x[0], x[1], x[2], x[3]]));
        //bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/strikes"), [bomb.strikes]));
    }



    public override void OnStrike(Bomb bomb)
    {
        bomb.BlinkLights([255, 0, 0], [[1.0f, 400.0f],[0.0f, 200.0f],[1.0f, 400.0f], [0.0f, 200.0f], [1.0f, 0.0f]], "/fx/lights/override", 0.04f, 2);
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/strike")));
    }

    public override void OnModuleDefused(Bomb bomb)
    {
        bomb.BlinkLights([0, 255, 0], [[1.0f, 0.0f]], "/fx/lights/override", 0.02f, 1);
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/moduledefused")));
    }

    public override void OnBombDefused(Bomb bomb)
    {
        bomb.BlinkLights([255, 255, 255], [[1.0f, 0.0f]], "/fx/lights/override", 0.002f, 10);
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/bombdefused")));
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/postgame_happy"), [new CoreOSC.OscTrue()]));
    }

    public override void OnExplode(Bomb bomb)
    {
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/explode")));
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/postgame_sad"), [new CoreOSC.OscTrue()]));
        ExplosionBlinkSequence(bomb);
    }

    public async Task ExplosionBlinkSequence(Bomb bomb)
    {
        bomb.BlinkLights([0, 0, 255],
            [
            [1.0f, 62.0f], [0.01f, 63.0f],
            [1.0f, 62.0f], [0.01f, 63.0f],
            [1.0f, 62.0f], [0.01f, 63.0f],
            [1.0f, 62.0f], [0.01f, 63.0f]], "/fx/lights/override", 1.0f, 10);
        await Task.Delay(875);
        List<float[]> seq = new List<float[]>();
        for(float v = 1.0f; v >= 0.0f; v -= 0.01f)
        {
            seq.Add(new float[] { v, 70.0f });
            seq.Add(new float[] { 0.01f, 70.0f });
        }
        bomb.BlinkLights([255, 0, 0], seq.ToArray(), "/fx/lights/override", 1.0f, 10);
    }

    public override void OnStart(Bomb bomb)
    {
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/start"), [bomb.secondsRemaining]));
    }

    public override void OnStop(Bomb bomb)
    {
        bomb.QueueMessage(new CoreOSC.OscMessage(new CoreOSC.Address("/fx/audio/stop")));
    }
}