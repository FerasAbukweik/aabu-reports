using System.Net;

namespace aabu_reports.core.Common;

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public HttpStatusCode StatusCode { get; private set; }

    protected Result(bool isSuccess, string? errorMessage, HttpStatusCode statusCode)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public static Result Success(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new Result(true, null, statusCode);
    }

    public static Result Failure(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        return new Result(false, message, statusCode);
    }
    
    public Result<TNewT> MapFailure<TNewT>(HttpStatusCode? newStatusCode =  null)
    {
        return Result<TNewT>.Failure(ErrorMessage ?? "no error message", newStatusCode ?? StatusCode);
    }


    public override string ToString()
    {
        return $"StatusCode: {StatusCode}\nErrorMessage: {ErrorMessage}\nIsSuccessful: {IsSuccess}";
    } 
}