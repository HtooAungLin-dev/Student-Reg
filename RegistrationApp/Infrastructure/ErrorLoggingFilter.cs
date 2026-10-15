using System.Web.Mvc;
using RegistrationApp.Data;

namespace RegistrationApp.Infrastructure
{
    /// <summary>Global filter: logs every unhandled controller exception, then shows a friendly page.</summary>
    public class ErrorLoggingFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled) return;

            AppLogger.Error(filterContext.Exception, filterContext.HttpContext.Request.RawUrl);

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new ViewResult { ViewName = "Error" };
        }
    }
}
