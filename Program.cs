using DNS.Protocol;
using DNS.Server;
using LowLevelDesign.Hexify;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace testdns
{
    class Program
    {
        private static readonly object outputlck = new();

        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: testdns {slow|invalid|noresponse}");
            }

            var masterFile = new MasterFile();

            if (args[0] == "invalid" || args[0] == "noresponse")
            {
                var eth0 = NetworkInterface.GetAllNetworkInterfaces().First(
                    nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

                var domain = new Domain("example.com");
                masterFile.AddIPAddressResourceRecord(domain, eth0.GetIPProperties().UnicastAddresses.FirstOrDefault(
                                        ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address ?? IPAddress.None);
                masterFile.AddIPAddressResourceRecord(domain, eth0.GetIPProperties().UnicastAddresses.FirstOrDefault(
                                        ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6)?.Address ?? IPAddress.IPv6None);
            }

            var dnsServer = new DnsServer(masterFile, "1.1.1.1");

            dnsServer.Requested += (o, ev) => {
                for (int i = 0; i < ev.Request.Questions.Count; i++)
                {
                    var q = ev.Request.Questions[i];
                    Console.WriteLine($"#{ev.Remote.GetHashCode()} question[{i + 1}]: {q.Name} {q.Class} {q.Type}");
                }
                if (args[0] == "slow")
                {
                    var msToWait = 5000;
                    Console.WriteLine($"#{ev.Remote.GetHashCode()} delaying {msToWait} ms");
                    Thread.Sleep(msToWait);
                }
            };
            dnsServer.Responded += (o, ev) => {
                if (args[0] == "noresponse")
                {
                    Console.WriteLine($"#{ev.Remote.GetHashCode()} clearing response");
                    ev.Response.AnswerRecords.Clear();
                }
                else
                {
                    for (var i = 0; i < ev.Response.AnswerRecords.Count; i++)
                    {
                        var r = ev.Response.AnswerRecords[i];
                        lock (outputlck)
                        {
                            Console.WriteLine($"#{ev.Remote.GetHashCode()} answer[{i + 1}]: {r.Name} {r.Class} {r.Type}");
                            Console.WriteLine(Hex.PrettyPrint(r.Data));
                            Console.Out.Flush();
                        }
                    }
                }
            };

            await dnsServer.Listen(new IPEndPoint(IPAddress.Any, 53));
        }
    }
}
