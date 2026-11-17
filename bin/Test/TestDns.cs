using System;
using System.Net;
class Program {
  static void Main() {
    string[] hosts = new[]{"2001:db8::1","::1","localhost","127.0.0.1"};
    foreach (var h in hosts) {
      try {
        var a = Dns.GetHostAddresses(h);
        Console.WriteLine(h + " -> " + string.Join(",", (object[])a));
      } catch (Exception e) {
        Console.WriteLine(h + " -> ERROR: " + e.GetType().Name + ": " + e.Message);
      }
    }
  }
}
