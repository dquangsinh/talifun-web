using System;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    public static class LegacyExtensions
    {
        /// <summary>
        /// Add a route that will permenently redirect (301) the current url to the specified mvc route.
        /// </summary>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to permanently redirect to specified mvc route (it must be an exact match)</param>
        /// <param name="redirectActionName">The specified mvc route</param>
        /// <returns></returns>
        public static Route MapLegacyRoute(this RouteCollection routes, string name, string url, string redirectActionName)
        {
            return routes.MapLegacyRoute(name, url, redirectActionName, new RouteValueDictionary());
        }

        /// <summary>
        /// Add a route that will permenently redirect (301) the current url to the specified mvc route.
        /// </summary>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to permanently redirect to specified mvc route (it must be an exact match)</param>
        /// <param name="redirectActionName">The specified mvc route</param>
        /// <param name="redirectValuesDictionary">The defaults to use for the specified mvc route</param>
        /// <returns></returns>
        public static Route MapLegacyRoute(this RouteCollection routes, string name, string url, string redirectActionName, object redirectValuesDictionary)
        {
            if (routes == null)
            {
                throw new ArgumentNullException("routes");
            }
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            var item = new LegacyRoute(url, redirectActionName, new RouteValueDictionary(redirectValuesDictionary), new LegacyRouteHandler());

            routes.Add(name, item);
            return item;
        }
    }
}
