using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.GTPay
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Payment
            routeBuilder.MapRoute("Plugin.Payments.GTPay.PaymentHandler", "Plugins/PaymentGTPay/PaymentHandler",
                 new { controller = "PaymentGTPay", action = "PaymentHandler" });
            //Payment
            routeBuilder.MapRoute("Plugin.Payments.GTPay.ReturnPaymentInfo", "Plugins/PaymentGTPay/ReturnPaymentInfo",
                 new { controller = "PaymentGTPay", action = "ReturnPaymentInfo" });
            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.GTPay.CancelOrder", "Plugins/PaymentGTPay/CancelOrder",
                 new { controller = "PaymentGTPay", action = "CancelOrder" });
            //SubmitPaymentInfo
            routeBuilder.MapRoute("Plugin.Payments.GTPay.SubmitPaymentInfo", "Plugins/PaymentGTPay/SubmitPaymentInfo",
            new { controller = "PaymentGTPay", action = "SubmitPaymentInfo" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority
        {
            get { return -1; }
        }
    }
}
