using System;
using System.Threading;
using RemotePC.Server;
using RemotePC.Client;

class Program {
  static void Main() {
    var server = new RemoteServer();
    server.Password = "1234";
    server.Start(18890);
    Thread.Sleep(500);
    var client = new RemoteClient();
    bool ok = client.Connect("[::1]", 18890, "1234", 5000);
    Console.WriteLine("Bracket IPv6 Connect: " + ok + " LastError=" + client.LastError);
    if (ok) { Thread.Sleep(200); client.Disconnect(); }
    server.Stop();
  }
}