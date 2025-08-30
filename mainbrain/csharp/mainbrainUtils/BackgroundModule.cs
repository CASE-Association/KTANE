using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main;

public class BackgroundModule: BombModule
{
    int lastSeconds = -1;

    public BackgroundModule()
    {
        defused = true;
    }

    public override void Update(Bomb bomb)
    {
        int t = (int)Math.Ceiling(bomb.secondsRemaining);
        bool sync = false;
        if (lastSeconds != t)
        {
            sync = true;
            lastSeconds = t;
        }

        if(sync)
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
}