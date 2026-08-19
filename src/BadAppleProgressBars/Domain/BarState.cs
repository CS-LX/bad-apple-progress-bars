namespace BadAppleProgressBars.Domain;

/// <summary>
/// The complete state of one pooled progress-bar slot in a baked frame.
/// </summary>
public readonly record struct BarState(
    int SlotId,
    bool Visible,
    int Row,
    int StartX,
    int Length,
    int Maximum,
    int Value);
