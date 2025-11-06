using System.Text.Json.Serialization;

namespace QuanLyAnTrua.Models
{
    public class CassoWebhookRequest
    {
        [JsonPropertyName("error")]
        public int Error { get; set; }
        
        [JsonPropertyName("data")]
        public CassoTransaction? Data { get; set; } // Webhook V2
        
        [JsonPropertyName("transactions")]
        public List<CassoTransaction>? Transactions { get; set; } // Webhook cũ (deprecated)
    }
}

