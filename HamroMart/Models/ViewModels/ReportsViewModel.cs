namespace HamroMart.ViewModels
{
    public class ReportsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<SalesData> SalesData { get; set; } = new List<SalesData>();
        public decimal TotalRevenue => SalesData.Sum(s => s.Revenue);
        public int TotalOrders => SalesData.Sum(s => s.Orders);
    }

    public class SalesData
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }
}