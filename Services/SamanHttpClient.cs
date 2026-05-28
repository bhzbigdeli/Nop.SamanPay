using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Nop.Core;

namespace Nop.Plugin.Payments.Saman.Services
{
    public class SamanHttpClient
    {
        private readonly HttpClient _httpClient;

        // Case-insensitive so the plugin survives any future Saman JSON casing changes
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public SamanHttpClient(HttpClient httpClient)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CurrentVersion}");
            _httpClient = httpClient;
        }

        /// <summary>Requests a payment token from Saman Bank (SEP).</summary>
        public async Task<SamanTokenResponse> RequestTokenAsync(
            string terminalId, long amount, string resNum, string redirectUrl)
        {
            var requestBody = new
            {
                action     = "token",
                TerminalId = terminalId,
                Amount     = amount,
                ResNum     = resNum,
                RedirectUrl = redirectUrl
            };

            var json    = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string rawResponse = null;
            try
            {
                var response = await _httpClient.PostAsync(SamanPaymentDefaults.TokenRequestUrl, content);
                rawResponse = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<SamanTokenResponse>(rawResponse, JsonOptions)
                    ?? new SamanTokenResponse { Status = -99, ErrorDesc = "Empty response from gateway" };

                result.RawResponse = rawResponse;
                return result;
            }
            catch (Exception ex)
            {
                return new SamanTokenResponse
                {
                    Status    = -99,
                    ErrorDesc = ex.Message,
                    RawResponse = rawResponse ?? $"[exception before read: {ex.Message}]"
                };
            }
        }

        /// <summary>Verifies a completed transaction with Saman Bank.</summary>
        public async Task<SamanVerifyResponse> VerifyTransactionAsync(string refNum, string terminalNumber)
        {
            var requestBody = new
            {
                RefNum         = refNum,
                TerminalNumber = terminalNumber
            };

            var json    = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string rawResponse = null;
            try
            {
                var response = await _httpClient.PostAsync(SamanPaymentDefaults.VerifyUrl, content);
                rawResponse = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<SamanVerifyResponse>(rawResponse, JsonOptions)
                    ?? new SamanVerifyResponse { ResultCode = -99, RawResponse = rawResponse };

                result.RawResponse = rawResponse;
                return result;
            }
            catch (Exception ex)
            {
                return new SamanVerifyResponse
                {
                    ResultCode  = -99,
                    Description = ex.Message,
                    RawResponse = rawResponse ?? $"[exception before read: {ex.Message}]"
                };
            }
        }
    }

    public class SamanTokenResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("errorDesc")]
        public string ErrorDesc { get; set; }

        [JsonIgnore]
        public string RawResponse { get; set; }
    }

    public class SamanVerifyResponse
    {
        [JsonPropertyName("ResultCode")]
        public int ResultCode { get; set; }

        [JsonPropertyName("Amount")]
        public long Amount { get; set; }

        [JsonPropertyName("Description")]
        public string Description { get; set; }

        [JsonIgnore]
        public string RawResponse { get; set; }
    }
}
