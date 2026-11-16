# RemotePC - Remote Desktop Application in C#

A fast, lightweight, and self-contained **Remote Desktop & Remote Control** application written in C# (.NET Framework 4.8 / Windows Forms). It lets you seamlessly view, control, transfer files, and communicate between **2 PCs** across a local network (Wi-Fi/LAN) or over the Internet.

---

## ✨ Features

- **⚡ Real-Time Screen Streaming & Remote Control:**
  - High-FPS, low-latency streaming with adaptive JPEG compression.
  - Remote mouse cursor rendering (displays remote cursor position and icon).
  - Full mouse control: Move, Left Click, Right Click, Middle Click, Drag & Drop, and Mouse Wheel scroll.
  - Full keyboard forwarding (including function keys, arrows, shortcuts).
  - Quick action toolbar: `Ctrl+Alt+Del`, `Alt+Tab`, `Windows Key`, `Show Desktop (Win+D)`, `Task Manager`, and `Lock PC`.
  - View modes: **Fit to Window** (aspect-ratio preserved), **Stretch**, **100% Native (1:1)**, and **Fullscreen (F11)**.
  - Adjustable streaming quality (Speed 35%, Balanced 60%, High 80%, Ultra 95%) and FPS limits.

- **📁 Bi-Directional File Manager:**
  - Dual-pane file explorer: Local PC on the left, Remote PC on the right.
  - Upload files from local PC to remote PC.
  - Download files from remote PC to local PC.
  - Create remote folders, delete remote files, and view transfer progress bars.

- **📋 Clipboard Synchronization:**
  - One-click text clipboard sync between the two PCs.

- **💬 Built-in Live Chat:**
  - Instant text messaging between Host and Client with timestamps.

- **ℹ️ Remote System Specifications:**
  - Inspect remote machine name, user, OS version, 64-bit architecture, CPU cores, screen resolution, and system uptime.

- **🔒 Security & Ease of Use:**
  - Branded app icon and logo embedded inside `RemotePC.exe` — the same artwork shows on the second PC with no extra files to copy.
  - Single executable (`RemotePC.exe` ~86 KB) contains **both Host (Server) and Client (Viewer)** modes.
  - Auto-generated or custom access PIN / password.
  - Option to toggle view-only vs full control.
  - **Automatic network adaptation:** when the laptop joins a different Wi-Fi, hotspot or LAN, the app re-applies Windows Firewall, restarts discovery, rebuilds the router port mapping and refreshes the public address — no manual reconfiguration.
  - **One-paste connection code:** `Copy Code` puts address + port + PIN on the clipboard; the client fills all three fields from it.
  - **IPv6 support:** the server is dual-stack, so on networks that provide a public IPv6 address the other PC can connect directly without any port forwarding.
  - Displays all local IPv4/IPv6 network adapters with 1-click **"Copy IP"** / **"Copy Code"** buttons.
  - Zero third-party dependencies — runs natively on Windows 7, 8, 10, and 11 out of the box!

---

## 🚀 How to Use Between 2 PCs

### Step 1: Copy `RemotePC.exe` to Both PCs
Copy `bin\Release\RemotePC.exe` to both PC 1 and PC 2 (using a USB flash drive, local network share, or cloud drive).

**That is the whole setup.** On first launch the app configures itself:
- Adds **Remote PC** to the Start Menu.
- Opens the required **Windows Firewall** ports (one UAC prompt — click *Yes*).
- Starts the server automatically so the PC is immediately reachable.

Nothing else has to be installed or configured by hand.

---

### Step 2: On PC 1 (The PC to Host / Access)
1. Double-click `RemotePC.exe`.
2. Approve the single Windows prompt asking to allow firewall changes.
3. Look at **"This PC's IPv4 Address"** and click **"Copy Code"** — this copies the address, port and PIN together.
4. Note the **Security PIN** shown on screen:
   - The server already started for you on the first run.
   - On later runs press the green **"Start Server"** button, or tick **⚡ Auto-Start Server**.
   - Generate a fresh code any time with **"Random PIN"**.
5. The status line reads: `Server is RUNNING on port 8888. Ready for connections.`

---

### Step 3: On PC 2 (The PC You Are Sitting At)
1. Double-click `RemotePC.exe`.
2. Switch to the **"Connect to Remote PC (Client)"** tab.
3. Paste the code from PC 1 into **"Remote PC IP Address"** (the port and PIN fill in automatically), or type the IP and PIN separately.
4. Click **"Connect to Remote PC"**.

🎉 The **Remote Desktop Viewer** window will open immediately with live screen viewing, responsive mouse & keyboard control, file management, and chat!

---

## 🌐 Using the Laptop on Different Networks (Zero Manual Config)

The app is designed to be carried between networks. On every server start **and every time
the laptop changes network**, it automatically:

1. Re-applies the Windows Firewall rules for the active profile (Private/Public/Domain).
2. Restarts LAN discovery on the new subnet.
3. Asks the router to forward the port using **NAT-PMP**, then **UPnP IGD**.
4. Discovers the new public IPv4 address.
5. Refreshes the address list, including any global **IPv6** address.
6. Renews the router mapping every 10 minutes and after router reboots.

### The one step on the other PC

On the host, click **"Copy Code"**. It copies one line like:

```
RPC1|203.0.113.25|8888|1234567890
```

On the client, paste it into **"Remote PC IP Address"** and click **Connect**. The port and
PIN fields are filled in automatically. Addresses marked **★** are the ones that work across
networks (automatic router mapping, IPv6, or Tailscale).

