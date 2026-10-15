using System.Web.Mvc;
using RegistrationApp.Infrastructure;

namespace RegistrationApp
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new ErrorLoggingFilter());
        }
    }
}
