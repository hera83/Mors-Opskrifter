using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace web.Controllers
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorController : Controller
    {
        [Route("Error/ServerError")]
        public IActionResult ServerError()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ViewData["RequestedPath"] = exceptionFeature?.Path;

            return View("ServerError");
        }

        [Route("Error/StatusCode/{statusCode:int}")]
        public IActionResult StatusCode(int statusCode)
        {
            var statusCodeFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            ViewData["RequestedPath"] = statusCodeFeature?.OriginalPath;

            return statusCode switch
            {
                StatusCodes.Status404NotFound => View("NotFound"),
                StatusCodes.Status403Forbidden => View("Forbidden"),
                _ => View("Generic")
            };
        }
    }
}
