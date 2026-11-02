using System.Net;

namespace AabuReports.Core.Results;

public class Result<T> : Result
{
    public T? Value { get; private set; }

    private Result(T? value, bool isSuccess, string? errorMessage, HttpStatusCode statusCode, string? logDetails = null)
        : base(isSuccess, errorMessage, statusCode, logDetails)
    {
        Value = value;
    }

    public static Result<T> Success(T value, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new Result<T>(value , true , null , statusCode);
    }
    public new static Result<T> Failure(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? logDetails = null)
    {
        return new Result<T>(default , false , message , statusCode, logDetails);
    }

    public override string ToString()
    {
        return base.ToString() + $"\nValue: {Value?.ToString()}";
    }
}
