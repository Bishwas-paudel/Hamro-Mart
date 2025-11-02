using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HamroMart.Data;
using HamroMart.Models;
using HamroMart.ViewModels;

namespace HamroMart.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<CartController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var cartItems = await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .Include(ci => ci.Product)
                .ToListAsync();

            var viewModel = new CartViewModel
            {
                Items = cartItems.Select(ci => new CartItemViewModel
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ImageUrl = ci.Product.ImageUrl,
                    Price = ci.Product.DiscountPrice ?? ci.Product.Price,
                    Quantity = ci.Quantity,
                    Unit = ci.Product.Unit,
                    StockQuantity = ci.Product.StockQuantity
                }).ToList()
            };

            return View(viewModel);
        }

        // POST: Cart/Add
        [HttpPost]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var product = await _context.Products.FindAsync(productId);

                if (product == null || !product.IsActive || product.StockQuantity < quantity)
                {
                    return Json(new { success = false, message = "Product not available or insufficient stock." });
                }

                var existingCartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);

                if (existingCartItem != null)
                {
                    existingCartItem.Quantity += quantity;
                    if (existingCartItem.Quantity > product.StockQuantity)
                    {
                        existingCartItem.Quantity = product.StockQuantity;
                    }
                }
                else
                {
                    var cartItem = new CartItem
                    {
                        UserId = userId,
                        ProductId = productId,
                        Quantity = quantity,
                        AddedOn = DateTime.Now
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                var cartCount = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .SumAsync(ci => ci.Quantity);

                return Json(new { success = true, message = "Product added to cart!", cartCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product to cart");
                return Json(new { success = false, message = "Error adding product to cart." });
            }
        }

        // POST: Cart/Update
        [HttpPost]
        public async Task<IActionResult> Update(int cartItemId, int quantity)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Product)
                    .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Cart item not found." });
                }

                if (quantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    if (quantity > cartItem.Product.StockQuantity)
                    {
                        return Json(new { success = false, message = $"Only {cartItem.Product.StockQuantity} items available in stock." });
                    }
                    cartItem.Quantity = quantity;
                }

                await _context.SaveChangesAsync();

                // Recalculate totals
                var cartItems = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .Include(ci => ci.Product)
                    .ToListAsync();

                var totalAmount = cartItems.Sum(ci => ci.Quantity * (ci.Product.DiscountPrice ?? ci.Product.Price));
                var totalItems = cartItems.Sum(ci => ci.Quantity);

                return Json(new
                {
                    success = true,
                    totalAmount = totalAmount.ToString("N2"),
                    totalItems,
                    itemTotal = (cartItem.Quantity * (cartItem.Product.DiscountPrice ?? cartItem.Product.Price)).ToString("N2")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item");
                return Json(new { success = false, message = "Error updating cart item." });
            }
        }

        // POST: Cart/Remove
        [HttpPost]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

                if (cartItem != null)
                {
                    _context.CartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();
                }

                var cartCount = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .SumAsync(ci => ci.Quantity);

                return Json(new { success = true, cartCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item");
                return Json(new { success = false, message = "Error removing item from cart." });
            }
        }

        // POST: Cart/Clear
        [HttpPost]
        public async Task<IActionResult> Clear()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var cartItems = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .ToListAsync();

                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cart cleared successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return Json(new { success = false, message = "Error clearing cart." });
            }
        }

        // GET: Cart/Count
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { count = 0 });
            }

            var userId = _userManager.GetUserId(User);
            var cartCount = await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .SumAsync(ci => ci.Quantity);

            return Json(new { count = cartCount });
        }
    }
}