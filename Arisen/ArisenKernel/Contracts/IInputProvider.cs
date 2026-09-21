namespace ArisenKernel.Contracts;

/// <summary>
/// Platform-neutral keyboard keys reported by <see cref="IInputProvider"/>.
/// </summary>
public enum InputKey
{
    Unknown = 0,
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    Digit0,
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt,
    Space,
    Tab,
    Enter,
    Escape,
    Left,
    Right,
    Up,
    Down,
    PageUp,
    PageDown
}

public enum InputMouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2
}

/// <summary>
/// Contract for per-frame keyboard/mouse state.
/// </summary>
/// <remarks>
/// Implementations snapshot the device once per frame. Consumers read the snapshot while building
/// their frame data and must not call back into the provider from worker threads.
/// </remarks>
[ServiceContract("Input Provider", "Provides per-frame keyboard, mouse and cursor-capture state.")]
public interface IInputProvider
{
    bool IsKeyDown(InputKey key);

    bool WasKeyPressed(InputKey key);

    bool WasKeyReleased(InputKey key);

    bool IsMouseButtonDown(InputMouseButton button);

    bool WasMouseButtonPressed(InputMouseButton button);

    bool WasMouseButtonReleased(InputMouseButton button);

    /// <summary>Horizontal pointer movement in pixels accumulated since the previous frame.</summary>
    float MouseDeltaX { get; }

    /// <summary>Vertical pointer movement in pixels accumulated since the previous frame.</summary>
    float MouseDeltaY { get; }

    /// <summary>
    /// True while the pointer is captured for relative input (hidden motion, no click-through to
    /// the desktop). Captured input is what camera navigation consumes.
    /// </summary>
    bool IsCursorCaptured { get; }

    /// <summary>
    /// Requests or releases pointer capture. Releasing restores the pointer position that was
    /// saved when capture began.
    /// </summary>
    void SetCursorCapture(bool captured);
}
