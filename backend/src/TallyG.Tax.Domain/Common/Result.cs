namespace TallyG.Tax.Domain.Common;

/// <summary>
/// Lightweight functional result for flows that prefer returning failures over throwing
/// (e.g. payment signature verification, OTP checks). The API layer can translate a
/// failed <see cref="Result"/> into an <see cref="AppException"/> where appropriate.
/// </summary>
public class Result
{
    public bool Succeeded { get; }
    public bool Failed => !Succeeded;
    public string? Code { get; }
    public string? Error { get; }

    protected Result(bool succeeded, string? code, string? error)
    {
        Succeeded = succeeded;
        Code = code;
        Error = error;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string code, string error) => new(false, code, error);

    public static Result<T> Success<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Failure<T>(string code, string error) => Result<T>.Fail(code, error);
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool succeeded, T? value, string? code, string? error)
        : base(succeeded, code, error)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(true, value, null, null);
    public static Result<T> Fail(string code, string error) => new(false, default, code, error);
}
