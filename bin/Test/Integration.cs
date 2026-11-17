using System;
using System.Threading;
using RemotePC.Server;
using RemotePC.Client;
using RemotePC.Common;

class Program {
  static void Main() {
    var server = new RemoteServer();
    server.Password = "1234";
    server.Start(18888);
    Thread.Sleep(500);
    var client = new RemoteClient();
    bool ok = client.Connect("127.0.0.1", 18888, "1234", 5000);
    Console.WriteLine("Connect: " + ok + " LastError=" + client.LastError);
    if (ok) {
      Thread.Sleep(500);
      client.Disconnect();
    }
    server.Stop();
  }
}