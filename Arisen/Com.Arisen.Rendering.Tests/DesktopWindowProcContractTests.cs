using System;
using System.Runtime.InteropServices;
using ArisenEngine.Platform.Desktop;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// The native HAL falls through to DefWindowProc only when the managed window procedure reports a
/// message as unhandled, which it signals with -1. A procedure that reports every message as
/// handled removes the default non-client behaviour for the whole window, and a runtime window
/// built that way never turns a click on its close button into SC_CLOSE or WM_CLOSE.
/// </summary>
public sealed class DesktopWindowProcContractTests
{
    private const int WmWindowPosChanging = 0x0046;
    private const int WmNcHitTest = 0x0084;
    private const int WmActivate = 0x0006;
    private const int ScMaximize = 0xF030;

    private static readonly IntPtr Unhandled = new(-1);

    [Theory]
    [InlineData(WmNcHitTest)]
    [InlineData(WmActivate)]
    [InlineData(WmWindowPosChanging)]
    public void UnconsumedMessagesFallThroughToTheDefaultWindowProcedure(int message)
    {
        WindowsProcHandler handler = new();

        Assert.Equal(Unhandled, InvokeProc(handler, message, IntPtr.Zero));
    }

    [Fact]
    public void OtherSystemCommandsStayAvailableToTheDefaultWindowProcedure()
    {
        WindowsProcHandler handler = new();

        Assert.Equal(Unhandled, InvokeProc(handler, Win32Native.WM_SYSCOMMAND, new IntPtr(ScMaximize)));
    }

    [Fact]
    public void BothCloseRoutesAreConsumedSoEngineShutdownOwnsWindowDestruction()
    {
        int closeRequests = 0;
        WindowsProcHandler handler = new(onCloseRequested: () => closeRequests++);

        Assert.Equal(IntPtr.Zero, InvokeProc(handler, Win32Native.WM_CLOSE, IntPtr.Zero));
        Assert.Equal(IntPtr.Zero, InvokeProc(handler, Win32Native.WM_SYSCOMMAND, new IntPtr(Win32Native.SC_CLOSE)));

        Assert.Equal(2, closeRequests);
    }

    private static IntPtr InvokeProc(WindowsProcHandler handler, int message, IntPtr wParam)
    {
        Win32Native.WndProc proc =
            Marshal.GetDelegateForFunctionPointer<Win32Native.WndProc>(handler.ProcPtr);
        return proc(IntPtr.Zero, message, wParam, IntPtr.Zero);
    }
}