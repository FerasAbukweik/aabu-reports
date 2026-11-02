using System.Net;

namespace AabuReports.Core.Results;

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public HttpStatusCode StatusCode { get; private set; }

    /// <summary> internal details for the logs only (ids, identity errors, exceptions), the controllers never send it to the client </summary>
    public string? LogDetails { get; private set; }

    protected Result(bool isSuccess, string? errorMessage, HttpStatusCode statusCode, string? logDetails = null)
    {
        LogDetails = logDetails;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public static Result Success(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new Result(true, null, statusCode);
    }

    public static Result Failure(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? logDetails = null)
    {
        return new Result(false, message, statusCode, logDetails);
    }
    
    public Result<TNewT> MapFailure<TNewT>(HttpStatusCode? newStatusCode =  null)
    {
        return Result<TNewT>.Failure(ErrorMessage ?? "no error message", newStatusCode ?? StatusCode, LogDetails);
    }


    public override string ToString()
    {
        return $"StatusCode: {StatusCode}\nErrorMessage: {ErrorMessage}\nIsSuccessful: {IsSuccess}";
    } 
}