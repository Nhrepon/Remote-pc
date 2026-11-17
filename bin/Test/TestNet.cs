using System;
using RemotePC.Common;
class Program {
  static void Main() {
    Console.WriteLine("Gateway: " + (NatPmpPortMapper.GetGateway() == null ? "null" : NatPmpPortMapper.GetGateway().ToString()));
  }
}
