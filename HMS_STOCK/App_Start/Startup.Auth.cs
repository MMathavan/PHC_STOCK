using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMS_STOCK
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
                CookieName = ".AspNet.ApplicationCookie",
                CookieHttpOnly = true,
                CookieSecure = CookieSecureOption.Never,
                CookiePath = "/",
                ExpireTimeSpan = TimeSpan.FromDays(1),
                SlidingExpiration = true,
            });
        }
    }
}