using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Tax;
using Nop.Services.Logging;
using Nop.Services.Customers;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Logging;

namespace Nop.Plugin.Payments.GTPay
{
    /// <summary>
    /// GTPay payment processor
    /// </summary>
    public class GTPayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly GTPayPaymentSettings _GTPayPaymentSettings;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger _logger;
        private readonly ICustomerService _customerService;


        #endregion

        #region Ctor

        public GTPayPaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            GTPayPaymentSettings GTPayPaymentSettings,
            IEncryptionService encryptionService,
            ILogger logger,
            ICustomerService customerService
           )
        {
            this._currencySettings = currencySettings;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._httpContextAccessor = httpContextAccessor;
            this._localizationService = localizationService;
            this._paymentService = paymentService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._GTPayPaymentSettings = GTPayPaymentSettings;
            this._encryptionService = encryptionService;
            this._logger = logger;
            this._customerService = customerService;

        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets GTPay URL
        /// </summary>
        /// <returns></returns>
        private string GetGTPayUrl()
        {
            return _GTPayPaymentSettings.UseSandbox ?
                "https://ibank.gtbank.com/GTPay/Tranx.aspx" :
                "https://ibank.gtbank.com/GTPay/Tranx.aspx";
        }

        private string GetGTPayResponseUrl()
        {
            return _GTPayPaymentSettings.UseSandbox ?
            "https://ibank.gtbank.com/GTPayService/gettransactionstatus.json" :
            "https://ibank.gtbank.com/GTPayService/gettransactionstatus.json";
        }



        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <param name="values">Values</param>
        /// <param name="response">Response</param>
        /// <returns>Result</returns>
        public bool GetPdtDetails(string tx, out Dictionary<string, string> values, out string response)
        {
            
            var req = (HttpWebRequest)WebRequest.Create(GetGTPayUrl());
            req.Method = WebRequestMethods.Http.Post;
            req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
            //now GTPay requires user-agent. otherwise, we can get 403 error
            req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];

            //var formContent = $"cmd=_notify-synch&at={_GTPayPaymentSettings.PdtToken}&tx={tx}";
            //req.ContentLength = formContent.Length;

            //using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
            //    sw.Write(formContent);

            using (var sr = new StreamReader(req.GetResponse().GetResponseStream()))
                response = WebUtility.UrlDecode(sr.ReadToEnd());

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool firstLine = true, success = false;
            foreach (var l in response.Split('\n'))
            {
                var line = l.Trim();
                if (firstLine)
                {
                    success = line.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
                    firstLine = false;
                }
                else
                {
                    var equalPox = line.IndexOf('=');
                    if (equalPox >= 0)
                        values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
                }
            }

            return success;
        }

       

       

        
        /// <summary>
        /// Add order items to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddItemsParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //upload order items
            parameters.Add("cmd", "_cart");
            parameters.Add("upload", "1");

            var cartTotal = decimal.Zero;
            var roundedCartTotal = decimal.Zero;
            var itemCount = 1;

            //add shopping cart items
            foreach (var item in postProcessPaymentRequest.Order.OrderItems)
            {
                var roundedItemPrice = Math.Round(item.UnitPriceExclTax, 2);

                //add query parameters
                parameters.Add($"item_name_{itemCount}", item.Product.Name);
                parameters.Add($"amount_{itemCount}", roundedItemPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", item.Quantity.ToString());

                cartTotal += item.PriceExclTax;
                roundedCartTotal += roundedItemPrice * item.Quantity;
                itemCount++;
            }

            //add checkout attributes as order items
            var checkoutAttributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(postProcessPaymentRequest.Order.CheckoutAttributesXml);
            foreach (var attributeValue in checkoutAttributeValues)
            {
                var attributePrice = _taxService.GetCheckoutAttributePrice(attributeValue, false, postProcessPaymentRequest.Order.Customer);
                var roundedAttributePrice = Math.Round(attributePrice, 2);

                //add query parameters
                if (attributeValue.CheckoutAttribute != null)
                {
                    parameters.Add($"item_name_{itemCount}", attributeValue.CheckoutAttribute.Name);
                    parameters.Add($"amount_{itemCount}", roundedAttributePrice.ToString("0.00", CultureInfo.InvariantCulture));
                    parameters.Add($"quantity_{itemCount}", "1");

                    cartTotal += attributePrice;
                    roundedCartTotal += roundedAttributePrice;
                    itemCount++;
                }
            }

            //add shipping fee as a separate order item, if it has price
            var roundedShippingPrice = Math.Round(postProcessPaymentRequest.Order.OrderShippingExclTax, 2);
            if (roundedShippingPrice > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Shipping fee");
                parameters.Add($"amount_{itemCount}", roundedShippingPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.OrderShippingExclTax;
                roundedCartTotal += roundedShippingPrice;
                itemCount++;
            }

            //add payment method additional fee as a separate order item, if it has price
            var roundedPaymentMethodPrice = Math.Round(postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax, 2);
            if (roundedPaymentMethodPrice > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Payment method fee");
                parameters.Add($"amount_{itemCount}", roundedPaymentMethodPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax;
                roundedCartTotal += roundedPaymentMethodPrice;
                itemCount++;
            }

            //add tax as a separate order item, if it has positive amount
            var roundedTaxAmount = Math.Round(postProcessPaymentRequest.Order.OrderTax, 2);
            if (roundedTaxAmount > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Tax amount");
                parameters.Add($"amount_{itemCount}", roundedTaxAmount.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.OrderTax;
                roundedCartTotal += roundedTaxAmount;
                itemCount++;
            }

            if (cartTotal > postProcessPaymentRequest.Order.OrderTotal)
            {
                //get the difference between what the order total is and what it should be and use that as the "discount"
                var discountTotal = Math.Round(cartTotal - postProcessPaymentRequest.Order.OrderTotal, 2);
                roundedCartTotal -= discountTotal;

                //gift card or rewarded point amount applied to cart in nopCommerce - shows in GTPay as "discount"
                parameters.Add("discount_amount_cart", discountTotal.ToString("0.00", CultureInfo.InvariantCulture));
            }

            //save order total that actually sent to GTPay (used for PDT order total validation)
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, GTPayHelper.OrderTotalSentToGTPay, roundedCartTotal);
        }

        /// <summary>
        /// Add order total to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddOrderTotalParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //round order total
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            parameters.Add("cmd", "_xclick");
            parameters.Add("item_name", $"Order Number {postProcessPaymentRequest.Order.CustomOrderNumber}");
            parameters.Add("amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //save order total that actually sent to GTPay (used for PDT order total validation)
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, GTPayHelper.OrderTotalSentToGTPay, roundedOrderTotal);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        //[HttpPost]
        //public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        //{
        //    //create common query parameters for the request
        //    var queryParameters = CreateQueryParameters(postProcessPaymentRequest);
        //    //remove null values from parameters
        //    queryParameters = queryParameters.Where(parameter => !string.IsNullOrEmpty(parameter.Value))
        //        .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);

        //    var url = QueryHelpers.AddQueryString(GetGTPayUrl(), queryParameters);
        //    if (_GTPayPaymentSettings.UseSandbox) { _logger.Information(url.Trim()); }
        //    _httpContextAccessor.HttpContext.Response.Redirect(url.Trim());
        //}
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            string customorderid = postProcessPaymentRequest.Order.CustomOrderNumber;
            //_logger.InsertLog(LogLevel.Information, customorderid);
            string url = $"{_webHelper.GetStoreLocation()}Plugins/PaymentGTPay/SubmitPaymentInfo?customorderid={customorderid}";
            _logger.InsertLog(LogLevel.Information,url);            
           _httpContextAccessor.HttpContext.Response.Redirect(url);
            
        }


        /// <summary>
        /// Create common query parameters for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created query parameters</returns>
        private IDictionary<string, string> CreateQueryParameters(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();
            string amount = (postProcessPaymentRequest.Order.OrderTotal * 100).ToString("F0");
            string currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
            string tranxid = $"order_{postProcessPaymentRequest.Order.CustomOrderNumber}";
            string paymentlink = $"{storeLocation}Plugins/PaymentGTPay/ReturnPaymentInfo";
            string custid = postProcessPaymentRequest.Order.CustomerId.ToString();
            string mertid = _GTPayPaymentSettings.GTPayMerchantID.ToString();
            string hashkey = _GTPayPaymentSettings.HashKey;

            string HashString = _encryptionService.CreatePasswordHash($"{mertid},{tranxid},{amount},{currency},{custid},{paymentlink}", hashkey, "SHA512");
                  
            return new Dictionary<string, string>
            {

                ["gtpay_mert_id"] =  mertid,
                //Customer order number as transation ID
                ["gtpay_tranx_id"] = tranxid,
                //Multiply by hundred to get value in kobo
                ["gtpay_tranx_amt"] = amount,
                //Use primary currency code
                ["gtpay_tranx_curr"] = currency,
                //Get customer ID
                ["gtpay_cust_id"] = custid,
                ["gtpay_tranx_memo"] = "",
                ["gtpay_tranx_noti_url"] = paymentlink,
                ["gtpay_gway_name"] = "",
                ["gtpay_gway_first"] = "",
                ["gtpay_echo_data"] = postProcessPaymentRequest.Order.OrderGuid.ToString(),
                ["gtpay_cust_name"] = postProcessPaymentRequest.Order.Customer.Username,
                ["gtpay_hash"] = HashString

            };

        }

       

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return Convert.ToDecimal(0.0);
            //return _paymentService.CalculateAdditionalFee(cart,
            //    _GTPayPaymentSettings.AdditionalFee, _GTPayPaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentGTPay/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentGTPay";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new GTPayPaymentSettings
            {
                UseSandbox = true
            });

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFee", "Additional fee");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.BusinessEmail", "Business Email");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.BusinessEmail.Hint", "Specify your GTPay business email.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.PassProductNamesAndTotals", "Pass product names and order totals to PayPal");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.PassProductNamesAndTotals.Hint", "Check if product names and order totals should be passed to PayPal.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.PDTToken", "PDT Identity Token");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.PDTToken.Hint", "Specify PDT identity token");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.RedirectionTip", "You will be redirected to GTPay site to complete the order.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.UseSandbox", "Use Sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.Instructions", @"
            <p>
	            <b>If you're using this gateway ensure that your primary store currency is supported by GTPay.</b>
	            <br />
            </p>");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.PaymentMethodDescription", "You will be redirected to GTPay site to complete the payment");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GTPay.RoundingWarning", "It looks like you have \"ShoppingCartSettings.RoundPricesDuringCalculation\" setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as GTPay only rounds to two decimals.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<GTPayPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFeePercentage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.BusinessEmail");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.BusinessEmail.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.PassProductNamesAndTotals");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.PassProductNamesAndTotals.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.PDTToken");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.PDTToken.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.Instructions");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.PaymentMethodDescription");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GTPay.RoundingWarning");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to GTPay site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.GTPay.PaymentMethodDescription"); }
        }

        #endregion
    }
}