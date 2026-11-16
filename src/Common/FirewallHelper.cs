using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;

namespace RemotePC.Common
{
    /// <summary>
    /// Creates the Windows Defender Firewall rules the app needs so that a user
    /// never has to touch the firewall by hand. Rules are created once, on first
    /// run, through a single UAC prompt; afterwards the app runs as a normal user.
    /// </summary>
    public static class FirewallHelper
    {
        public const string RulePrefix = "RemotePC";

        /// <summary>Command-line switch used to re-launch this exe elevated for setup only.</summary>
        public const string SetupSwitch = "-setupfw";

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        public static string RuleNameForTcp(int port)
        {
            return RulePrefix + " Server TCP " + port;
        }

        public static string RuleNameForDiscovery()
        {
            return RulePrefix + " LAN Discovery UDP " + NetworkDiscovery.DISCOVERY_PORT;
        }

        /// <summary>Rule exists? Querying rules needs no elevation.</summary>
        public static bool RuleExists(string ruleName)
        {
            string output = RunNetsh("advfirewall firewall show rule name=\"" + ruleName + "\"", false);
            return output != null && output.IndexOf(ruleName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Makes sure the inbound rules for the server port and LAN discovery exist.
        /// Returns true when the rules are in place afterwards.
        /// </summary>
        public static bool EnsureRules(IList<int> tcpPorts, out string error)
        {
            error = null;
            try
            {
                bool needFix = !RuleExists(RuleNameForDiscovery());
                if (!needFix)
                {
                    for (int i = 0; i < tcpPorts.Count; i++)
                    {
                        if (!RuleExists(RuleNameForTcp(tcpPorts[i]))) { needFix = true; break; }
                    }
                }

                if (!needFix) return true;

                if (IsAdministrator())
                {
                    return ApplyRulesElevated(tcpPorts, out error) == 0;
                }

                // Not elevated: ask Windows once for permission, then apply.
                if (!RelaunchSelfElevatedForSetup(tcpPorts, out error))
                    return false;

                // Verify the elevated helper really created them.
                if (!RuleExists(RuleNameForDiscovery())) return false;
                for (int i = 0; i < tcpPorts.Count; i++)
                {
                    if (!RuleExists(RuleNameForTcp(tcpPorts[i]))) return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Runs inside the elevated instance (see Program.Main).</summary>
        public static int ApplyRulesElevated(IList<int> tcpPorts, out string error)
        {
            error = null;
            try
            {
                List<string> commands = new List<string>();

                if (!RuleExists(RuleNameForDiscovery()))
                {
                    commands.Add("advfirewall firewall add rule name=\"" + RuleNameForDiscovery() +
                                 "\" dir=in action=allow protocol=UDP localport=" + NetworkDiscovery.DISCOVERY_PORT +
                                 " profile=any");
                }

                for (int i = 0; i < tcpPorts.Count; i++)
                {
                    int port = tcpPorts[i];
                    if (RuleExists(RuleNameForTcp(port))) continue;
                    commands.Add("advfirewall firewall add rule name=\"" + RuleNameForTcp(port) +
                                 "\" dir=in action=allow protocol=TCP localport=" + port +
                                 " profile=any");
                }

                // Always refresh the program-scoped rule so every port the app listens on is covered.
                // The delete is allowed to fail when the rule is not there yet.
                RunNetsh("advfirewall firewall delete rule name=\"" + RulePrefix + " Application\"", false);
                commands.Add("advfirewall firewall add rule name=\"" + RulePrefix + " Application\"" +
                             " dir=in action=allow program=\"" + Process.GetCurrentProcess().MainModule.FileName + "\"" +
                             " enable=yes profile=any");

                for (int i = 0; i < commands.Count; i++)
                {
                    string output = RunNetsh(commands[i], true);
                    if (output == null)
                    {
                        error = "netsh failed for: " + commands[i];
                        return 1;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return 1;
            }
        }

        private static bool RelaunchSelfElevatedForSetup(IList<int> tcpPorts, out string error)
        {
            error = null;
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;

                StringBuilder args = new StringBuilder();
                args.Append(SetupSwitch);
                for (int i = 0; i < tcpPorts.Count; i++)
                {
                    args.Append(' ');
                    args.Append(tcpPorts[i]);
                }

                ProcessStartInfo psi = new ProcessStartInfo(exe, args.ToString());
                psi.UseShellExecute = true;
                psi.Verb = "runas"; // triggers the single UAC prompt
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        error = "Could not start the elevated setup helper.";
                        return false;
                    }
                    p.WaitForExit(30000);
                }
                return true;
            }
            catch (Exception ex)
            {
                // User declined the UAC prompt: not fatal, the app still runs.
                error = ex.Message;
                return false;
            }
        }

        private static string RunNetsh(string arguments, bool requireSuccess)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(20000);
                    if (requireSuccess && p.ExitCode != 0) return null;
                    return output;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
