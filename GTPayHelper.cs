using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.GTPay
{
    /// <summary>
    /// Represents GTPay helper
    /// </summary>
    public class GTPayHelper
    {
        #region Properties

        /// <summary>
        /// Get the generic attribute name that is used to store an order total that actually sent to GTPay (used to PDT order total validation)
        /// </summary>
        public static string OrderTotalSentToGTPay => "orderTotalSentToGTPay";

        #endregion

        #region Methods
        public static string GetGTPayUrl()
        {

            return "https://ibank.gtbank.com/GTPay/Tranx.aspx";
                
        }
        /// <summary>
        /// Gets a payment status
        /// </summary>
        /// <param name="paymentStatus">GTPay payment status</param>
        /// <param name="pendingReason">GTPay pending reason</param>
        /// <returns>Payment status</returns>
        public static PaymentStatus GetPaymentStatus(string paymentStatus, string pendingReason)
        {
            var result = PaymentStatus.Pending;

            if (paymentStatus == null)
                paymentStatus = string.Empty;

            if (pendingReason == null)
                pendingReason = string.Empty;

            switch (paymentStatus.ToLowerInvariant())
            {
                case "pending":
                    switch (pendingReason.ToLowerInvariant())
                    {
                        case "authorization":
                            result = PaymentStatus.Authorized;
                            break;
                        default:
                            result = PaymentStatus.Pending;
                            break;
                    }
                    break;
                case "processed":
                case "completed":
                case "canceled_reversal":
                    result = PaymentStatus.Paid;
                    break;
                case "denied":
                case "expired":
                case "failed":
                case "voided":
                    result = PaymentStatus.Voided;
                    break;
                case "refunded":
                case "reversed":
                    result = PaymentStatus.Refunded;
                    break;
                default:
                    break;
            }
            return result;
        }

        #endregion
    }
}