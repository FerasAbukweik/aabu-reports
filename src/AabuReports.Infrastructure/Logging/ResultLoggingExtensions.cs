using System.Net;
using System.Runtime.CompilerServices;
using AabuReports.Core.Results;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Logging;

internal static class ResultLoggingExtensions
{
    /// <summary>
    /// logs the result if it failed and returns it untouched: a 5xx is an Error, a 4xx is a Warning (only if <paramref name="logClientErrors"/>).
    /// the method name is the name of the decorator method calling this, which is the same as the service method
    /// </summary>
    public static async Task<TResult> LogFailure<TResult>(
        this Task<TResult> task,
        ILogger logger,
        string serviceName,
        bool logClientErrors = false,
        [CallerMemberName] string methodName = "") where TResult : Result
    {
        var result = await task;
        if (result.IsSuccess) return result;

        var error = result.LogDetails is null ? result.ErrorMessage : $"{result.ErrorMessage} ({result.LogDetails})";

        if (result.StatusCode >= HttpStatusCode.InternalServerError)
            logger.LogError("{ServiceName}.{MethodName} failed with {StatusCode}: {Error}",
                serviceName, methodName, (int)result.StatusCode, error);
        else if (logClientErrors)
            logger.LogWarning("{ServiceName}.{MethodName} failed with {StatusCode}: {Error}",
                serviceName, methodName, (int)result.StatusCode, error);

        return result;
    }
}
