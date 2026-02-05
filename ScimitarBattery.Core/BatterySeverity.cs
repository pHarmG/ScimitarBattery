namespace ScimitarBattery.Core;

/// <summary>
/// Battery level severity for state machine and optional notifications.
/// </summary>
public enum BatterySeverity
{
    Normal,
    Low,
    Critical,
    Unknown
}
