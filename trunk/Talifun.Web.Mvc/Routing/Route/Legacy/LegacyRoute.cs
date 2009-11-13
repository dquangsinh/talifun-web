using System.Web;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    public class LegacyRoute : Route
    {
        public LegacyRoute(string url, string redirectActionName, IRouteHandler routeHandler)
            : base(url, routeHandler)
        {
            RedirectActionName = redirectActionName;
            RedirectValuesDictionary = new RouteValueDictionary();
        }

        public LegacyRoute(string url, string redirectActionName, RouteValueDictionary valuesDictionary, IRouteHandler routeHandler)
            : base(url, routeHandler)
        {
            RedirectActionName = redirectActionName;
            RedirectValuesDictionary = valuesDictionary;
        }

        public string RedirectActionName { get; set; }
        public RouteValueDictionary RedirectValuesDictionary { get; set; }

        public override RouteData GetRouteData(HttpContextBase httpContext)
        {
            var key = "ExactMatch" + this.Url;
            object value = new UrlExactMatchConstraint();

            if (Constraints == null)
            {
                Constraints = new RouteValueDictionary();
            }

            if (!Constraints.ContainsKey(key))
            {
                Constraints.Add(key, value);
            }

            return base.GetRouteData(httpContext);
        }
    }
}
