using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Saman.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Saman.Fields.TerminalId")]
        public string TerminalId { get; set; }
        public bool TerminalId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Saman.Fields.AmountMultiplier")]
        public int AmountMultiplier { get; set; }
        public bool AmountMultiplier_OverrideForStore { get; set; }
    }
}
