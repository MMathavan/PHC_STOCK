using System.Web;
using System.Web.Mvc;

namespace HMS_STOCK
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Enforce redirect to Login when critical session keys are missing
            filters.Add(new SessionExpire());
        }
    }
}
