namespace Ucommerce.Transactions.Payments.Adyen;

/// <summary>
/// Contains immutable event codes that conform to an Adyen request EventCode.
/// </summary>
public static class EventCodes
{
    public const string Authorisation = "AUTHORISATION";
    public const string Cancellation = "CANCELLATION";
    public const string Capture = "CAPTURE";
    public const string CancelOrRefund = "CANCEL_OR_REFUND";
    public const string CaptureFailed = "CAPTURE_FAILED";
    public const string Refund = "REFUND";
    public const string ReportAvailable = "REPORT_AVAILABLE";

}