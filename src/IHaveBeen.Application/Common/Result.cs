namespace IHaveBeen.Application.Common;

public enum ErrorKind { Validation, NotFound, Forbidden, Conflict, TooLarge, Unavailable }
public sealed record ApplicationError(ErrorKind Kind, string Message);
public readonly record struct Unit;

public sealed class Result<T>
{
    private Result(T? value, ApplicationError? error) { Value = value; Error = error; }
    public T? Value { get; }
    public ApplicationError? Error { get; }
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Invalid(string message) => new(default, new(ErrorKind.Validation, message));
    public static Result<T> NotFound() => new(default, new(ErrorKind.NotFound, "This item or invitation is no longer available."));
    public static Result<T> Forbidden() => new(default, new(ErrorKind.Forbidden, "Access is not allowed."));
    public static Result<T> Conflict(string message) => new(default, new(ErrorKind.Conflict, message));
    public static Result<T> TooLarge(string message) => new(default, new(ErrorKind.TooLarge, message));
    public static Result<T> Unavailable(string message) => new(default, new(ErrorKind.Unavailable, message));
}
