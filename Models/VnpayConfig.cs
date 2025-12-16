namespace VnpayPymentQR.Models
{
    public class VnpayConfig
    {
        public string TmnCode { get; set; } = string.Empty;
        public string HashSecret { get; set; } = string.Empty;
        public string PayUrl { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string IpnUrl { get; set; } = string.Empty; // Tùy chọn
    }
}