### What is really automatic

| Network | Works with no configuration? |
| --- | --- |
| Same Wi-Fi / LAN | **Yes** — discovery, firewall and addresses are automatic |
| Home/office router with UPnP or NAT-PMP | **Yes** — port mapping and public address are automatic |
| Network with a public IPv6 address (many ISPs/mobile networks) | **Usually yes** — pick the ★ IPv6 address |
| Tailscale installed on both PCs | **Yes** — pick the ★ Tailscale address |
| Hotel / café / airport guest Wi-Fi, or ISP carrier-grade NAT | **No** — inbound connections are blocked before they reach the laptop |
| Public Wi-Fi that requires a web login | **No** — the laptop is not reachable until you sign in, and usually not even then |

### If the network blocks inbound connections

No application can accept an inbound connection through carrier-grade NAT or a guest Wi-Fi
without a relay server. The host's log says plainly when this is the case, for example:

```
[Internet] Public address 100.64.x.x is carrier-grade NAT; inbound connections cannot reach this PC.
```

The free options are:

1. **Tailscale (recommended):** install it on both PCs, sign in with the same account, then
   use the address marked **★ … (Internet/Tailscale)** in the app. It works from any network.
2. Keep the **host** on a home/office network and use the laptop as the **client**.
3. *(Advanced)* Forward TCP port 8888 manually on a router you control. The log still shows
   the public address, and the `⚠ Internet (port forward needed)` entry lets you copy it.

### Security on public networks

The PIN is ten digits and the server locks a source address out after five wrong attempts
for 15 minutes, so exposing the port does not make the PIN guessable. Still, use a fresh PIN
and prefer Tailscale on networks you do not control.

---

## 🛡️ Windows Firewall

The app opens the firewall itself on first run — you should only ever see a single **"Do you want to allow this app to make changes?"** prompt. Click **Yes** and you are done.

If you clicked *No* (or are behind a restrictive policy), click **🛡 Auto-Configure Firewall** in the *Zero-Work Automation* box on the Host tab to retry. The status line under the buttons reports whether the ports are open.

What it creates (all inbound, all profiles):

| Rule | Purpose |
| --- | --- |
| `RemotePC Server TCP <port>` | The remote-control port (8888 by default) |
| `RemotePC LAN Discovery UDP 8889` | Lets the other PC find this one automatically |
| `RemotePC Application` | Covers any other port the app itself listens on |

To remove them again:
```cmd
for %R in ("RemotePC Server TCP 8888" "RemotePC LAN Discovery UDP 8889" "RemotePC Application") do netsh advfirewall firewall delete rule name=%R
```

---

## 🛠️ Project Structure & Building from Source

```
remote-pc/
├── assets/
│   ├── app.ico                // Multi-size app icon (16→256) - embedded in the .exe
│   └── app-logo.png           // Square logo shown in the tab headers
├── bin/
│   └── Release/
│       └── RemotePC.exe       <-- Pre-compiled ready-to-run executable (~86 KB)
├── src/
│   ├── Common/
│   │   ├── NativeMethods.cs   // Win32 SendInput, mouse/cursor capture, LockWorkstation
│   │   ├── Protocol.cs        // Binary packet framing & serialization
│   │   ├── InputSimulator.cs  // Mouse & keyboard simulation
│   │   ├── ScreenCapturer.cs  // GDI+ screen capture, cursor drawing, JPEG compression
│   │   ├── SystemInfo.cs      // IPv4/IPv6 detection & hardware specs
│   │   ├── AppIcon.cs         // Embedded app icon & logo loader
│   │   ├── ConnectionCode.cs  // One-paste address + port + PIN parser/formatter
│   │   ├── FirewallHelper.cs  // Zero-touch Windows Firewall rule automation
│   │   ├── NetworkChangeWatcher.cs // Debounced Wi-Fi/network change notifications
│   │   ├── NetworkDiscovery.cs     // LAN UDP auto-discovery
│   │   ├── NatPmpPortMapper.cs     // NAT-PMP router mapping with lease renewal
│   │   ├── UPnPPortForwarder.cs    // UPnP IGD mapping with renewal & cleanup
│   │   └── PublicIpResolver.cs     // Public IP detection + CGNAT/private classification
│   ├── Server/
│   │   ├── RemoteServer.cs    // Dual-stack (IPv4 + IPv6) TCP listener
│   │   └── ServerSession.cs   // Client session management & screen stream loop
│   ├── Client/
│   │   └── RemoteClient.cs    // Network client, frame decoder, event forwarder
│   ├── UI/
│   │   ├── MainForm.cs        // Host & Client launcher dashboard
│   │   ├── ViewerForm.cs      // Remote Desktop viewer with top toolbar
│   │   ├── FileManagerForm.cs // Dual-pane file manager for file transfers
│   │   ├── ChatForm.cs        // Real-time chat messaging dialog
│   │   └── SystemInfoForm.cs  // Remote system specifications dialog
│   └── Program.cs             // Application entry point & exception handlers
├── tests/
│   └── TestRunner.cs          // Automated verification test suite
├── build.bat                  // 1-Click Batch compiler
├── build.ps1                  // 1-Click PowerShell compiler
├── RemotePC.csproj            // MSBuild / Visual Studio project file
└── README.md                  // User guide
```

### Compiling:
- Simply double-click `build.bat`
- Or run in PowerShell:
  ```powershell
  .\build.ps1
  ```
- Or open `RemotePC.csproj` in Visual Studio and build in **Release** mode.
