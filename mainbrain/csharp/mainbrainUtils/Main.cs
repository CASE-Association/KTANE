// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples


namespace Main;

using CommunityToolkit.HighPerformance;
using CoreOSC;
using CoreOSC.IO;
using CoreOSC.Types;
using Stride.Core.Diagnostics;
using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using VL.Core;
using VL.Core.Utils;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member


public class OscMsgPack: Dictionary<string, IEnumerable<object>>;


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
    Dictionary<IPAddress, int> ipToId = [];
    Dictionary<int, List<IPAddress>> idToIps = [];
    public Dictionary<int, Bomb> idToBomb = [];

    OscMessageConverter converter = new();

    Stopwatch sw = Stopwatch.StartNew();

    IPEndPoint any = new(IPAddress.Any, 4444);

    int heartbeat = 0;

    public BombConnectionManager()
    {
        receiver = new UdpClient(new IPEndPoint(new IPAddress([0,0,0,0]), 4000));
        sender = new UdpClient();
    }


    public void Update() // Called by VVVV
    {
        float delta = ((float)sw.ElapsedMilliseconds / 1000.0f);
        sw.Restart();

        while (receiver.Available > 0)
        {
            IPEndPoint senderEndpoint = new IPEndPoint(IPAddress.Any, 4500);
            byte[] data = receiver.Receive(ref senderEndpoint);
            converter.Deserialize(BytesToDwords(data), out OscMessage msg);
            IPAddress senderAddress = senderEndpoint.Address;

            String address = msg.Address.Value.ToString();

            Console.Write($"Received message from {senderAddress}: {address} with arguments: [ ");
            foreach(var arg in msg.Arguments)
            {
                Console.Write($"{arg} ");
            };
            Console.WriteLine("]");

            if (address == "/connect")
            {
                int ip = (int)msg.Arguments.ElementAt(0);

                var bytes = OscToBytes(new OscMessage(new CoreOSC.Address("/ok"), [ip]));
                sender.Send(bytes, new IPEndPoint(senderAddress, 5000));


                int id = (int)msg.Arguments.ElementAt(1);

                ipToId[senderAddress] = id;
                if (!idToIps.TryGetValue(id, out List<IPAddress>? value))
                {
                    value = new List<IPAddress>();
                    idToIps[id] = value;

                    Console.WriteLine($"Module connected: BombID {id} from {senderAddress}");
                }

                if (!value.Contains(senderAddress))
                {
                    value.Add(senderAddress);
                }

                if (!idToBomb.ContainsKey(id))
                {
                    idToBomb[id] = new Bomb
                    {
                        connectionManager = this
                    };
                    Console.WriteLine($"Bomb created: BombID {id}");
                }
                idToBomb[id].Sync();
            }
            else
            {
                if (ipToId.TryGetValue(senderAddress, out int id))
                {
                    if (idToBomb.TryGetValue(id, out Bomb? value))
                    {
                        value.OnMessage(msg);   
                    }
                }
            }
        }

        // Regularly trigger full sync on all modules
        // TODO reduce this
        if (heartbeat > 0) 
        {
            heartbeat--;
        }
        else
        {
            heartbeat = 60 * 10; // every ten secs
            foreach (var kvp in idToBomb)
            {
                kvp.Value.Sync();
            }
            foreach (var kvp in idToIps)
            {
                foreach (IPAddress ip in kvp.Value)
                {
                    OscMessage h = new(new CoreOSC.Address("/heartbeat"));
                }
            }
        }

        foreach (var kvp in idToBomb) // "Game loop" + retreive messages from modules and transmit them to nodes
        {
            var messages = kvp.Value.Update(delta);
            if (messages != null && messages.Count > 0)
            {
                foreach (OscMessage retMessage in messages)
                {
                    var bytes = OscToBytes(retMessage);
                    foreach (IPAddress ip in idToIps[kvp.Key])
                    {
                        sender.Send(bytes, new IPEndPoint(ip, 5000));
                        Console.WriteLine($"Sent message to {ip} for BombID {kvp.Key}: {retMessage.Address.Value}");
                    }

                }
            }
        }
    }

    private byte[] OscToBytes(OscMessage message)
    {
        var dwords = converter.Serialize(message);
        DwordsToBytes(dwords, out IEnumerable<byte> value);
        return [.. value];
    }

    private static IEnumerable<DWord> BytesToDwords(IEnumerable<byte> value)
    {
        if (!value.Any())
        {
            return [];
        }

        var next = value.Take(4);
        var dWord = new DWord([.. next]);
        return new[] { dWord }.Concat(BytesToDwords(value.Skip(4)));
    }

    public static IEnumerable<DWord> DwordsToBytes(IEnumerable<DWord> dWords, out IEnumerable<byte> value)
    {
        if (!dWords.Any())
        {
            value = [];
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