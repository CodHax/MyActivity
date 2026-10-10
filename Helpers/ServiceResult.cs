namespace MyActivity.Helpers;

public class ServiceResult
{
    public bool Success { get; protected init; }
    public string Message { get; protected init; } = string.Empty;

    public static ServiceResult Ok(string message = "") => new() { Success = true, Message = message };
    public static ServiceResult Fail(string message) => new() { Success = false, Message = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message };

    public static new ServiceResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}
