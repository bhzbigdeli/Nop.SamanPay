using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Saman.Components
{
    [ViewComponent(Name = "PaymentSaman")]
    public class PaymentSamanViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Saman/Views/PaymentInfo.cshtml");
        }
    }
}
