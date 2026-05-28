using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Saman.Infrastructure
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute(
                "Plugin.Payments.Saman.PaymentForm",
                "Plugins/PaymentSaman/PaymentForm",
                new { controller = "PaymentSaman", action = "PaymentForm" });

            endpointRouteBuilder.MapControllerRoute(
                "Plugin.Payments.Saman.Return",
                "Plugins/PaymentSaman/Return",
                new { controller = "PaymentSaman", action = "Return" });
        }

        public int Priority => -1;
    }
}
