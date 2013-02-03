using DotNetNuke.Web.Api;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Services
{
    public class RouteMapper : IServiceRouteMapper
    {
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute("ActiveForumsTapatalk", "default", "{controller}/{action}/{moduleId}", new[] { "DotNetNuke.Modules.ActiveForumsTapatalk.Services" });
        }
    }
}