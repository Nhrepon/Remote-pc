using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RemotePC.Common;

namespace RemotePC.Tests
{
    class Scratch
    {
        static int Main()
        {
            int failures = 0;

            // 1. AppSettings: defaults + round trip on renamed keys.
            AppSettings s = new AppSettings();
            Check(ref failures, s.ServerPort == 8888, "default ServerPort");
            Check(ref failures, s.AllowFileTransfer, "default AllowFileTransfer");
            Check(ref failures, s.LastHost == "", "default LastHost");
            Check(ref failures, !s.FirstRunComplete, "default FirstRunComplete");
            Check(ref failures, !string.IsNullOrEmpty(s.Password), "default Password generated");

            // 2. AppShortcutHelper exposes every signature MainForm calls.
            Check(ref failures, HasOverload("AppShortcutHelper", "CreateStartMenuShortcut", 3), "CreateStartMenuShortcut(exe,name,out loc)");
            Check(ref failures, HasOverload("AppShortcutHelper", "CreateDesktopShortcut", 3), "CreateDesktopShortcut(exe,name,out loc)");
            Check(ref failures, HasOverload("AppShortcutHelper", "CreateStartupShortcut", 3), "CreateStartupShortcut(exe,name,out loc)");
            Check(ref failures, HasOverload("AppShortcutHelper", "SetStartWithWindows", 2), "SetStartWithWindows(bool,out err)");

            // 3. NetworkDiscovery / UPnP signatures MainForm calls.
            Check(ref failures, HasOverload("NetworkDiscovery", "StartBroadcasting", 1), "StartBroadcasting(port)");
            Check(ref failures, HasOverload("UPnPPortForwarder", "ForwardPortAsync", 2), "ForwardPortAsync(port, cb)");

            // 4. FirewallHelper surface used by Program/MainForm.
            Check(ref failures, HasOverload("FirewallHelper", "EnsureRules", 2), "EnsureRules(ports,out err)");
            Check(ref failures, HasOverload("FirewallHelper", "ApplyRulesElevated", 2), "ApplyRulesElevated(ports,out err)");
            Check(ref failures, FirewallHelper.SetupSwitch == "-setupfw", "SetupSwitch value");

            // 5. Read-only: rule query must not throw and must work unelevated.
            bool exists = FirewallHelper.RuleExists(FirewallHelper.RuleNameForDiscovery());
            Console.WriteLine("[INFO] Discovery firewall rule present: " + exists);

            Console.WriteLine(failures == 0 ? "[PASS] scratch checks OK" : "[FAIL] " + failures + " checks failed");
            return failures == 0 ? 0 : 1;
        }

        static bool HasOverload(string type, string method, int paramCount)
        {
            Type t = Type.GetType("RemotePC.Common." + type);
            if (t == null) return false;
            MethodInfo[] all = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo m in all)
            {
                if (m.Name == method && m.GetParameters().Length == paramCount) return true;
            }
            return false;
        }

        static void Check(ref int failures, bool ok, string label)
        {
            Console.WriteLine((ok ? "[ok]   " : "[FAIL] ") + label);
            if (!ok) failures++;
        }
    }
}
