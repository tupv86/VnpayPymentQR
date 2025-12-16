using VnpayPymentQR.Models;
using VnpayPymentQR.Services;

var builder = WebApplication.CreateBuilder(args);
// Đọc cấu hình Vnpay và bind vào class
builder.Services.Configure<VnpayConfig>(builder.Configuration.GetSection("Vnpay"));

// Hoặc nếu bạn muốn inject trực tiếp như singleton (khuyến nghị cho config không thay đổi)
builder.Services.AddSingleton(provider =>
    builder.Configuration.GetSection("Vnpay").Get<VnpayConfig>() ?? new VnpayConfig());

builder.Services.AddSingleton<OrderService>();
// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
