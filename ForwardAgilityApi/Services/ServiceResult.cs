namespace ForwardAgilityApi.Services;

public enum ServiceResultStatus { Ok, NotFound, Forbidden, Conflict, BadRequest }

public class ServiceResult
{
    public ServiceResultStatus Status { get; init; }
    public string? Error { get; init; }

    public static ServiceResult Ok() => new() { Status = ServiceResultStatus.Ok };
    public static ServiceResult NotFound(string error) => new() { Status = ServiceResultStatus.NotFound, Error = error };
    public static ServiceResult Forbidden(string error) => new() { Status = ServiceResultStatus.Forbidden, Error = error };
    public static ServiceResult Conflict(string error) => new() { Status = ServiceResultStatus.Conflict, Error = error };
    public static ServiceResult BadRequest(string error) => new() { Status = ServiceResultStatus.BadRequest, Error = error };
}

public class ServiceResult<T>
{
    public ServiceResultStatus Status { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { Status = ServiceResultStatus.Ok, Value = value };
    public static ServiceResult<T> NotFound(string error) => new() { Status = ServiceResultStatus.NotFound, Error = error };
    public static ServiceResult<T> Forbidden(string error) => new() { Status = ServiceResultStatus.Forbidden, Error = error };
    public static ServiceResult<T> Conflict(string error) => new() { Status = ServiceResultStatus.Conflict, Error = error };
    public static ServiceResult<T> BadRequest(string error) => new() { Status = ServiceResultStatus.BadRequest, Error = error };
}
