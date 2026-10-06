namespace Shared;

public sealed record Result<T>(T? Value, string? Error) where T : class
{
    public bool IsSuccess => Value is not null;
    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(null, error);
}
