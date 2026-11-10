using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace GameHub.App.Services;

public sealed class ControllerService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Pad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LX, LY, RX, RY; }
    [StructLayout(LayoutKind.Sequential)] private struct State { public uint Packet; public Pad Pad; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern uint ReadModern(uint index, out State state);
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern uint ReadLegacy(uint index, out State state);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private ushort previous;
    private long lastMove;
    private bool connected;
    public event Action<string>? Input;
    public event Action<bool>? ConnectionChanged;

    public ControllerService() { timer.Tick += Poll; timer.Start(); }
    private static bool Read(uint index, out State state)
    {
        try { return ReadModern(index, out state) == 0; }
        catch (DllNotFoundException) { try { return ReadLegacy(index, out state) == 0; } catch (DllNotFoundException) { state = default; return false; } }
    }
    private void Poll(object? sender, EventArgs args)
    {
        State state = default;
        bool found = false;
        for (uint i = 0; i < 4; i++) if (Read(i, out state)) { found = true; break; }
        if (found != connected) { connected = found; ConnectionChanged?.Invoke(found); }
        if (!found) { previous = 0; return; }
        ushort buttons = state.Pad.Buttons;
        if (state.Pad.LX < -16000) buttons |= 4;
        if (state.Pad.LX > 16000) buttons |= 8;
        if (state.Pad.LY > 16000) buttons |= 1;
        if (state.Pad.LY < -16000) buttons |= 2;
        ushort pressed = (ushort)(buttons & ~previous);
        foreach (var pair in new (ushort Mask, string Action)[] { (0x1000,"Play"),(0x2000,"Back"),(0x4000,"Favorite"),(0x8000,"Search"),(0x0100,"PreviousTab"),(0x0200,"NextTab"),(0x0010,"Menu"),(0x0020,"Details") })
            if ((pressed & pair.Mask) != 0) Input?.Invoke(pair.Action);
        long now = Environment.TickCount64;
        if ((buttons & 15) != 0 && ((pressed & 15) != 0 || now - lastMove >= 220))
        {
            Input?.Invoke((buttons & 1) != 0 ? "Up" : (buttons & 2) != 0 ? "Down" : (buttons & 4) != 0 ? "Left" : "Right");
            lastMove = now;
        }
        previous = buttons;
    }
    public void Dispose() { timer.Stop(); timer.Tick -= Poll; }
}
