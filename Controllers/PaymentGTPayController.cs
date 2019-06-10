using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.GTPay.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.GTPay.Controllers
{
    public class PaymentGTPayController : BasePaymentController
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly GTPayPaymentSettings _GTPayPaymentSettings;
        private readonly IEncryptionService _encryptionService;
        private readonly CurrencySettings _currencySettings;
        private readonly ICurrencyService _currencyService;

        #endregion

        #region Ctor

        public PaymentGTPayController(
            IGenericAttributeService genericAttributeService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            ShoppingCartSettings shoppingCartSettings,
            GTPayPaymentSettings GTPayPaymentSettings,
            CurrencySettings currencySettings,
            ICurrencyService currencyService,
        IEncryptionService encryptionService)
        {
            this._genericAttributeService = genericAttributeService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._paymentService = paymentService;
            this._permissionService = permissionService;
            this._localizationService = localizationService;
            this._logger = logger;
            this._notificationService = notificationService;
            this._settingService = settingService;
            this._shoppingCartSettings = shoppingCartSettings;
            this._storeContext = storeContext;
            this._webHelper = webHelper;
            this._workContext = workContext;
            this._GTPayPaymentSettings = GTPayPaymentSettings;
            this._currencySettings = currencySettings;
            this._currencyService = currencyService;
            this._encryptionService = encryptionService;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var GTPayPaymentSettings = _settingService.LoadSetting<GTPayPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = GTPayPaymentSettings.UseSandbox,
                Title = GTPayPaymentSettings.Title,
                Description = GTPayPaymentSettings.Description,
                WebPayMerchantID = GTPayPaymentSettings.WebPayMerchantID,
                GTPayMerchantID = GTPayPaymentSettings.GTPayMerchantID,
                HashKey = GTPayPaymentSettings.HashKey,
                ActiveStoreScopeConfiguration = storeScope
            };
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(GTPayPaymentSettings, x => x.UseSandbox, storeScope);
                model.Title_OverrideForStore = _settingService.SettingExists(GTPayPaymentSettings, x => x.Title, storeScope);
                model.Description_OverrideForStore = _settingService.SettingExists(GTPayPaymentSettings, x => x.Description, storeScope);
                model.WebPayMerchantID_OverrideForStore = _settingService.SettingExists(GTPayPaymentSettings, x => x.WebPayMerchantID, storeScope);
                model.GTPayMerchantID_OverrideForStore = _settingService.SettingExists(GTPayPaymentSettings, x => x.GTPayMerchantID, storeScope);
                model.HashKey_OverrideForStore = _settingService.SettingExists(GTPayPaymentSettings, x => x.HashKey, storeScope);
            }

            return View("~/Plugins/Payments.GTPay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var GTPayPaymentSettings = _settingService.LoadSetting<GTPayPaymentSettings>(storeScope);

            //save settings
            GTPayPaymentSettings.UseSandbox = model.UseSandbox;
            GTPayPaymentSettings.Title = model.Title;
            GTPayPaymentSettings.Description = model.Description;
            GTPayPaymentSettings.WebPayMerchantID = model.WebPayMerchantID;
            GTPayPaymentSettings.GTPayMerchantID = model.GTPayMerchantID;
            GTPayPaymentSettings.HashKey = model.HashKey;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(GTPayPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(GTPayPaymentSettings, x => x.Title, model.Title_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(GTPayPaymentSettings, x => x.Description, model.Description_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(GTPayPaymentSettings, x => x.WebPayMerchantID, model.WebPayMerchantID_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(GTPayPaymentSettings, x => x.GTPayMerchantID, model.GTPayMerchantID_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(GTPayPaymentSettings, x => x.HashKey, model.HashKey_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        //action displaying notification (warning) to a store owner about inaccurate GTPay rounding
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = _localizationService.GetResource("Plugins.Payments.GTPay.RoundingWarning") });

            return Json(new { Result = string.Empty });
        }

        public ActionResult ReturnPaymentInfo(IFormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.GTPay") as GTPayPaymentProcessor;
            if (processor == null || !_paymentService.IsPaymentMethodActive(processor) || !processor.PluginDescriptor.Installed)
            {
                throw new NopException("GTPay module cannot be loaded");
            }
            string tranx_id = form["gtpay_tranx_id"];
            string tranx_status_code = form["gtpay_tranx_status_code"];
            string tranx_status_msg = form["gtpay_tranx_status_msg"];
            string gtpay_tranx_amt = form["gtpay_tranx_amt"];
            string gtpay_tranx_curr = form["gtpay_tranx_curr"];
            string gtpay_cust_id = form["gtpay_cust_id"];
            string gtpay_gway_name = form["gtpay_gway_name"];
            string gtpay_echo_data = form["gtpay_echo_data"];


            _logger.Information("transid: " + tranx_id);
            _logger.Information("tranx_status_code: " + tranx_status_code);
            _logger.Information("tranx_status_msg: " + tranx_status_msg);
            _logger.Information("gtpay_echo_data: " + gtpay_echo_data);
            _logger.Information("gtpay_tranx_amt: " + gtpay_tranx_amt);
            _logger.Information("gtpay_tranx_curr: " + gtpay_tranx_curr);

            var orderGuid = Guid.Parse(gtpay_echo_data);
            Order order = _orderService.GetOrderByGuid(orderGuid);

            if (!string.Equals(tranx_status_code, "00", StringComparison.InvariantCultureIgnoreCase))
            {
                var model = new ReturnPaymentInfoModel();
                model.DescriptionText = "Your transaction was unsuccessful.";
                model.OrderId = order.Id;
                model.StatusCode = tranx_status_code;
                model.StatusMessage = tranx_status_msg;

                return View("~/Plugins/Payments.GTPay/Views/ReturnPaymentInfo.cshtml", model);
            }

            order.PaymentStatus = PaymentStatus.Paid;
            _orderService.UpdateOrder(order);
            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            //return RedirectToAction("Completed", "Checkout");
        }
        [AuthorizeAdmin]
        [AdminAntiForgery]
        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("HomePage");
        }
        [HttpGet]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        public  IActionResult SubmitPaymentInfo(string customorderid)
        {
            Order order = _orderService.GetOrderByCustomOrderNumber(customorderid);
            var sb = new StringBuilder();
            var storeLocation = _webHelper.GetStoreLocation();
            string tranxamt = (order.OrderTotal * 100).ToString("F0");
            string tranxcurr = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
            string tranxid = $"order_{order.Id.ToString()}";
            string paymentlink = $"{storeLocation}Plugins/PaymentGTPay/ReturnPaymentInfo";
            string custid = order.CustomerId.ToString();
            string mertid = _GTPayPaymentSettings.GTPayMerchantID.ToString();
            string hashkey = _GTPayPaymentSettings.HashKey;           
            //compute gtpay_hash here
            string hashstring = _encryptionService.CreatePasswordHash($"{mertid}{tranxid}{tranxamt}{tranxcurr}{custid}{paymentlink}", hashkey, "SHA512");
            string tranxurl = GTPayHelper.GetGTPayUrl();
            sb.AppendLine("<html>");
            sb.AppendLine("<body onload=\"document.submit2gtpay_form.submit()\">");
            sb.AppendLine($"<form name=\"submit2gtpay_form\" action=\"{tranxurl}\" target=\"_self\" method=\"post\">");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_mert_id\" value=\"{mertid}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_tranx_id\" value=\"{tranxid}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_tranx_amt\" value=\"{tranxamt}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_tranx_curr\" value=\"{tranxcurr}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_cust_id\" value=\"{custid}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_cust_name\" value=\"{order.Customer.Username}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_tranx_memo\" value=\"orderId:{order.Id.ToString()},orderGuid:{order.OrderGuid.ToString()},customerId:{order.Customer.Id.ToString()}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_tranx_noti_url\" value=\"{paymentlink}\" />");           
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_echo_data\" value=\"{order.OrderGuid.ToString()}\" />");
            sb.AppendLine($"<input type=\"hidden\" name=\"gtpay_hash\" value=\"{hashstring}\" />");
            sb.AppendLine("</form>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            string content = sb.ToString();
            _logger.Information(content);
            return Content(content, "text/html");

        }
        #endregion
    }
}