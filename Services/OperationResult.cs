namespace CMRP.Services;

/// <summary>Simple success/failure result so services never throw for expected business-rule failures.</summary>
public class OperationResult
{
    private OperationResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }
    public string? Error { get; }

    public static OperationResult Ok() => new(true, null);
    public static OperationResult Fail(string error) => new(false, error);
}

public class CreateReportResult
{
    private CreateReportResult(bool succeeded, string? error, int reportId, string? reference)
    {
        Succeeded = succeeded;
        Error = error;
        ReportId = reportId;
        ReferenceNumber = reference;
    }

    public bool Succeeded { get; }
    public string? Error { get; }
    public int ReportId { get; }
    public string? ReferenceNumber { get; }

    public static CreateReportResult Ok(int id, string reference) => new(true, null, id, reference);
    public static CreateReportResult Fail(string error) => new(false, error, 0, null);
}
