using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HamroMart.Data;
using HamroMart.Models;
using HamroMart.Services;
using HamroMart.ViewModels;

namespace HamroMart.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<OrdersController> _logger;
        private readonly KhaltiSettings _khaltiSettings;

        public OrdersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<OrdersController> logger,
            IOptions<KhaltiSettings> khaltiSettings)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _khaltiSettings = khaltiSettings.Value;
        }

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderViewModel
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    PaymentStatus = o.PaymentStatus.ToString(),
                    OrderStatus = o.OrderStatus.ToString(),
                    ShippingAddress = o.ShippingAddress,
                    City = o.City,
                    PostalCode = o.PostalCode,
                    PhoneNumber = o.PhoneNumber
                })
                .ToListAsync();

            return View(orders);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            var viewModel = new OrderViewModel
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                OrderStatus = order.OrderStatus.ToString(),
                ShippingAddress = order.ShippingAddress,
                City = order.City,
                PostalCode = order.PostalCode,
                PhoneNumber = order.PhoneNumber,
                OrderItems = order.OrderItems.Select(oi => new OrderItemViewModel
                {
                    ProductName = oi.Product.Name,
                    ImageUrl = oi.Product.ImageUrl,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice,
                    Unit = oi.Product.Unit
                }).ToList()
            };

            return View(viewModel);
        }

        // GET: Orders/Create (Checkout)
        public async Task<IActionResult> Checkout()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .Include(ci => ci.Product)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty. Please add items to your cart before checkout.";
                return RedirectToAction("Index", "Cart");
            }

            // Check stock availability
            foreach (var item in cartItems)
            {
                if (item.Quantity > item.Product.StockQuantity)
                {
                    TempData["ErrorMessage"] = $"Sorry, only {item.Product.StockQuantity} units of {item.Product.Name} are available in stock.";
                    return RedirectToAction("Index", "Cart");
                }
            }

            var viewModel = new CheckoutViewModel
            {
                ShippingAddress = user?.Address,
                City = user?.City,
                PostalCode = user?.PostalCode,
                PhoneNumber = user?.PhoneNumber
            };

            ViewBag.CartItems = cartItems;
            ViewBag.TotalAmount = cartItems.Sum(ci => ci.Quantity * (ci.Product.DiscountPrice ?? ci.Product.Price));
            ViewBag.KhaltiPublicKey = _khaltiSettings.LivePublicKey;

            return View(viewModel);
        }



        [HttpPost]
        public async Task<IActionResult> ProcessKhalti(string khaltiToken, int amount, string orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderId);
            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Key {_khaltiSettings.LiveSecretKey}");

            var values = new Dictionary<string, string>
    {
        { "token", khaltiToken },
        { "amount", amount.ToString() }
    };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://test-pay.khalti.com//payment/verify/", content);
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                order.PaymentStatus = PaymentStatus.Completed;
                order.OrderStatus = OrderStatus.Processing;
                _context.Update(order);
                await _context.SaveChangesAsync();

                return Json(new { success = true, orderId = order.Id });
            }

            return Json(new { success = false, message = "Khalti verification failed." });
        }

        // POST: Orders/Create (Checkout)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var user = await _userManager.GetUserAsync(User);
                var cartItems = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .Include(ci => ci.Product)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    TempData["ErrorMessage"] = "Your cart is empty.";
                    return RedirectToAction("Index", "Cart");
                }

                if (ModelState.IsValid)
                {
                    // Create order
                    var order = new Order
                    {
                        UserId = userId,
                        OrderNumber = GenerateOrderNumber(),
                        ShippingAddress = model.ShippingAddress,
                        City = model.City,
                        PostalCode = model.PostalCode,
                        PhoneNumber = model.PhoneNumber,
                        PaymentMethod = Enum.Parse<PaymentMethod>(model.PaymentMethod),
                        PaymentStatus = PaymentStatus.Pending,
                        OrderStatus = OrderStatus.Pending,
                        TotalAmount = cartItems.Sum(ci => ci.Quantity * (ci.Product.DiscountPrice ?? ci.Product.Price)),
                        OrderDate = DateTime.Now
                    };

                    // Add order items
                    foreach (var cartItem in cartItems)
                    {
                        var orderItem = new OrderItem
                        {
                            ProductId = cartItem.ProductId,
                            Quantity = cartItem.Quantity,
                            UnitPrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price,
                            TotalPrice = cartItem.Quantity * (cartItem.Product.DiscountPrice ?? cartItem.Product.Price)
                        };
                        order.OrderItems.Add(orderItem);

                        // Update product stock
                        cartItem.Product.StockQuantity -= cartItem.Quantity;
                    }

                    _context.Orders.Add(order);

                    // Clear cart
                    _context.CartItems.RemoveRange(cartItems);

                    await _context.SaveChangesAsync();

                    // Process payment based on method
                    if (model.PaymentMethod == "CashOnDelivery")
                    {
                        order.PaymentStatus = PaymentStatus.Completed;
                        order.OrderStatus = OrderStatus.Processing;
                        _context.Update(order);
                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = $"Order placed successfully! Your order number is: {order.OrderNumber}";
                        return RedirectToAction("Details", new { id = order.Id });
                    }
                    else if (model.PaymentMethod == "Khalti")
                    {
                        return Json(new { success = true, orderId = order.Id, orderNumber = order.OrderNumber });
                    }

                    TempData["SuccessMessage"] = $"Order placed successfully! Your order number is: {order.OrderNumber}";
                    return RedirectToAction("Details", new { id = order.Id });
                }

                ViewBag.CartItems = cartItems;
                ViewBag.TotalAmount = cartItems.Sum(ci => ci.Quantity * (ci.Product.DiscountPrice ?? ci.Product.Price));
                ViewBag.KhaltiPublicKey = _khaltiSettings.LivePublicKey;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during checkout");
                TempData["ErrorMessage"] = "An error occurred during checkout. Please try again.";
                return RedirectToAction("Index", "Cart");
            }
        }

        // GET: Orders/KhaltiPayment/5
        public async Task<IActionResult> KhaltiPayment(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentStatus != PaymentStatus.Pending)
            {
                TempData["ErrorMessage"] = "Payment has already been processed for this order.";
                return RedirectToAction("Details", new { id });
            }

            ViewBag.KhaltiPublicKey = _khaltiSettings.LivePublicKey;
            return View(order);
        }

        // POST: Orders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                {
                    return NotFound();
                }

                if (order.OrderStatus != OrderStatus.Pending && order.OrderStatus != OrderStatus.Processing)
                {
                    TempData["ErrorMessage"] = "Order cannot be cancelled at this stage.";
                    return RedirectToAction("Details", new { id });
                }

                // Restore product stock
                foreach (var item in order.OrderItems)
                {
                    item.Product.StockQuantity += item.Quantity;
                }

                order.OrderStatus = OrderStatus.Cancelled;
                _context.Update(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Order cancelled successfully.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order");
                TempData["ErrorMessage"] = "Error cancelling order.";
                return RedirectToAction("Details", new { id });
            }
        }

        private string GenerateOrderNumber()
        {
            return "ORD" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }
    }
}