using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Saman
{
    public class SamanPaymentSettings : ISettings
    {
        public string TerminalId { get; set; }

        /// <summary>
        /// Multiplier applied to the NopCommerce order total before sending to Saman Bank.
        /// Set to 1  if your store currency is already Iranian Rial (IRR).
        /// Set to 10 if your store currency is Iranian Toman (1 Toman = 10 Rials).
        /// Saman Bank requires the amount in Rials; minimum accepted is 1,000 Rials.
        /// </summary>
        public int AmountMultiplier { get; set; } = 1;
    }
}
