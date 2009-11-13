using System;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    public class UrlExactMatchConstraint : IRouteConstraint
    {
        #region IRouteConstraint Members

        public bool Match(System.Web.HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            var virtualPath = httpContext.Request.AppRelativeCurrentExecutionFilePath.Substring(2) + httpContext.Request.PathInfo;
            return route.Url.Equals(virtualPath, StringComparison.InvariantCultureIgnoreCase);
        }

        #endregion
    }
}
