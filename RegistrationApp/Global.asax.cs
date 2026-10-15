using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using RegistrationApp.Data;

namespace RegistrationApp
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // Creates the database + tables on first run (so F5 just works).
            DbSetup.EnsureCreated(Server.MapPath("~/Database/01_Schema.sql"));

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        // Errors outside controllers (routing, model binding, ...)
        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex is HttpException && ((HttpException)ex).GetHttpCode() == 404) return;
            AppLogger.Error(ex, Request.RawUrl);
        }
    }
}
