using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Saman.Models;
using Nop.Plugin.Payments.Saman.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Saman.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PaymentSamanController : BasePaymentController
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly SamanHttpClient _samanHttpClient;
        private readonly SamanPaymentSettings _samanPaymentSettings;

        #endregion

        #region Ctor

        public PaymentSamanController(
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            SamanHttpClient samanHttpClient,
            SamanPaymentSettings samanPaymentSettings)
        {
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _samanHttpClient = samanHttpClient;
            _samanPaymentSettings = samanPaymentSettings;
        }

        #endregion

        #region Admin

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var settings   = _settingService.LoadSetting<SamanPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                TerminalId                   = settings.TerminalId,
                AmountMultiplier             = settings.AmountMultiplier > 0 ? settings.AmountMultiplier : 1,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.TerminalId_OverrideForStore       = _settingService.SettingExists(settings, x => x.TerminalId,       storeScope);
                model.AmountMultiplier_OverrideForStore = _settingService.SettingExists(settings, x => x.AmountMultiplier, storeScope);
            }

            return View("~/Plugins/Payments.Saman/Views/Configure.cshtml", model);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var settings   = _settingService.LoadSetting<SamanPaymentSettings>(storeScope);

            settings.TerminalId       = model.TerminalId;
            settings.AmountMultiplier = model.AmountMultiplier > 0 ? model.AmountMultiplier : 1;

            _settingService.SaveSettingOverridablePerStore(settings, x => x.TerminalId,       model.TerminalId_OverrideForStore,       storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.AmountMultiplier, model.AmountMultiplier_OverrideForStore, storeScope, false);

            _settingService.ClearCache();

            _notificationService.SuccessNotification(
                _localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        #endregion

        #region Public

        [HttpGet]
        [IgnoreAntiforgeryToken]
        public IActionResult PaymentForm(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted)
                return RedirectToRoute("HomePage");

            if (_workContext.CurrentCustomer.Id != order.CustomerId)
                return RedirectToRoute("HomePage");

            var token = _genericAttributeService.GetAttribute<string>(
                order, SamanPaymentDefaults.PaymentTokenAttribute);

            if (string.IsNullOrEmpty(token))
            {
                _logger.Warning($"Saman Bank: no token found for order #{orderId}");
                return RedirectToRoute("HomePage");
            }

            ViewBag.PaymentUrl = SamanPaymentDefaults.PaymentPageUrl;
            ViewBag.Token = token;

            return View("~/Plugins/Payments.Saman/Views/PaymentForm.cshtml");
        }

        [AcceptVerbs("GET", "POST")]
        [IgnoreAntiforgeryToken]
        public IActionResult Return()
        {
            try
            {
                return ProcessReturn();
            }
            catch (Exception ex)
            {
                _logger.Error("Saman Bank: unhandled exception in Return action", ex);
                return RedirectToRoute("HomePage");
            }
        }

        private IActionResult ProcessReturn()
        {
            // ── Step 1: dump everything received from Saman Bank ──────────────────
            var sb = new StringBuilder();
            sb.Append($"[Saman Return] Method={Request.Method} | ");

            var hasForm = Request.HasFormContentType;
            var formData = hasForm
                ? string.Join(", ", Request.Form.Select(f => $"{f.Key}={f.Value}"))
                : "[no form content]";

            sb.Append($"Form=[{formData}] | Query=[");
            sb.Append(string.Join(", ", Request.Query.Select(q => $"{q.Key}={q.Value}")));
            sb.Append("]");

            _logger.Information(sb.ToString());

            // ── Step 2: read parameters directly – never rely on model binding ────
            string Get(string key) =>
                (hasForm ? Request.Form[key].FirstOrDefault() : null)
                ?? Request.Query[key].FirstOrDefault()
                ?? string.Empty;

            var statusRaw = Get("Status");
            var refNum    = Get("RefNum");
            var resNum    = Get("ResNum");
            var traceNo   = Get("TraceNo");
            var securePan = Get("SecurePan");
            var token     = Get("Token");

            _logger.Information(
                $"[Saman Return] Parsed – Status={statusRaw}, ResNum={resNum}, " +
                $"RefNum={refNum}, TraceNo={traceNo}, SecurePan={securePan}, Token={token}");

            // ── Step 3: resolve the order ─────────────────────────────────────────
            if (!int.TryParse(resNum, out var orderId))
            {
                _logger.Error($"[Saman Return] Cannot parse ResNum as order ID. Raw value: '{resNum}'");
                return RedirectToRoute("HomePage");
            }

            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted)
            {
                _logger.Error($"[Saman Return] Order #{orderId} not found or deleted.");
                return RedirectToRoute("HomePage");
            }

            _logger.Information(
                $"[Saman Return] Order #{orderId} found. " +
                $"PaymentStatus={order.PaymentStatus}, OrderStatus={order.OrderStatus}");

            // ── Step 4: check Saman status field ─────────────────────────────────
            if (!int.TryParse(statusRaw, out var status))
            {
                _logger.Error(
                    $"[Saman Return] Cannot parse Status field. Raw value: '{statusRaw}'. " +
                    $"Treating as failure for order #{orderId}.");
                status = -1;
            }

            // Negative/zero status = definitive failure (user cancelled, timeout, gateway error).
            // Status=1 (success) and Status=2 (bank-side reversal / already authorised) both
            // carry a RefNum; proceed to verify and let the verify API be the final word.
            if (status < 1)
            {
                _logger.Warning(
                    $"[Saman Return] Payment failed/cancelled for order #{orderId}. " +
                    $"Status={status} (raw='{statusRaw}')");

                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note    = $"Saman Bank payment failed/cancelled. Status={status}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            }

            _logger.Information(
                $"[Saman Return] Status={status} for order #{orderId} – proceeding to verify.");

            // ── Step 5: idempotency guard ─────────────────────────────────────────
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                _logger.Information(
                    $"[Saman Return] Order #{orderId} already marked as Paid. Skipping.");
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }

            // ── Step 6: verify with Saman Bank ────────────────────────────────────
            if (string.IsNullOrEmpty(refNum))
            {
                _logger.Error(
                    $"[Saman Return] RefNum is empty for order #{orderId}. " +
                    $"Cannot call verify API. All received params: {sb}");

                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note    = "Saman Bank: RefNum missing – verification skipped.",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            }

            _logger.Information(
                $"[Saman Return] Calling verify API. RefNum={refNum}, " +
                $"TerminalId={_samanPaymentSettings.TerminalId}");

            var verifyResult = _samanHttpClient
                .VerifyTransactionAsync(refNum, _samanPaymentSettings.TerminalId)
                .GetAwaiter().GetResult();

            _logger.Information(
                $"[Saman Return] Verify response: ResultCode={verifyResult.ResultCode}, " +
                $"Amount={verifyResult.Amount}, Desc={verifyResult.Description}, " +
                $"Raw={verifyResult.RawResponse}");

            if (verifyResult.ResultCode != 0)
            {
                _logger.Error(
                    $"[Saman Return] Verification FAILED for order #{orderId}. " +
                    $"ResultCode={verifyResult.ResultCode}, Raw={verifyResult.RawResponse}");

                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note    = $"Saman Bank verification failed. ResultCode={verifyResult.ResultCode}. " +
                              $"Raw={verifyResult.RawResponse}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            }

            // ── Step 7: mark order as paid ────────────────────────────────────────
            var canPay = _orderProcessingService.CanMarkOrderAsPaid(order);
            _logger.Information(
                $"[Saman Return] CanMarkOrderAsPaid={canPay} for order #{orderId}. " +
                $"OrderStatus={order.OrderStatus}, PaymentStatus={order.PaymentStatus}");

            if (canPay)
            {
                order.AuthorizationTransactionId     = refNum;
                order.AuthorizationTransactionCode   = traceNo;
                order.AuthorizationTransactionResult =
                    $"RefNum={refNum}; TraceNo={traceNo}; SecurePan={securePan}";

                _orderService.UpdateOrder(order);
                _orderProcessingService.MarkOrderAsPaid(order);

                _logger.Information(
                    $"[Saman Return] Order #{orderId} successfully marked as Paid. RefNum={refNum}");
            }
            else
            {
                _logger.Warning(
                    $"[Saman Return] Verification OK but CanMarkOrderAsPaid=false for order #{orderId}. " +
                    $"OrderStatus={order.OrderStatus}, PaymentStatus={order.PaymentStatus}. " +
                    $"Order will NOT be marked as paid.");
            }

            _orderService.InsertOrderNote(new OrderNote
            {
                OrderId = order.Id,
                Note    = $"Saman Bank payment verified. RefNum={refNum}, TraceNo={traceNo}, " +
                          $"CanMarkAsPaid={canPay}",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        #endregion
    }
}
