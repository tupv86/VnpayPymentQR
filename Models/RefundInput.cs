namespace VnpayPymentQR.Models
{
    public class RefundInput
    {
        public string OrderId { get; set; }
        public decimal RefundAmount { get; set; }  // Số tiền người dùng muốn hoàn (đơn vị VND, không nhân 100)
    }
}
