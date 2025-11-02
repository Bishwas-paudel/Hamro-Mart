using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using HamroMart.Models;

namespace HamroMart.Services
{
    public class KhaltiPaymentRequest
    {
        public string token { get; set; }
        public decimal amount { get; set; }
        public string mobile { get; set; }
        public string product_identity { get; set; }
        public string product_name { get; set; }
        public string product_url { get; set; }
    }

    public class KhaltiPaymentResponse
    {
        public string idx { get; set; }
        public KhaltiPaymentDetail payment { get; set; }
        public string token { get; set; }
        public string state { get; set; }
        public string type { get; set; }
    }

    public class KhaltiPaymentDetail
    {
        public string idx { get; set; }
        public decimal amount { get; set; }
        public string mobile { get; set; }
        public string product_identity { get; set; }
        public string product_name { get; set; }
        public string product_url { get; set; }
        public string token { get; set; }
        public string state { get; set; }
    }

    public interface IKhaltiService
    {
        Task<KhaltiPaymentResponse> VerifyPayment(string token, decimal amount, string mobile);
        Task<KhaltiPaymentResponse> GetPaymentDetail(string pidx);
    }

    public class KhaltiService : IKhaltiService
    {
        private readonly KhaltiSettings _khaltiSettings;
        private readonly HttpClient _httpClient;

        public KhaltiService(IOptions<KhaltiSettings> khaltiSettings, HttpClient httpClient)
        {
            _khaltiSettings = khaltiSettings.Value;
            _httpClient = httpClient;

            // Set up HttpClient for Khalti API
            _httpClient.BaseAddress = new Uri(_khaltiSettings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Key {_khaltiSettings.LiveSecretKey}");
        }

        public async Task<KhaltiPaymentResponse> VerifyPayment(string token, decimal amount, string mobile)
        {
            try
            {
                var request = new KhaltiPaymentRequest
                {
                    token = token,
                    amount = amount * 100, // Khalti expects amount in paisa
                    mobile = mobile,
                    product_identity = "hamromart-order",
                    product_name = "HamroMart Order",
                    product_url = "https://hamromart.com"
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("payment/verify/", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var paymentResponse = JsonSerializer.Deserialize<KhaltiPaymentResponse>(responseContent);

                return paymentResponse;
            }
            catch (Exception ex)
            {
                throw new Exception($"Khalti payment verification failed: {ex.Message}");
            }
        }

        public async Task<KhaltiPaymentResponse> GetPaymentDetail(string pidx)
        {
            try
            {
                var response = await _httpClient.GetAsync($"payment/status/{pidx}/");
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var paymentDetail = JsonSerializer.Deserialize<KhaltiPaymentResponse>(responseContent);

                return paymentDetail;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get payment details: {ex.Message}");
            }
        }
    }
}