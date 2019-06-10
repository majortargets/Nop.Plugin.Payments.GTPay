using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.GTPay
{
    /// <summary>
    /// Represents settings of the GTPay payment plugin
    /// </summary>
    public class GTPayPaymentSettings : ISettings
    {       
        public bool UseSandbox { get; set; }
        public string Title { get; set; }
       public string Description { get; set; }

      public string WebPayMerchantID { get; set; }
       public int GTPayMerchantID { get; set; }
        public string HashKey { get; set; }
        
    }
}
