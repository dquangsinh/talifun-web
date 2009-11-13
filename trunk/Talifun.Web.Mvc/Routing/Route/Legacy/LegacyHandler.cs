using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    /// <summary>
    /// The legacy HttpHandler that handles the request
    /// </summary>
    public class LegacyHandler : MvcHandler
    {
        public LegacyHandler(RequestContext requestContext)
            : base(requestContext)
        {
        }

        protected override void ProcessRequest(HttpContextBase httpContext)
        {
            var legacyRoute = ((LegacyRoute)RequestContext.RouteData.Route);
            string redirectActionName = legacyRoute.RedirectActionName;
            RouteValueDictionary redirectValuesDictionary = legacyRoute.RedirectValuesDictionary;

            // ... copy all of the querystring parameters and put them within RouteContext.RouteData.Values
            RouteValueDictionary valueDictionary = RequestContext.RouteData.Values;
            foreach (var key in redirectValuesDictionary.Keys)
            {
                if (valueDictionary.ContainsKey(key))
                {
                    valueDictionary[key] = redirectValuesDictionary[key];
                }
                else
                {
                    valueDictionary.Add(key, redirectValuesDictionary[key]);
                }
            }

            var data = RouteTable.Routes.GetVirtualPath(RequestContext, redirectActionName, valueDictionary);
            var virtualPath = data.VirtualPath;

            httpContext.Response.Status = "301 Moved Permanently";
            httpContext.Response.AppendHeader("Location", virtualPath);
        }
    }
}
