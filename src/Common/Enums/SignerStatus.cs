namespace TrustSign.Common.Enums;

/// <summary>Trạng thái của một người ký theo thứ tự tuần tự (F04, F05).</summary>
public enum SignerStatus
{
    Waiting = 0,
    Current = 1,
    Signed = 2,
    Declined = 3
}
