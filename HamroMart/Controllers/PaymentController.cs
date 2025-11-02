using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HamroMart.Data;
using HamroMart.Models;
using HamroMart.Services;

namespace HamroMart.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IKhaltiService _khaltiService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IKhaltiService khaltiService,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _userManager = userManager;
            _khaltiService = khaltiService;
            _logger = logger;
        }

        // POST: Payment/ProcessKhalti
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessKhalti(int orderId, string khaltiToken, string mobile)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                if (order.PaymentStatus == PaymentStatus.Completed)
                {
                    return Json(new { success = false, message = "Payment has already been processed for this order." });
                }

                // Verify payment with Khalti
                var khaltiResponse = await _khaltiService.VerifyPayment(
                    khaltiToken, order.TotalAmount, mobile);

                if (khaltiResponse?.state == "Completed")
                {
                    // Payment successful
                    order.PaymentStatus = PaymentStatus.Completed;
                    order.OrderStatus = OrderStatus.Processing;
                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Khalti payment successful for order {OrderNumber}", order.OrderNumber);

                    return Json(new
                    {
                        success = true,
                        message = "Payment processed successfully!",
                        orderId = order.Id
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "Payment verification failed. Please try again."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Khalti payment");
                return Json(new
                {
                    success = false,
                    message = "Error processing payment. Please try again."
                });
            }
        }

        // GET: Payment/Success
        public IActionResult Success(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }

        // GET: Payment/Failed
        public IActionResult Failed(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}