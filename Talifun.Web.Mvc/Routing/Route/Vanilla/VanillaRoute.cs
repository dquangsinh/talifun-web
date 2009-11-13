using System.Reflection;
using System.Web;
using System.Web.Routing;


namespace Talifun.Web.Mvc
{
    public class VanillaRoute : Route
    {
        private const BindingFlags NeedlesslyPrivate = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo parsedRouteFieldInfo;
        private static readonly MethodInfo matchMethodInfo;
        private static readonly MethodInfo bindMethodInfo;
        private static readonly PropertyInfo urlPropertyInfo;
        private static readonly PropertyInfo valuesPropertyInfo;

        private static readonly VanillaTransform transform = new VanillaTransform();

        static VanillaRoute()
        {
            parsedRouteFieldInfo = typeof(Route).GetField("_parsedRoute", NeedlesslyPrivate);
            matchMethodInfo = parsedRouteFieldInfo.FieldType.GetMethod("Match", NeedlesslyPrivate, null, CallingConventions.Any, new[] { typeof(string), typeof(RouteValueDictionary) }, null);
            bindMethodInfo = parsedRouteFieldInfo.FieldType.GetMethod("Bind", NeedlesslyPrivate, null, CallingConventions.Any, new[] { typeof(RouteValueDictionary), typeof(RouteValueDictionary), typeof(RouteValueDictionary), typeof(RouteValueDictionary) }, null);
            var boundUrlType = bindMethodInfo.ReturnType;
            urlPropertyInfo = boundUrlType.GetProperty("Url");
            valuesPropertyInfo = boundUrlType.GetProperty("Values");
        }

        public VanillaRoute(string url, IRouteHandler routeHandler)
            : base(url, routeHandler)
        {
        }

        public VanillaRoute(string url, RouteValueDictionary defaults, IRouteHandler routeHandler)
            : base(url, defaults, routeHandler)
        {
        }

        public VanillaRoute(string url, RouteValueDictionary defaults, RouteValueDictionary constraints, IRouteHandler routeHandler)
            : base(url, defaults, constraints, routeHandler)
        {
        }

        public VanillaRoute(string url, RouteValueDictionary defaults, RouteValueDictionary constraints, RouteValueDictionary dataTokens, IRouteHandler routeHandler)
            : base(url, defaults, constraints, dataTokens, routeHandler)
        {
        }

        private RouteValueDictionary Match(string virtualPath, RouteValueDictionary defaultValues)
        {
            var parsedRoute = parsedRouteFieldInfo.GetValue(this);
            var match = (RouteValueDictionary)matchMethodInfo.Invoke(parsedRoute, new object[] { virtualPath, defaultValues });
            return match;
        }

        public override RouteData GetRouteData(HttpContextBase httpContext)
        {
            var virtualPath = httpContext.Request.AppRelativeCurrentExecutionFilePath.Substring(1) + httpContext.Request.PathInfo;
            virtualPath = transform.ApplyTransform(virtualPath);
            virtualPath = virtualPath.Substring(1);

            var values = Match(virtualPath, this.Defaults);
            if (values == null)
            {
                return null;
            }

            var data = new RouteData(this, this.RouteHandler);
            if (!this.ProcessConstraints(httpContext, values, RouteDirection.IncomingRequest))
            {
                return null;
            }

            foreach (var pair in values)
            {
                data.Values.Add(pair.Key, pair.Value);
            }

            if (this.DataTokens != null)
            {
                foreach (var pair2 in this.DataTokens)
                {
                    data.DataTokens[pair2.Key] = pair2.Value;
                }
            }

            return data;
        }

        private BoundUrl Bind(RequestContext requestContext, RouteValueDictionary values)
        {
            var parsedRoute = parsedRouteFieldInfo.GetValue(this);
            object bindResult = bindMethodInfo.Invoke(parsedRoute,
                                                  new object[]
                                                      {
                                                          requestContext.RouteData.Values, values, this.Defaults,
                                                          this.Constraints
                                                      });

            if (bindResult == null) return null;

            var bindUrl = (string)urlPropertyInfo.GetValue(bindResult, null);
            var bindValues = (RouteValueDictionary)valuesPropertyInfo.GetValue(bindResult, null);

            var result = new BoundUrl
            {
                Url = bindUrl,
                Values = bindValues
            };

            return result;
        }

        public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
        {
            var url = Bind(requestContext, values);
            if (url == null)
            {
                return null;
            }

            if (!this.ProcessConstraints(requestContext.HttpContext, url.Values, RouteDirection.UrlGeneration))
            {
                return null;
            }

            string rewrittenUrl = url.Url;
            if (rewrittenUrl != "")
            {
                rewrittenUrl = "/" + rewrittenUrl;
                rewrittenUrl = transform.ApplyReverseTransform(rewrittenUrl);
                rewrittenUrl = rewrittenUrl.Substring(1);
            }

            var data = new VirtualPathData(this, rewrittenUrl);

            if (this.DataTokens != null)
            {
                foreach (var pair in this.DataTokens)
                {
                    data.DataTokens[pair.Key] = pair.Value;
                }
            }
            return data;
        }

        private bool ProcessConstraints(HttpContextBase httpContext, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (this.Constraints != null)
            {
                foreach (var pair in this.Constraints)
                {
                    if (!this.ProcessConstraint(httpContext, pair.Value, pair.Key, values, routeDirection))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
