using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace RemotePC.Common
{
    /// <summary>
    /// Throttles PIN guessing on the listening port.
    ///
    /// Once the app can forward its port through the router the server is reachable from the
    /// whole internet, and a short PIN with unlimited attempts is worthless: an attacker simply
    /// tries every combination. This guard tracks failed attempts per source address and locks
    /// that address out after a small number of misses, which turns an exhaustive search from
    /// minutes into years while leaving a mistyped PIN barely noticeable.
    ///
    /// It also allows only a handful of authentication attempts per connection, so a single
    /// socket cannot be reused to keep guessing.
    /// </summary>
    public static class AuthGuard
    {
        /// <summary>Wrong PINs allowed from one address before it is locked out.</summary>
        public const int MaxFailuresPerAddress = 5;

        /// <summary>Wrong PINs allowed on a single connection before it is dropped.</summary>
        public const int MaxAttemptsPerConnection = 3;

        /// <summary>How long an address stays locked out after exhausting its attempts.</summary>
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        /// <summary>Window after which an address's failure count is forgotten.</summary>
        public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);

        private class Entry
        {
            public int Failures;
            public DateTime FirstFailureUtc;
            public DateTime LockedUntilUtc;
        }

        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Returns true when the address may try to authenticate. When it returns false it also
        /// reports how long the caller must wait, so the client can be told something useful.
        /// </summary>
        public static bool IsAllowed(string address, out string reason)
        {
            reason = null;

            string key = Normalize(address);
            if (key == null) return true;

            lock (_lock)
            {
                Prune();
                Entry entry;
                if (!_entries.TryGetValue(key, out entry)) return true;

                DateTime now = DateTime.UtcNow;
                if (entry.LockedUntilUtc > now)
                {
                    int minutes = (int)Math.Ceiling((entry.LockedUntilUtc - now).TotalMinutes);
                    reason = "Too many incorrect PIN attempts. Try again in about " + minutes + " minute(s).";
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// How many more wrong PINs the address can supply before it is locked out.
        /// </summary>
        public static int RemainingAttempts(string address)
        {
            string key = Normalize(address);
            if (key == null) return MaxFailuresPerAddress;

            lock (_lock)
            {
                Prune();
                Entry entry;
                if (!_entries.TryGetValue(key, out entry)) return MaxFailuresPerAddress;

                // While an address is locked out it has no attempts left, even though the
                // failure counter was reset when the lockout started.
                if (entry.LockedUntilUtc > DateTime.UtcNow) return 0;

                int left = MaxFailuresPerAddress - entry.Failures;
                return left > 0 ? left : 0;
            }
        }

        /// <summary>Records a wrong PIN for an address.</summary>
        public static void RecordFailure(string address)
        {
            string key = Normalize(address);
            if (key == null) return;

            lock (_lock)
            {
                Prune();
                Entry entry;
                if (!_entries.TryGetValue(key, out entry))
                {
                    entry = new Entry();
                    entry.FirstFailureUtc = DateTime.UtcNow;
                    _entries[key] = entry;
                }

                entry.Failures++;

                if (entry.Failures >= MaxFailuresPerAddress)
                {
                    entry.LockedUntilUtc = DateTime.UtcNow + LockoutDuration;
                    entry.Failures = 0;
                    entry.FirstFailureUtc = DateTime.UtcNow;
                }
            }
        }

        /// <summary>Clears the failure count for an address after a good PIN.</summary>
        public static void RecordSuccess(string address)
        {
            string key = Normalize(address);
            if (key == null) return;

            lock (_lock)
            {
                _entries.Remove(key);
            }
        }

        /// <summary>
        /// Reduces an endpoint such as "192.168.1.5:51234" to its address, so every new socket
        /// from the same machine shares one failure budget.
        /// </summary>
        private static string Normalize(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;

            try
            {
                int colon = address.LastIndexOf(':');
                if (colon > 0)
                {
                    string host = address.Substring(0, colon).Trim('[', ']');
                    IPAddress parsed;
                    if (IPAddress.TryParse(host, out parsed)) return parsed.ToString();
                }

                IPAddress direct;
                if (IPAddress.TryParse(address, out direct)) return direct.ToString();
            }
            catch { }

            return address;
        }

        /// <summary>Drops entries whose lockout and failure window have both expired.</summary>
        private static void Prune()
        {
            if (_entries.Count == 0) return;

            DateTime now = DateTime.UtcNow;
            List<string> stale = null;

            foreach (KeyValuePair<string, Entry> kv in _entries)
            {
                bool locked = kv.Value.LockedUntilUtc > now;
                bool recent = (now - kv.Value.FirstFailureUtc) < FailureWindow;
                if (!locked && !recent)
                {
                    if (stale == null) stale = new List<string>();
                    stale.Add(kv.Key);
                }
            }

            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) _entries.Remove(stale[i]);
        }
    }
}
