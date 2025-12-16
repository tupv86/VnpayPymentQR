using System.Text.Json;
using VnpayPymentQR.Models;
namespace VnpayPymentQR.Services
{
    public class OrderService
    {
        private readonly string _filePath = "Data/orders.json";

        public OrderService()
        {
            // Tạo thư mục nếu chưa có
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            // Tạo file rỗng nếu chưa tồn tại
            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, "[]");
        }

        public List<Order> GetAllOrders()
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Order>>(json) ?? new List<Order>();
        }

        public void SaveOrders(List<Order> orders)
        {
            var json = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        public void AddOrder(Order order)
        {
            var orders = GetAllOrders();
            orders.Add(order);
            SaveOrders(orders);
        }

        public Order? GetOrderById(string orderId)
        {
            return GetAllOrders().FirstOrDefault(o => o.OrderId == orderId);
        }

        public void UpdateOrder(Order updatedOrder)
        {
            var orders = GetAllOrders();
            var index = orders.FindIndex(o => o.OrderId == updatedOrder.OrderId);
            if (index != -1)
            {
                orders[index] = updatedOrder;
                SaveOrders(orders);
            }
        }
    }
}
