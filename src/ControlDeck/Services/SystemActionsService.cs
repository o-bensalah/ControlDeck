using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControlDeck.Services;

internal static class SystemActionsService
{
    public static void Lock() => LockWorkStation();

    public static void PrintScreen() => SendKeyTap(VkSnapshot);

    public static void ShowDesktop() => SendKeyCombo(VkLWin, VkD);

    public static void Sleep() => SetSuspendState(false, false, false);

    public static void OpenTaskManager() => Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });

    public static void OpenFileExplorer() => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });

    private static void SendKeyTap(ushort vk)
    {
        var inputs = new[] { KeyDown(vk), KeyUp(vk) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendKeyCombo(ushort vk1, ushort vk2)
    {
        var inputs = new[] { KeyDown(vk1), KeyDown(vk2), KeyUp(vk2), KeyUp(vk1) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyDown(ushort vk) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = vk } }
    };

    private static Input KeyUp(ushort vk) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = vk, Flags = KeyEventFKeyUp } }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("PowrProf.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int structSize);

    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const ushort VkSnapshot = 0x2C;
    private const ushort VkLWin = 0x5B;
    private const ushort VkD = 0x44;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
