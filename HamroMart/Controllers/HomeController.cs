using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HamroMart.Data;
using HamroMart.ViewModels;

namespace HamroMart.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Debug: Check what products are being retrieved
            var allProducts = await _context.Products.ToListAsync();
            _logger.LogInformation("Total products in database: {Count}", allProducts.Count);

            foreach (var product in allProducts)
            {
                _logger.LogInformation("Product: {Name}, Stock: {Stock}, Active: {Active}",
                    product.Name, product.StockQuantity, product.IsActive);
            }

            var featuredProducts = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity > 0)
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
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

            _logger.LogInformation("Featured products count: {Count}", featuredProducts.Count);

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .Take(6)
                .ToListAsync();

            ViewBag.Categories = categories;
            return View(featuredProducts);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}