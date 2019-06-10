using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.GTPay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Title")]
        public string Title { get; set; }
        public bool Title_OverrideForStore { get; set; }

        [NopResourceDisplayName("Description ")]
        public string Description { get; set; }
        public bool Description_OverrideForStore { get; set; }

        [NopResourceDisplayName("WebPay Merchant ID ")]
        public string WebPayMerchantID { get; set; }
        public bool WebPayMerchantID_OverrideForStore { get; set; }

        [NopResourceDisplayName("GTPay Merchant ID")]
        public int GTPayMerchantID { get; set; }
        public bool GTPayMerchantID_OverrideForStore { get; set; }

        [NopResourceDisplayName("Hash Key ")]
        public string HashKey { get; set; }
        public bool HashKey_OverrideForStore { get; set; }
    }
}