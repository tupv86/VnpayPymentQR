namespace VnpayPymentQR.Models
{// Models/Order.cs
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;           // vnp_TxnRef
        public decimal Amount { get; set; }                            // Số tiền gốc (không nhân 100)
        public string OrderInfo { get; set; } = string.Empty;
        public string CreateDate { get; set; }
        public string? ExpireDate { get; set; }
        public string? TransactionNo { get; set; }                     // vnp_TransactionNo
        public string? BankCode { get; set; }
        public string? PayDate { get; set; }
        public string Status { get; set; } = "Pending";                // Pending, Success, Failed, Refunded
        public string? ResponseCode { get; set; }                     // vnp_ResponseCode
        public string? PromoCode { get; set; }
    }
}
