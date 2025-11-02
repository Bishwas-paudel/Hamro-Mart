using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HamroMart.Data;
using HamroMart.Models;
using HamroMart.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HamroMart.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager,
            ILogger<ProductsController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Products
        public async Task<IActionResult> Index(int? categoryId, string search, int page = 1, int pageSize = 12)
        {
            var productsQuery = _context.Products
                .Where(p => p.IsActive)
                .Include(p => p.Category)
                .AsQueryable();

            // Filter by category
            if (categoryId.HasValue && categoryId > 0)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
                ViewBag.SelectedCategory = await _context.Categories.FindAsync(categoryId.Value);
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                productsQuery = productsQuery.Where(p =>
                    p.Name.Contains(search) ||
                    p.Description.Contains(search) ||
                    p.Brand.Contains(search));
            }

            // Pagination
            var totalProducts = await productsQuery.CountAsync();
            var products = await productsQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    DiscountPrice = p.DiscountPrice,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    Brand = p.Brand,
                    Unit = p.Unit,
                    CategoryName = p.Category.Name,
                    CategoryId = p.CategoryId,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var model = new ProductListViewModel
            {
                Products = products,
                Categories = categories,
                SelectedCategoryId = categoryId,
                SearchTerm = search,
                CurrentPage = page,
                PageSize = pageSize,
                TotalProducts = totalProducts,
                TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize)
            };

            return View(model);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            var viewModel = new ProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                StockQuantity = product.StockQuantity,
                ImageUrl = product.ImageUrl,
                Brand = product.Brand,
                Unit = product.Unit,
                CategoryName = product.Category.Name,
                CategoryId = product.CategoryId,
                IsActive = product.IsActive
            };

            // Get related products
            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.IsActive)
                .Take(4)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    DiscountPrice = p.DiscountPrice,
                    ImageUrl = p.ImageUrl,
                    Brand = p.Brand,
                    Unit = p.Unit
                })
                .ToListAsync();

            return View(viewModel);
        }

        // GET: Products/Create (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View();
        }

        // POST: Products/Create (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(ProductCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string imageUrl = "/images/products/placeholder.jpg";

                    // Handle image upload
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ImageFile.CopyToAsync(fileStream);
                        }

                        imageUrl = "/images/products/" + uniqueFileName;
                    }

                    var product = new Product
                    {
                        Name = model.Name,
                        Description = model.Description,
                        Price = model.Price,
                        DiscountPrice = model.DiscountPrice,
                        StockQuantity = model.StockQuantity,
                        ImageUrl = imageUrl,
                        Brand = model.Brand,
                        Unit = model.Unit,
                        CategoryId = model.CategoryId,
                        IsActive = model.IsActive,
                        CreatedAt = DateTime.Now
                    };

                    _context.Add(product);
                    await _context.SaveChangesAsync();

                    // Log the action
                    await LogAudit("Created", "Product", product.Id, $"Created product: {product.Name}");

                    TempData["SuccessMessage"] = "Product created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating product");
                    ModelState.AddModelError("", "An error occurred while creating the product. Please try again.");
                }
            }

            // Reload categories if validation fails
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View(model);
        }

        // GET: Products/Edit/5 (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            var model = new ProductEditViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                StockQuantity = product.StockQuantity,
                Brand = product.Brand,
                Unit = product.Unit,
                CategoryId = product.CategoryId,
                IsActive = product.IsActive,
                CurrentImageUrl = product.ImageUrl
            };

            return View(model);
        }

        // POST: Products/Edit/5 (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, ProductEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            // Reload categories for the view in case of validation errors
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            if (ModelState.IsValid)
            {
                try
                {
                    var product = await _context.Products.FindAsync(id);
                    if (product == null)
                    {
                        return NotFound();
                    }

                    // Handle image upload if new file is provided
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ImageFile.CopyToAsync(fileStream);
                        }

                        // Delete old image if it's not the placeholder
                        if (!string.IsNullOrEmpty(product.ImageUrl) &&
                            !product.ImageUrl.Contains("placeholder") &&
                            System.IO.File.Exists(Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'))))
                        {
                            var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        product.ImageUrl = "/images/products/" + uniqueFileName;
                    }

                    // Update product properties
                    product.Name = model.Name;
                    product.Description = model.Description;
                    product.Price = model.Price;
                    product.DiscountPrice = model.DiscountPrice;
                    product.StockQuantity = model.StockQuantity;
                    product.Brand = model.Brand;
                    product.Unit = model.Unit;
                    product.CategoryId = model.CategoryId;
                    product.IsActive = model.IsActive;

                    _context.Products.Update(product);
                    await _context.SaveChangesAsync();

                    // Log the action
                    await LogAudit("Updated", "Product", product.Id, $"Updated product: {product.Name}");

                    TempData["SuccessMessage"] = "Product updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating product");
                    ModelState.AddModelError("", "An error occurred while updating the product. Please try again.");
                }
            }

            // If we got this far, something failed; redisplay form
            return View(model);
        }

        // POST: Products/Delete/5 (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.OrderItems)
                    .Include(p => p.CartItems)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if product has existing orders
                if (product.OrderItems.Any())
                {
                    // Instead of deleting, set as inactive
                    product.IsActive = false;
                    _context.Products.Update(product);
                    await _context.SaveChangesAsync();

                    await LogAudit("Deactivated", "Product", product.Id, $"Deactivated product due to existing orders: {product.Name}");
                    TempData["SuccessMessage"] = "Product has been deactivated because it has existing orders. It will no longer appear in the product catalog.";
                }
                else
                {
                    // Remove cart items first (if any)
                    if (product.CartItems.Any())
                    {
                        _context.CartItems.RemoveRange(product.CartItems);
                    }

                    // Then remove the product
                    _context.Products.Remove(product);
                    await _context.SaveChangesAsync();

                    await LogAudit("Deleted", "Product", product.Id, $"Deleted product: {product.Name}");
                    TempData["SuccessMessage"] = "Product deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product");
                TempData["ErrorMessage"] = "An error occurred while deleting the product. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Products/Manage (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage(int page = 1, int pageSize = 10)
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalProducts = await _context.Products.CountAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            return View(products);
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
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
                // Don't throw, just log the error - we don't want audit failures to break main functionality
            }
        }
    }
}