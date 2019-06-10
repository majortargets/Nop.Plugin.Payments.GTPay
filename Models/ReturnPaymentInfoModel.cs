using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.GTPay.Models
{
    public class ReturnPaymentInfoModel
    {
        public string DescriptionText { get; set; }
        public string StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public int OrderId { get; set; }
    }
}
