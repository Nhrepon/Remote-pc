using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemotePC.Common
{
    public static class InputSimulator
    {
        public static void SendMouseMove(int x, int y, int screenW, int screenH)
        {
            NativeMethods.SetCursorPos(x, y);
        }

        public static void SendMouseButton(int button, bool isDown, int x, int y, int screenW, int screenH)
        {
            NativeMethods.SetCursorPos(x, y);

            uint flag = 0;
            if (button == 1) // Left
            {
                flag = isDown ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP;
            }
            else if (button == 2) // Right
            {
                flag = isDown ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP;
            }
            else if (button == 3) // Middle
            {
                flag = isDown ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP;
            }

            if (flag == 0) return;

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
            inputs[0].type = NativeMethods.INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = flag;

            NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        public static void SendMouseWheel(int delta, int x, int y)
        {
            NativeMethods.SetCursorPos(x, y);

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
            inputs[0].type = NativeMethods.INPUT_MOUSE;
            inputs[0].u.mi.mouseData = (uint)delta;
            inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_WHEEL;

            NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        public static void SendKeyboard(int virtualKey, bool isKeyDown, bool isExtended)
        {
            if (virtualKey <= 0 || virtualKey > 255) return;

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)virtualKey;
            inputs[0].u.ki.wScan = (ushort)NativeMethods.MapVirtualKey((uint)virtualKey, 0);

            uint flags = isKeyDown ? NativeMethods.KEYEVENTF_KEYDOWN : NativeMethods.KEYEVENTF_KEYUP;
            if (isExtended)
            {
                flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }
            inputs[0].u.ki.dwFlags = flags;

            NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        public static void ExecuteSpecialCommand(int commandId)
        {
            try
            {
                switch (commandId)
                {
                    case 1: // Ctrl+Alt+Del -> Launch Taskmgr or Security screen
                        Process.Start("taskmgr.exe");
                        break;

                    case 2: // Alt+Tab
                        SendKeyboard((int)Keys.Menu, true, false);
                        SendKeyboard((int)Keys.Tab, true, false);
                        SendKeyboard((int)Keys.Tab, false, false);
                        SendKeyboard((int)Keys.Menu, false, false);
                        break;

                    case 3: // Windows Key
                        SendKeyboard((int)Keys.LWin, true, true);
                        SendKeyboard((int)Keys.LWin, false, true);
                        break;

                    case 4: // Win + D (Show Desktop)
                        SendKeyboard((int)Keys.LWin, true, true);
                        SendKeyboard((int)Keys.D, true, false);
                        SendKeyboard((int)Keys.D, false, false);
                        SendKeyboard((int)Keys.LWin, false, true);
                        break;

                    case 5: // Task Manager
                        Process.Start("taskmgr.exe");
                        break;

                    case 6: // Lock PC
                        NativeMethods.LockWorkStation();
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Special command error: " + ex.Message);
            }
        }
    }
}
