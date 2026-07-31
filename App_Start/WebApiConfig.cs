using System.Web.Http;

namespace ERPaperless
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            DependencyConfig.ConfigureWebApi(config);
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
