using System.Net;
using AabuReports.Core.Results;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.ExtensionMethods;

public static class ResultToActionResult
{
    public static ActionResult ToActionResult(this Result result)
    {
        // no body on success, and a StatusCodeResult keeps a 200 from being turned into 204 for a null body
        if (result.IsSuccess)
            return new StatusCodeResult((int)result.StatusCode);

        return new ObjectResult(result.ErrorMessage) { StatusCode = (int)result.StatusCode };
    }

    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        return result.StatusCode switch
        {
            HttpStatusCode.OK => new OkObjectResult(result.Value),
            HttpStatusCode.Created => new ObjectResult(result.Value) { StatusCode = (int)HttpStatusCode.Created },
            _ => ToActionResult((Result)result)
        };
    }
}
