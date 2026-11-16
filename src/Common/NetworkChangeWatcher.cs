using System;
using System.Net.NetworkInformation;
using System.Threading;

namespace RemotePC.Common
{
    /// <summary>
    /// Debounced notification for "this laptop just changed networks".
    ///
    /// Windows fires NetworkAddressChanged several times for a single Wi-Fi switch (DHCP
    /// lease, gateway, DNS, IPv6 router advertisement), so the raw events are collapsed into
    /// one callback. That callback is what lets the app re-open the firewall, restart LAN
    /// discovery and rebuild the router port mapping without the user touching anything.
    /// </summary>
    public class NetworkChangeWatcher : IDisposable
    {
        private readonly Action _onChanged;
        private readonly int _debounceMs;
        private int _pending;
        private int _started;
        private DateTime _lastRaisedUtc = DateTime.MinValue;

        public NetworkChangeWatcher(Action onChanged) : this(onChanged, 2500)
        {
        }

        public NetworkChangeWatcher(Action onChanged, int debounceMs)
        {
            if (onChanged == null) throw new ArgumentNullException("onChanged");
            _onChanged = onChanged;
            _debounceMs = debounceMs > 250 ? debounceMs : 250;
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1) return;

            try { NetworkChange.NetworkAddressChanged += HandleNetworkAddressChanged; }
            catch { }
            try { NetworkChange.NetworkAvailabilityChanged += HandleNetworkAvailabilityChanged; }
            catch { }
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _started, 0) == 0) return;

            try { NetworkChange.NetworkAddressChanged -= HandleNetworkAddressChanged; }
            catch { }
            try { NetworkChange.NetworkAvailabilityChanged -= HandleNetworkAvailabilityChanged; }
            catch { }
        }

        private void HandleNetworkAddressChanged(object sender, EventArgs e)
        {
            Schedule();
        }

        private void HandleNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Schedule();
        }

        private void Schedule()
        {
            if (Volatile.Read(ref _started) == 0) return;

            // A burst of events is already being collapsed by another worker.
            if (Interlocked.Exchange(ref _pending, 1) == 1) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // Keep resetting the debounce window while events keep arriving.
                    while (true)
                    {
                        Thread.Sleep(_debounceMs);
                        if (Interlocked.Exchange(ref _pending, 0) == 0) break;
                        Interlocked.Exchange(ref _pending, 1);
                    }

                    if (Volatile.Read(ref _started) == 0) return;

                    // Do not hammer the router if Windows reports a second change right after.
                    TimeSpan sinceLast = DateTime.UtcNow - _lastRaisedUtc;
                    int remaining = 6000 - (int)sinceLast.TotalMilliseconds;
                    if (remaining > 0) Thread.Sleep(remaining);

                    if (Volatile.Read(ref _started) == 0) return;
                    _lastRaisedUtc = DateTime.UtcNow;

                    _onChanged();
                }
                catch
                {
                    // Network notifications must never take the app down.
                }
            });
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
