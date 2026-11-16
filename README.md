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
  - Single executable (`RemotePC.exe` ~86 KB) contains **both Host (Server) and Client (Viewer)** modes.
  - Auto-generated or custom access PIN / password.
  - Option to toggle view-only vs full control.
  - Displays all local IPv4 network adapters with a 1-click **"Copy IP"** button.
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
3. Look at **"This PC's IPv4 Address"** (e.g. `192.168.1.105`) and click **"Copy IP"**.
4. Note the **Security PIN** shown on screen:
   - The server already started for you on the first run.
   - On later runs press the green **"Start Server"** button, or tick **⚡ Auto-Start Server**.
   - Generate a fresh code any time with **"Random PIN"**.
5. The status line reads: `Server is RUNNING on port 8888. Ready for connections.`

---

### Step 3: On PC 2 (The PC You Are Sitting At)
1. Double-click `RemotePC.exe`.
2. Switch to the **"Connect to Remote PC (Client)"** tab.
3. In **"Remote PC IP Address"**, paste the IP address from PC 1.
4. Enter the **Security PIN** from PC 1.
5. Click **"Connect to Remote PC"**.

🎉 The **Remote Desktop Viewer** window will open immediately with live screen viewing, responsive mouse & keyboard control, file management, and chat!

---

## 🌐 Connecting Across Different Networks (Over the Internet)

If your 2 PCs are in different locations (e.g. Home PC and Office PC, or different Wi-Fi networks):

### Method 1: Tailscale (Recommended — 100% Free, Secure, Zero Router Setup)
1. Install [Tailscale](https://tailscale.com) on both PCs and sign in with the same account.
2. Tailscale assigns each PC a private secure IP (e.g. `100.x.y.z`).
3. In `RemotePC.exe`, select or enter the Tailscale IP on PC 2 to connect securely from anywhere in the world!

### Method 2: Router Port Forwarding
1. On PC 1's home/office router, open router admin page (e.g. `192.168.1.1`).
2. Add a Port Forwarding rule:
   - External Port: `8888`
   - Internal Port: `8888`
   - Internal IP: PC 1's local IP (e.g. `192.168.1.105`)
   - Protocol: `TCP`
3. On PC 2, connect using PC 1's **Public IP address** (find it by visiting [whatismyipaddress.com](https://whatismyipaddress.com) on PC 1).

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
├── bin/
│   └── Release/
│       └── RemotePC.exe       <-- Pre-compiled ready-to-run executable (~86 KB)
├── src/
│   ├── Common/
│   │   ├── NativeMethods.cs   // Win32 SendInput, mouse/cursor capture, LockWorkstation
│   │   ├── Protocol.cs        // Binary packet framing & serialization
│   │   ├── InputSimulator.cs  // Mouse & keyboard simulation
│   │   ├── ScreenCapturer.cs  // GDI+ screen capture, cursor drawing, JPEG compression
│   │   └── SystemInfo.cs      // Network IP detection & hardware specs
│   ├── Server/
│   │   ├── RemoteServer.cs    // Multi-threaded TCP listener
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
