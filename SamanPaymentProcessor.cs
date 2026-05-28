using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Saman.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.Saman
{
    public class SamanPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly SamanHttpClient _samanHttpClient;
        private readonly SamanPaymentSettings _samanPaymentSettings;

        #endregion

        #region Ctor

        public SamanPaymentProcessor(
            CurrencySettings currencySettings,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            ILogger logger,
            IPaymentService paymentService,
            ISettingService settingService,
            IWebHelper webHelper,
            SamanHttpClient samanHttpClient,
            SamanPaymentSettings samanPaymentSettings)
        {
            _currencySettings = currencySettings;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _logger = logger;
            _paymentService = paymentService;
            _settingService = settingService;
            _webHelper = webHelper;
            _samanHttpClient = samanHttpClient;
            _samanPaymentSettings = samanPaymentSettings;
        }

        #endregion

        #region Methods

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        private long ConvertToRials(decimal orderTotal)
        {
            // Step 1: NopCommerce currency → IRR via exchange rate (if IRR is configured)
            var primaryCurrency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);

            if (primaryCurrency != null &&
                !primaryCurrency.CurrencyCode.Equals("IRR", StringComparison.OrdinalIgnoreCase))
            {
                var irrCurrency = _currencyService
                    .GetAllCurrencies(showHidden: true)
                    .FirstOrDefault(c => c.CurrencyCode.Equals("IRR", StringComparison.OrdinalIgnoreCase));

                if (irrCurrency != null)
                {
                    var amountInIrr = _currencyService.ConvertFromPrimaryStoreCurrency(orderTotal, irrCurrency);
                    var rials = (long)Math.Round(amountInIrr);
                    _logger.Information(
                        $"[Saman] Currency converted: {orderTotal} {primaryCurrency.CurrencyCode} " +
                        $"→ {rials} IRR via exchange rate.");
                    return rials;
                }

                _logger.Warning(
                    $"[Saman] IRR currency not found in the system. " +
                    $"Falling back to AmountMultiplier ({_samanPaymentSettings.AmountMultiplier}). " +
                    $"Add Iranian Rial (IRR) as a currency for accurate conversion.");
            }

            // Step 2: Fallback — multiply by the admin-configured multiplier.
            //   • Store in Toman → set AmountMultiplier = 10  (1 Toman = 10 Rials)
            //   • Store in Rials → set AmountMultiplier = 1
            var multiplier = _samanPaymentSettings.AmountMultiplier > 0
                ? _samanPaymentSettings.AmountMultiplier
                : 1;

            var result = (long)Math.Round(orderTotal) * multiplier;
            _logger.Information(
                $"[Saman] Amount = {orderTotal} × multiplier {multiplier} = {result} Rials.");
            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var order         = postProcessPaymentRequest.Order;
            var storeLocation = _webHelper.GetStoreLocation().TrimEnd('/') + "/";
            var returnUrl     = $"{storeLocation}Plugins/PaymentSaman/Return";
            var resNum        = order.Id.ToString();

            var amountInRials = ConvertToRials(order.OrderTotal);

            _logger.Information(
                $"[Saman] Token request → TerminalId={_samanPaymentSettings.TerminalId}, " +
                $"Amount={amountInRials}, ResNum={resNum}, ReturnUrl={returnUrl}");

            var tokenResponse = _samanHttpClient
                .RequestTokenAsync(
                    _samanPaymentSettings.TerminalId,
                    amountInRials,
                    resNum,
                    returnUrl)
                .GetAwaiter().GetResult();

            _logger.Information(
                $"[Saman] Token response → Status={tokenResponse.Status}, " +
                $"Token={tokenResponse.Token}, Raw={tokenResponse.RawResponse}");

            if (tokenResponse.Status != 1 || string.IsNullOrEmpty(tokenResponse.Token))
            {
                _logger.Error(
                    $"[Saman] Token request FAILED for order #{order.Id}. " +
                    $"Status={tokenResponse.Status}, Error={tokenResponse.ErrorDesc}");

                _httpContextAccessor.HttpContext.Response
                    .Redirect($"{storeLocation}orderdetails/{order.Id}");
                return;
            }

            _genericAttributeService.SaveAttribute(
                order,
                SamanPaymentDefaults.PaymentTokenAttribute,
                tokenResponse.Token);

            _logger.Information(
                $"[Saman] Token saved. Redirecting to PaymentForm for order #{order.Id}.");

            _httpContextAccessor.HttpContext.Response
                .Redirect($"{storeLocation}Plugins/PaymentSaman/PaymentForm?orderId={order.Id}");
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart) => false;

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart) => decimal.Zero;

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
            => new CapturePaymentResult { Errors = new[] { "Capture not supported" } };

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
            => new RefundPaymentResult { Errors = new[] { "Refund not supported" } };

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
            => new VoidPaymentResult { Errors = new[] { "Void not supported" } };

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
            => new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
            => new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return (DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds >= 5;
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
            => new List<string>();

        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
            => new ProcessPaymentRequest();

        public override string GetConfigurationPageUrl()
            => $"{_webHelper.GetStoreLocation()}Admin/PaymentSaman/Configure";

        public string GetPublicViewComponentName() => "PaymentSaman";

        public override void Install()
        {
            _settingService.SaveSetting(new SamanPaymentSettings
            {
                TerminalId       = string.Empty,
                AmountMultiplier = 1
            });

            _localizationService.AddPluginLocaleResource(new Dictionary<string, string>
            {
                ["Plugins.Payments.Saman.Fields.TerminalId"]              = "Terminal ID",
                ["Plugins.Payments.Saman.Fields.TerminalId.Hint"]         = "Enter your Saman Bank terminal ID.",
                ["Plugins.Payments.Saman.Fields.AmountMultiplier"]        = "Amount Multiplier",
                ["Plugins.Payments.Saman.Fields.AmountMultiplier.Hint"]   =
                    "Multiplier applied to the order total before sending to Saman Bank. " +
                    "Set to 1 if your store currency is Iranian Rial (IRR). " +
                    "Set to 10 if your store currency is Toman (1 Toman = 10 Rials). " +
                    "Saman Bank minimum is 1,000 Rials.",
                ["Plugins.Payments.Saman.PaymentMethodDescription"]       = "Pay via Saman Bank internet payment gateway",
                ["Plugins.Payments.Saman.RedirectionTip"]                 = "You will be redirected to Saman Bank (SEP) to complete your payment.",
                ["Plugins.Payments.Saman.Payment.Successful"]             = "Payment was successful.",
                ["Plugins.Payments.Saman.Payment.Failed"]                 = "Payment failed or was cancelled.",
                ["Plugins.Payments.Saman.Payment.VerificationFailed"]     = "Payment verification failed. Please contact support.",
            });

            base.Install();
        }

        public override void Uninstall()
        {
            _settingService.DeleteSetting<SamanPaymentSettings>();
            _localizationService.DeletePluginLocaleResources("Plugins.Payments.Saman");
            base.Uninstall();
        }

        #endregion

        #region Properties

        public bool SupportCapture => false;
        public bool SupportPartiallyRefund => false;
        public bool SupportRefund => false;
        public bool SupportVoid => false;
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;
        public bool SkipPaymentInfo => false;

        public string PaymentMethodDescription =>
            _localizationService.GetResource("Plugins.Payments.Saman.PaymentMethodDescription");

        #endregion
    }
}
