using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HamroMart.Data;
using HamroMart.Models;
using HamroMart.ViewModels;

namespace HamroMart.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // DASHBOARD
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var model = new AdminDashboardViewModel
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalProducts = await _context.Products.CountAsync(p => p.IsActive),
                    TotalOrders = await _context.Orders.CountAsync(),
                    PendingOrders = await _context.Orders.CountAsync(o => o.OrderStatus == OrderStatus.Pending),
                    TotalRevenue = await _context.Orders
                        .Where(o => o.PaymentStatus == PaymentStatus.Completed)
                        .SumAsync(o => o.TotalAmount),
                    TodayRevenue = await _context.Orders
                        .Where(o => o.PaymentStatus == PaymentStatus.Completed &&
                                   o.OrderDate.Date == DateTime.Today)
                        .SumAsync(o => o.TotalAmount)
                };

                // Recent orders
                model.RecentOrders = await _context.Orders
                    .Include(o => o.User)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .Select(o => new RecentOrderViewModel
                    {
                        OrderId = o.Id,
                        OrderNumber = o.OrderNumber,
                        CustomerName = o.User.FirstName + " " + o.User.LastName,
                        Amount = o.TotalAmount,
                        Status = o.OrderStatus.ToString(),
                        OrderDate = o.OrderDate
                    })
                    .ToListAsync();

                // Popular products
                model.PopularProducts = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .Where(oi => oi.Order.PaymentStatus == PaymentStatus.Completed)
                    .GroupBy(oi => new { oi.ProductId, oi.Product.Name, oi.Product.ImageUrl })
                    .Select(g => new PopularProductViewModel
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.Name,
                        SalesCount = g.Sum(oi => oi.Quantity),
                        Revenue = g.Sum(oi => oi.TotalPrice),
                        ImageUrl = g.Key.ImageUrl
                    })
                    .OrderByDescending(p => p.SalesCount)
                    .Take(5)
                    .ToListAsync();

                // Analytics data
                model.Analytics = new AnalyticsViewModel
                {
                    AverageOrderValue = model.TotalOrders > 0 ? model.TotalRevenue / model.TotalOrders : 0,
                    ProductsLowStock = await _context.Products.CountAsync(p => p.StockQuantity < 10 && p.StockQuantity > 0),
                    NewCustomersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt.Month == DateTime.Now.Month && u.CreatedAt.Year == DateTime.Now.Year)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard data";
                return View(new AdminDashboardViewModel());
            }
        }

        // PRODUCTS MANAGEMENT
        public async Task<IActionResult> Products(int page = 1, string search = "")
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * 10)
                .Take(10)
                .ToListAsync();

            var totalProducts = await query.CountAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / 10.0);
            ViewBag.SearchTerm = search;

            return View(products);
        }

        // CATEGORIES MANAGEMENT
        // CATEGORIES MANAGEMENT
        // CATEGORIES MANAGEMENT
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        [HttpGet]
        public IActionResult CreateCategory()
        {
            return View(new Category()); // Initialize with empty category
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(Category category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Set default values
                    category.CreatedAt = DateTime.Now;

                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();

                    await LogAudit("Created", "Category", category.Id, $"Created category: {category.Name}");
                    TempData["SuccessMessage"] = $"Category '{category.Name}' created successfully!";
                    return RedirectToAction(nameof(Categories));
                }

                // If we got here, something went wrong with validation
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Model error: {Error}", error.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                TempData["ErrorMessage"] = $"An error occurred while creating the category: {ex.Message}";
            }

            return View(category);
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    TempData["ErrorMessage"] = "Category not found.";
                    return RedirectToAction(nameof(Categories));
                }
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading category for edit");
                TempData["ErrorMessage"] = "Error loading category.";
                return RedirectToAction(nameof(Categories));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, Category category)
        {
            try
            {
                if (id != category.Id)
                {
                    TempData["ErrorMessage"] = "Category ID mismatch.";
                    return RedirectToAction(nameof(Categories));
                }

                if (ModelState.IsValid)
                {
                    var existingCategory = await _context.Categories.FindAsync(id);
                    if (existingCategory == null)
                    {
                        TempData["ErrorMessage"] = "Category not found.";
                        return RedirectToAction(nameof(Categories));
                    }

                    // Update only the properties that should change
                    existingCategory.Name = category.Name;
                    existingCategory.Description = category.Description;
                    existingCategory.ImageUrl = category.ImageUrl;
                    existingCategory.IsActive = category.IsActive;

                    _context.Categories.Update(existingCategory);
                    await _context.SaveChangesAsync();

                    await LogAudit("Updated", "Category", category.Id, $"Updated category: {category.Name}");
                    TempData["SuccessMessage"] = $"Category '{category.Name}' updated successfully!";
                    return RedirectToAction(nameof(Categories));
                }

                // Log validation errors
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Model validation error: {Error}", error.ErrorMessage);
                }

                return View(category);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(category.Id))
                {
                    TempData["ErrorMessage"] = "Category not found.";
                    return RedirectToAction(nameof(Categories));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category");
                TempData["ErrorMessage"] = $"An error occurred while updating the category: {ex.Message}";
                return View(category);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    TempData["ErrorMessage"] = "Category not found.";
                    return RedirectToAction(nameof(Categories));
                }

                // Check if category has products
                if (category.Products.Any())
                {
                    TempData["ErrorMessage"] = $"Cannot delete category '{category.Name}' because it has {category.Products.Count} product(s). Please reassign or delete the products first.";
                    return RedirectToAction(nameof(Categories));
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                await LogAudit("Deleted", "Category", id, $"Deleted category: {category.Name}");
                TempData["SuccessMessage"] = $"Category '{category.Name}' deleted successfully!";
                return RedirectToAction(nameof(Categories));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category");
                TempData["ErrorMessage"] = $"An error occurred while deleting the category: {ex.Message}";
                return RedirectToAction(nameof(Categories));
            }
        }

        //private bool CategoryExists(int id)
        //{
        //    return _context.Categories.Any(e => e.Id == id);
        //}

        //private bool CategoryExists(int id)
        //{
        //    return _context.Categories.Any(e => e.Id == id);
        //}

        // ORDERS MANAGEMENT
        public async Task<IActionResult> Orders(int page = 1, string status = "")
        {
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            {
                query = query.Where(o => o.OrderStatus == orderStatus);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * 10)
                .Take(10)
                .ToListAsync();

            var totalOrders = await query.CountAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / 10.0);
            ViewBag.SelectedStatus = status;
            ViewBag.OrderStatuses = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            var oldStatus = order.OrderStatus;
            order.OrderStatus = status;

            if (status == OrderStatus.Shipped)
            {
                order.ShippedDate = DateTime.Now;
            }
            else if (status == OrderStatus.Delivered)
            {
                order.DeliveredDate = DateTime.Now;
                order.PaymentStatus = PaymentStatus.Completed;
            }

            _context.Update(order);
            await _context.SaveChangesAsync();

            await LogAudit("Updated", "Order", order.Id, $"Updated order status from {oldStatus} to {status}");
            TempData["SuccessMessage"] = $"Order status updated to {status}";
            return RedirectToAction(nameof(Orders));
        }

        // USERS MANAGEMENT
        public async Task<IActionResult> Users(int page = 1, string search = "")
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search) || u.Email.Contains(search));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * 10)
                .Take(10)
                .ToListAsync();

            var totalUsers = await query.CountAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / 10.0);
            ViewBag.SearchTerm = search;

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent admin from deleting themselves
            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                await LogAudit("Deleted", "User", 0, $"Deleted user: {user.Email}");
                TempData["SuccessMessage"] = "User deleted successfully";
            }
            else
            {
                TempData["ErrorMessage"] = "Error deleting user";
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId, bool isActive)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // You can add logic here to disable/enable user
            // For now, we'll just update email confirmation status
            user.EmailConfirmed = isActive;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var action = isActive ? "Enabled" : "Disabled";
                await LogAudit("Updated", "User", 0, $"{action} user: {user.Email}");
                TempData["SuccessMessage"] = $"User {action.ToLower()} successfully";
            }
            else
            {
                TempData["ErrorMessage"] = "Error updating user status";
            }

            return RedirectToAction(nameof(Users));
        }

        // REPORTS
        public async Task<IActionResult> Reports(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today;

            var model = new ReportsViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value
            };

            // Get sales data
            var salesData = await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.PaymentStatus == PaymentStatus.Completed)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new SalesData
                {
                    Date = g.Key,
                    Revenue = g.Sum(o => o.TotalAmount),
                    Orders = g.Count()
                })
                .OrderBy(s => s.Date)
                .ToListAsync();

            model.SalesData = salesData;

            // Get category sales
            ViewBag.CategorySales = await _context.OrderItems
                .Include(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Where(oi => oi.Order.OrderDate >= startDate && oi.Order.OrderDate <= endDate && oi.Order.PaymentStatus == PaymentStatus.Completed)
                .GroupBy(oi => oi.Product.Category.Name)
                .Select(g => new CategorySalesData
                {
                    CategoryName = g.Key,
                    Revenue = g.Sum(oi => oi.TotalPrice),
                    Orders = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(c => c.Revenue)
                .Take(10)
                .ToListAsync();

            return View(model);
        }

        // AUDIT LOGS
        public async Task<IActionResult> AuditLogs(int page = 1)
        {
            var logs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            var totalLogs = await _context.AuditLogs.CountAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalLogs / 20.0);

            return View(logs);
        }

        // HELPER METHODS
        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
        private async Task LogAudit(string action, string entity, int entityId, string description)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Cannot log audit: User ID is null");
                    return;
                }

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    Description = description,
                    Timestamp = DateTime.Now,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action} on {Entity} {EntityId}", action, entity, entityId);
                // Don't throw, just log the error
            }
        }
    }
}