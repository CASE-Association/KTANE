// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples


namespace Main;

using CoreOSC;
using CoreOSC.IO;
using CoreOSC.Types;
using Stride.Core.Diagnostics;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using VL.Core;
using VL.Core.Utils;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class BombState
{
    public BombState(int seed)
    {
        Random random = new Random(seed);

        
    }
}


public class OscMsgPack: Dictionary<string, IEnumerable<object>>;

public class Bomb
{

    BombState state;
    public List<BombModule> modules = new List<BombModule>();

    public Bomb()
    {
        state = new BombState(0);

        Random rng = new Random(0);
        modules.Add(new WordMaze(rng));
        // Initialize bomb state
    }
    


    public List<OscMessage> OnMessage(OscMessage message)
    {
        List<OscMessage> ret = new List<OscMessage>();

        foreach (var module in modules)
        {
            var moduleMessages = module.OnMessage(this, message.Address.Value.ToString(), message.Arguments.FirstOrDefault());
            if (moduleMessages != null && moduleMessages.Count() > 0)
            {
                ret.AddRange(moduleMessages);
            }
        }

        Console.WriteLine("Retcount" + ret.Count());

        return ret;
    }
}

public abstract class BombModule
{
    //void Initialize(Random rng);
    public virtual List<OscMessage> OnMessage(Bomb bomb, string address, object value)
    {
        return null;
    }
    public virtual List<OscMessage> Update(Bomb bomb)
    {
        return null;
    }
    public virtual List<OscMessage> Sync(Bomb bomb)
    {
        return null;
    }
}


public static class OSC
{
    public static OscMessage NewOscMessage(string address, object value)
    {
        return new OscMessage(new CoreOSC.Address(address), [value]);
    }
}

// Host receives on 4000, sends on 4001
// Boms receive on 5000, send on 5001
public class BombConnectionManager
{
    UdpClient receiver;
    UdpClient sender;
    Dictionary<IPAddress, int> ipToId = new Dictionary<IPAddress, int>();
    Dictionary<int, List<IPAddress>> idToIps = new Dictionary<int, List<IPAddress>>();
    public Dictionary<int, Bomb> idToBomb = new Dictionary<int, Bomb>();

    OscMessageConverter converter = new OscMessageConverter();

    IPEndPoint any = new IPEndPoint(IPAddress.Any, 4444);

    int heartbeat = 0;

    public BombConnectionManager()
    {
        receiver = new UdpClient(new IPEndPoint(new IPAddress([0,0,0,0]), 4000));
        sender = new UdpClient();
    }


    public async void Update() // Called by VVVV
    {
        List<OscMessage> receivedMessages = new List<OscMessage>();
        while (receiver.Available > 0)
        {
            IPEndPoint senderEndpoint = new IPEndPoint(IPAddress.Any, 4500);
            byte[] data = receiver.Receive(ref senderEndpoint);
            converter.Deserialize(BytesToDwords(data), out OscMessage msg);
            IPAddress senderAddress = senderEndpoint.Address;

            String address = msg.Address.Value.ToString();

            Console.WriteLine($"Received message from {senderAddress}: {address} with arguments: {msg.Arguments.FirstOrDefault().ToString()}");

            if (address == "/connect")
            {
                int ip = (int)msg.Arguments.ElementAt(0);

                var bytes = OscToBytes(new OscMessage(new CoreOSC.Address("/ok"), [ip]));
                sender.Send(bytes, new IPEndPoint(senderAddress, 5000));


                int id = (int)msg.Arguments.ElementAt(1);

                ipToId[senderAddress] = id;
                if (!idToIps.ContainsKey(id))
                {
                    idToIps[id] = new List<IPAddress>();

                    Console.WriteLine($"Module connected: BombID {id} from {senderAddress}");
                }

                idToIps[id].Add(senderAddress);

                if (!idToBomb.ContainsKey(id))
                {
                    idToBomb[id] = new Bomb();
                    Console.WriteLine($"Bomb created: BombID {id}");
                }
            }
            else
            {
                if (ipToId.ContainsKey(senderAddress))
                {
                    int id = ipToId[senderAddress];
                    if (idToBomb.ContainsKey(id))
                    {
                        Bomb bomb = idToBomb[id];
                        List<OscMessage> ret = bomb.OnMessage(msg);
                        if (ret != null && ret.Count > 0)
                        {
                            foreach (OscMessage retMessage in ret)
                            {
                                var bytes = OscToBytes(retMessage);
                                foreach (IPAddress ip in idToIps[id])
                                {
                                    sender.Send(bytes, new IPEndPoint(ip, 5000));
                                    Console.WriteLine($"Sent response to {ip} for BombID {id}: {retMessage.Address.Value}");
                                }

                            }
                        }
                    }
                }
            }
        }

        if (heartbeat > 0)
        {
            heartbeat--;
        }
        else
        {
            heartbeat = 10 * 50;
            foreach(var kvp in idToIps)
            {
                foreach (IPAddress ip in kvp.Value)
                {
                    OscMessage h = new OscMessage( new CoreOSC.Address("/heartbeat"));
                }
            }
        }
    }

    private byte[] OscToBytes(OscMessage message)
    {
        var dwords = converter.Serialize(message);
        var responseData = DwordsToBytes(dwords, out IEnumerable<byte> value);
        return value.ToArray();
    }

    private IEnumerable<DWord> BytesToDwords(IEnumerable<byte> value)
    {
        if (!value.Any())
        {
            return new DWord[0];
        }

        var next = value.Take(4);
        var dWord = new DWord(next.ToArray());
        return new[] { dWord }.Concat(BytesToDwords(value.Skip(4)));
    }

    public IEnumerable<DWord> DwordsToBytes(IEnumerable<DWord> dWords, out IEnumerable<byte> value)
    {
        if (!dWords.Any())
        {
            value = new byte[0];
            return dWords;
        }
        var next = dWords.First().Bytes;
        var nextDWords = DwordsToBytes(dWords.Skip(1), out IEnumerable<byte> nextValue);
        value = next.Concat(nextValue);
        return nextDWords;
    }

    public void Dispose()
    {
        receiver.Close();
        receiver.Dispose();
    }

}