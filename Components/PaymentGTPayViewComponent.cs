using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.GTPay.Components
{
    [ViewComponent(Name = "PaymentGTPay")]
    public class PaymentGTPayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.GTPay/Views/PaymentInfo.cshtml");
        }
    }
}
