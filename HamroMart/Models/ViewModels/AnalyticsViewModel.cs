namespace HamroMart.ViewModels
{
    public class AnalyticsViewModel
    {
        public List<MonthlyRevenue> MonthlyRevenues { get; set; } = new List<MonthlyRevenue>();
        public List<CategorySales> CategorySales { get; set; } = new List<CategorySales>();
        public int NewCustomersThisMonth { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int ProductsLowStock { get; set; }
    }

    public class MonthlyRevenue
    {
        public string Month { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CategorySales
    {
        public string CategoryName { get; set; }
        public int SalesCount { get; set; }
        public decimal Revenue { get; set; }
    }
}