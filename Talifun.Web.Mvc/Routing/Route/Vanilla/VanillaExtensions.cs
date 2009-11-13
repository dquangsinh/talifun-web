using System;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    public static class VanillaExtensions
    {
        /// <summary>
        /// Urls are rewritten 
        /// on input:
        ///     from /test-me-a-lot to /TestMeALot
        /// on output:
        ///     from /TestMeALot to /test-me-a-lot
        /// </summary>
        /// <remarks>
        /// The standard mvc route is used for calculation of url
        /// </remarks>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to match</param>
        /// <returns>The route to use for matching url</returns>
        public static Route MapVanillaRoute(this RouteCollection routes, string name, string url)
        {
            return routes.MapVanillaRoute(name, url, null, null);
        }

        /// <summary>
        /// Urls are rewritten 
        /// on input:
        ///     from /test-me-a-lot to /TestMeALot
        /// on output:
        ///     from /TestMeALot to /test-me-a-lot
        /// </summary>
        /// <remarks>
        /// The standard mvc route is used for calculation of url
        /// </remarks>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to match</param>
        /// <param name="defaults">The defaults to use for route actions</param>
        /// <returns>The route to use for matching url</returns>
        public static Route MapVanillaRoute(this RouteCollection routes, string name, string url, object defaults)
        {
            return routes.MapVanillaRoute(name, url, defaults, null);
        }

        /// <summary>
        /// Urls are rewritten 
        /// on input:
        ///     from /test-me-a-lot to /TestMeALot
        /// on output:
        ///     from /TestMeALot to /test-me-a-lot
        /// </summary>
        /// <remarks>
        /// The standard mvc route is used for calculation of url
        /// </remarks>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to match</param>
        /// <param name="namespaces">The namespace to use for route</param>
        /// <returns>The route to use for matching url</returns>
        public static Route MapVanillaRoute(this RouteCollection routes, string name, string url, string[] namespaces)
        {
            return routes.MapVanillaRoute(name, url, null, null, namespaces);
        }

        /// <summary>
        /// Urls are rewritten 
        /// on input:
        ///     from /test-me-a-lot to /TestMeALot
        /// on output:
        ///     from /TestMeALot to /test-me-a-lot
        /// </summary>
        /// <remarks>
        /// The standard mvc route is used for calculation of url
        /// </remarks>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to match</param>
        /// <param name="defaults">The defaults to use for route actions</param>
        /// <param name="constraints">Contraints to use for route actions</param>
        /// <returns>The route to use for matching url</returns>
        public static Route MapVanillaRoute(this RouteCollection routes, string name, string url, object defaults, object constraints)
        {
            return routes.MapVanillaRoute(name, url, defaults, constraints, null);
        }

        public static Route MapVanillaRoute(this RouteCollection routes, string name, string url, object defaults, string[] namespaces)
        {
            return routes.MapVanillaRoute(name, url, defaults, null, namespaces);
        }

        /// <summary>
        /// Urls are rewritten 
        /// on input:
        ///     from /test-me-a-lot to /TestMeALot
        /// on output:
        ///     from /TestMeALot to /test-me-a-lot
        /// </summary>
        /// <remarks>
        /// The standard mvc route is used for calculation of url
        /// </remarks>
        /// <param name="routes">The routes collection to apply extensions too.</param>
        /// <param name="name">The name of the route</param>
        /// <param name="url">The url to match</param>
        /// <param name="defaults">The defaults to use for route actions</param>
        /// <param name="constraints">Contraints to use for route actions</param>
        /// <param name="namespaces">The namespace to use for route</param>
        /// <returns>The route to use for matching url</returns>
        public static Route MapVanillaRoute(this RouteCollection routes, string name, string url, object defaults, object constraints, string[] namespaces)
        {
            if (routes == null)
            {
                throw new ArgumentNullException("routes");
            }

            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            var route2 = new VanillaRoute(url, new VanillaRouteHandler())
            {
                Constraints = new RouteValueDictionary(constraints),
                Defaults = new RouteValueDictionary(defaults)
            };

            var item = route2;
            if ((namespaces != null) && (namespaces.Length > 0))
            {
                item.DataTokens = new RouteValueDictionary();
                item.DataTokens["Namespaces"] = namespaces;
            }
            routes.Add(name, item);
            return item;
        }
    }
}
