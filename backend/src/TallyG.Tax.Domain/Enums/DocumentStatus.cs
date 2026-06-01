namespace TallyG.Tax.Domain.Enums;

/// <summary>Processing state of an uploaded document.</summary>
public enum DocumentStatus
{
    Uploaded = 0,
    Scanning = 1,
    Extracting = 2,
    Extracted = 3,
    NeedsReview = 4,
    Verified = 5,
    Failed = 6
}
