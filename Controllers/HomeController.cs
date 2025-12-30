using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VnpayPymentQR.Models;
using VnpayPymentQR.Services;
using VnpayPymentQR.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace VnpayPymentQR.Controllers
{


    public class HomeController : Controller
    {
        private readonly VnpayConfig _vnpayConfig;
        private readonly OrderService _orderService;

        public HomeController(IOptions<VnpayConfig> vnpayOptions, OrderService orderService)
        {
            _vnpayConfig = vnpayOptions.Value;
            _orderService = orderService;
        }
        public IActionResult Index()
        {
            ViewBag.Orders = _orderService.GetAllOrders().OrderByDescending(o => o.CreateDate).ToList();
            return View("Index"); // Tạo Index.cshtml
        }
        public IActionResult PaymentForm()
        {
            // Có thể truyền thêm dữ liệu nếu cần
            ViewBag.TmnCode = _vnpayConfig.TmnCode; // Nếu muốn hiển thị sẵn
            return View();
        }
        [HttpPost]
        public IActionResult CreatePayment(string amount, string vnp_PromoCode = "")
        {
            var orderId = DateTime.Now.ToString("yyyyMMddHHmmss");
            var expireDate = DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss");

            decimal amt = decimal.Parse(amount);


            var order = new Order
            {
                OrderId = orderId,
                Amount = amt,
                OrderInfo = "Test thanh toan tien Thuoc",
                CreateDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                ExpireDate = DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
                PromoCode = vnp_PromoCode,
                Status = "Pending"
            };
            _orderService.AddOrder(order);

            var vnpParams = new SortedDictionary<string, string>
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", _vnpayConfig.TmnCode },
                { "vnp_Amount", (long.Parse(amount) * 100).ToString() },
                { "vnp_CurrCode", "VND" },
                { "vnp_TxnRef", orderId }, // Sửa lỗi gốc
                { "vnp_OrderInfo", "Test thanh toan tien Thuoc" },
                { "vnp_OrderType", "oldmc" },
                { "vnp_Locale", "vn" },
                { "vnp_ReturnUrl", _vnpayConfig.ReturnUrl },
                { "vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1" },
                { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
                { "vnp_ExpireDate", expireDate }
            };

            if (!string.IsNullOrEmpty(vnp_PromoCode))
            {
                vnpParams["vnp_PromoCode"] = vnp_PromoCode + amount;
            }

            var helper = new VnpayHelper(_vnpayConfig.HashSecret);
            var signature = helper.CreateSignature(vnpParams);

            var queryString = string.Join("&", vnpParams.Select(kvp => $"{kvp.Key}={System.Net.WebUtility.UrlEncode(kvp.Value)}"));
            var paymentUrl = $"{_vnpayConfig.PayUrl}?{queryString}&vnp_SecureHash={signature}";

            return Redirect(paymentUrl);
        }

        // IPN (Instant Payment Notification) - VNPAY gọi server-to-server
        [HttpGet]
        public IActionResult PaymentIpn()
        {
            var query = Request.Query;
            var vnp_SecureHash = query["vnp_SecureHash"].FirstOrDefault();
            var orderId = query["vnp_TxnRef"].FirstOrDefault();

            if (query.Count > 0)
            {
                var helper = new VnpayHelper(_vnpayConfig.HashSecret);
                var secureHash = query["vnp_SecureHash"];
                if (helper.ValidateSignature(query.ToDictionary(k => k.Key, v => v.Value.FirstOrDefault()), secureHash))
                {
                    var responseCode = query["vnp_ResponseCode"];
                    if (responseCode == "00")
                    {
                        var order = _orderService.GetOrderById(orderId);
                        if (order != null)
                        {
                            order.ResponseCode = query["vnp_ResponseCode"];
                            order.TransactionNo = query["vnp_TransactionNo"];
                            order.BankCode = query["vnp_BankCode"];
                            order.PayDate = query["vnp_PayDate"];
                            order.Status = query["vnp_ResponseCode"] == "00" ? "Success" : "Failed";
                            _orderService.UpdateOrder(order);
                        }
                        // Thanh toán thành công - cập nhật DB ở đây
                        return Json(new { RspCode = "00", Message = "Confirm Success" });
                    }
                    else
                    {
                        return Json(new { RspCode = "01", Message = "Payment Error" });
                    }
                }
                else
                {
                    return Json(new { RspCode = "97", Message = "Invalid Signature" });
                }
            }
            return Json(new { RspCode = "99", Message = "Invalid request" });
        }

        // Return URL - Hiển thị cho user
        [HttpGet]
        public IActionResult PaymentReturn()
        {
            var model = new VnpayResponseModel
            {
                vnp_Amount = Request.Query["vnp_Amount"],
                vnp_BankCode = Request.Query["vnp_BankCode"],
                vnp_BankTranNo = Request.Query["vnp_BankTranNo"],
                vnp_CardType = Request.Query["vnp_CardType"],
                vnp_OrderInfo = Request.Query["vnp_OrderInfo"],
                vnp_PayDate = Request.Query["vnp_PayDate"],
                vnp_ResponseCode = Request.Query["vnp_ResponseCode"],
                vnp_TmnCode = Request.Query["vnp_TmnCode"],
                vnp_TransactionNo = Request.Query["vnp_TransactionNo"],
                vnp_TransactionStatus = Request.Query["vnp_TransactionStatus"],
                vnp_TxnRef = Request.Query["vnp_TxnRef"],
                vnp_SecureHash = Request.Query["vnp_SecureHash"]
            };

            return View("PaymentResult", model); // Views/Home/PaymentResult.cshtml
        }


        // ... các using khác

        [HttpPost]
        public async Task<IActionResult> QueryDR([FromBody] QueryDrInput input)
        {
            if (string.IsNullOrEmpty(input?.OrderId))
                return Json(new { success = false, message = "orderId không hợp lệ" });

            var order = _orderService.GetOrderById(input.OrderId);
            if (order == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            var url = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction"; // Production: bỏ sandbox.

            var requestId = Guid.NewGuid().ToString("N").Substring(0, 20); // Random string
            var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            var ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Thứ tự fields chính xác theo tài liệu VNPAY cho querydr
            //var data = $"{requestId}|2.1.0|querydr|{_vnpayConfig.TmnCode}|{order.OrderId}|{order.CreateDate}|{createDate}|{ipAddr}|{order.OrderInfo}";
            var data = buildQueryDrData("2.1.3", requestId, "querydr", _vnpayConfig.TmnCode, order, createDate, ipAddr);
            var secureHash = HmacSHA512(_vnpayConfig.HashSecret, data);

            var payload = new
            {
                vnp_RequestId = requestId,
                vnp_Version = "2.1.3",
                vnp_Command = "querydr",
                vnp_TmnCode = _vnpayConfig.TmnCode,
                vnp_TxnRef = order.OrderId,
                vnp_TransactionDate = order.CreateDate, // Format yyyyMMddHHmmss từ IPN
                vnp_CreateDate = createDate,
                vnp_IpAddr = ipAddr,
                vnp_OrderInfo = order.OrderInfo,
                vnp_SecureHash = secureHash
            };

            using var client = new HttpClient();
            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Json(new { success = false, message = "Lỗi kết nối VNPAY", details = responseString });

                var json = JObject.Parse(responseString);

                // Validate chữ ký response
                var respHash = json["vnp_SecureHash"]?.ToString();
                if (string.IsNullOrEmpty(respHash))
                    return Json(new { success = false, message = "Không có chữ ký từ VNPAY" });

                // Xây dựng data verify (bao gồm cả promotion nếu có)
                var promotionCode = json["vnp_PromotionCode"]?.ToString() ?? "";
                var promotionAmount = json["vnp_PromotionAmount"]?.ToString() ?? "";

                var verifyData = BuildVerifyData(json);

                var calculatedHash = HmacSHA512(_vnpayConfig.HashSecret, verifyData);

                if (!calculatedHash.Equals(respHash, StringComparison.OrdinalIgnoreCase))
                    return Json(new { success = false, message = "Chữ ký response không hợp lệ" });

                // Cập nhật order từ response
                var respCode = json["vnp_ResponseCode"]?.ToString();
                order.ResponseCode = respCode;
                order.TransactionNo = json["vnp_TransactionNo"]?.ToString();
                order.BankCode = json["vnp_BankCode"]?.ToString();
                order.PayDate = json["vnp_PayDate"]?.ToString();

                order.Status = respCode == "00" ? "Success" : "Failed";

                _orderService.UpdateOrder(order);

                return Json(new
                {
                    success = true,
                    message = json["vnp_Message"]?.ToString() ?? "Query thành công",
                    data = json,
                    status = order.Status
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Refund([FromBody] RefundInput input) // refundAmount: số tiền gốc (VD: 100000)
        {
            if (input == null || string.IsNullOrEmpty(input.OrderId) || input.RefundAmount <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var order = _orderService.GetOrderById(input.OrderId);
            if (order == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            if (order.Status != "Success")
                return Json(new { success = false, message = "Chỉ hoàn tiền cho giao dịch thành công" });

            if (input.RefundAmount > order.Amount)
                return Json(new { success = false, message = "Số tiền hoàn không được lớn hơn số tiền giao dịch" });

            // Xác định loại hoàn: 02 = full, 03 = partial
            var transType = input.RefundAmount == order.Amount ? "02" : "03";

            var url = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

            var requestId = Guid.NewGuid().ToString("N").Substring(0, 20);
            var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            var ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Thứ tự fields cho refund (khác querydr một chút)
            var data = $"{requestId}|2.1.0|refund|{_vnpayConfig.TmnCode}|{transType}|{order.OrderId}|" +
                       $"{(long)(input.RefundAmount * 100)}|{order.TransactionNo}|{order.PayDate}|admin|{createDate}|{ipAddr}|" +
                       $"Hoàn tiền đơn hàng {order.OrderId}";

            var secureHash = HmacSHA512(_vnpayConfig.HashSecret, data);

            var payload = new
            {
                vnp_RequestId = requestId,
                vnp_Version = "2.1.0",
                vnp_Command = "refund",
                vnp_TmnCode = _vnpayConfig.TmnCode,
                vnp_TransactionType = transType,
                vnp_TxnRef = order.OrderId,
                vnp_Amount = (long)(input.RefundAmount * 100),
                vnp_TransactionNo = order.TransactionNo,
                vnp_TransactionDate = order.PayDate,
                vnp_CreateBy = "admin", // Tên người thực hiện hoàn (có thể thay bằng username)
                vnp_CreateDate = createDate,
                vnp_IpAddr = ipAddr,
                vnp_OrderInfo = $"Hoàn tiền đơn hàng {order.OrderId}",
                vnp_SecureHash = secureHash
            };

            using var client = new HttpClient();
            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var respCode = json["vnp_ResponseCode"]?.ToString();

                if (respCode == "00")
                {
                    order.Status = "Refunded"; // Hoặc "PartiallyRefunded" nếu partial
                    _orderService.UpdateOrder(order);
                }

                return Json(new
                {
                    success = true,
                    message = $"Đã gửi yêu cầu hoàn {input.RefundAmount:N0} VND thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // Hàm HMAC SHA512 (giữ nguyên từ code bạn)
        private static string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                foreach (var b in hashValue)
                    hash.Append(b.ToString("x2"));
            }
            return hash.ToString();
        }


        private string buildQueryDrData(
        string version,
        string requestId,
        string command,
        string tmnCode,
        Order order,
        string createDate,
        string ipAddr
)
        {
            if ("2.1.3".Equals(version))
            {
                // Version 2.1.3: CÓ vnp_TransactionNo
                return String.Join("|",
                        requestId,
                        version,
                        command,
                        tmnCode,
                        order.OrderId,           // vnp_TxnRef
                        order.TransactionNo,     // vnp_TransactionNo
                        order.CreateDate,        // vnp_TransactionDate
                        createDate,
                        ipAddr,
                        order.OrderInfo
                );
            }

            // Default: version 2.1.0 (KHÔNG có vnp_TransactionNo)
            return String.Join("|",
                    requestId,
                    version,
                    command,
                    tmnCode,
                    order.OrderId,               // vnp_TxnRef
                    order.CreateDate,            // vnp_TransactionDate
                    createDate,
                    ipAddr,
                    order.OrderInfo
            );
        }


        private string BuildVerifyData(JObject json)
        {
            // Lấy version, mặc định 2.1.0
            string version = json.Value<string>("vnp_Version")?.Trim() ?? "2.1.3";

            // Helper: lấy string, nếu null / missing => ""
            string V(string key) => json.Value<string>(key) ?? "";

            if (version == "2.1.3")
            {
                // Version 2.1.3
                return string.Join("|",
                    V("vnp_ResponseId"),
                    V("vnp_Command"),
                    V("vnp_ResponseCode"),
                    V("vnp_Message"),
                    V("vnp_TmnCode"),
                    V("vnp_TxnRef"),
                    V("vnp_Trace"),
                    V("vnp_Amount"),
                    V("vnp_FeeAmount"),
                    V("vnp_CurrCode"),
                    V("vnp_BankCode"),
                    V("vnp_CardNumber"),
                    V("vnp_CardHolder"),
                    V("vnp_MobileNumber"),
                    V("vnp_PayDate"),
                    V("vnp_TransactionNo"),
                    V("vnp_TransactionType"),
                    V("vnp_TransactionStatus"),
                    V("vnp_OrderInfo"),
                    V("vnp_PromotionCode"),
                    V("vnp_PromotionAmount"),
                    V("vnp_CardType"),
                    V("vnp_PayType"),
                    V("vnp_AccountType"),
                    V("vnp_Issuer"),
                    V("vnp_ApprovalCode")
                );
            }

            // Default: version 2.1.0
            return string.Join("|",
                V("vnp_ResponseId"),
                V("vnp_Command"),
                V("vnp_ResponseCode"),
                V("vnp_Message"),
                V("vnp_TmnCode"),
                V("vnp_TxnRef"),
                V("vnp_Amount"),
                V("vnp_BankCode"),
                V("vnp_PayDate"),
                V("vnp_TransactionNo"),
                V("vnp_TransactionType"),
                V("vnp_TransactionStatus"),
                V("vnp_OrderInfo"),
                V("vnp_PromotionCode"),
                V("vnp_PromotionAmount")
            );
        }
    }

}
