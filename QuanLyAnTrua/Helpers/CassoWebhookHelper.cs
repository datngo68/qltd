using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace QuanLyAnTrua.Helpers
{
    public static class CassoWebhookHelper
    {
        /// <summary>
        /// Xác thực chữ ký số từ header X-Casso-Signature (Webhook V2)
        /// Theo hướng dẫn của Casso: https://developers.casso.vn/docs/webhook-v2
        /// </summary>
        public static bool VerifyWebhookSignature(string receivedSignature, object webhookData, string checksumKey)
        {
            try
            {
                // Bước 1 & 2: Parse header: t=<timestamp>,v1=<signature>
                var match = Regex.Match(receivedSignature, @"t=(\d+),v1=([a-f0-9]+)");
                if (!match.Success)
                    return false;

                var timestampStr = match.Groups[1].Value;
                var signature = match.Groups[2].Value;

                if (!long.TryParse(timestampStr, out long timestamp))
                    return false;

                // Bước 3 & 4: Chuyển đổi object thành Dictionary và sort theo key
                var dict = ConvertToDictionary(webhookData);
                if (dict == null)
                    return false;

                // Bước 3: Sort dictionary theo key (recursive cho nested objects)
                var sortedData = SortObjDataByKey(dict);

                // Bước 4: Chuyển dữ liệu đã sort về dạng JSON string (sử dụng Newtonsoft.Json như code mẫu)
                var jsonString = JsonConvert.SerializeObject(sortedData);

                // Bước 5: Tạo message: timestamp + "." + JSON string
                var messageToSign = timestamp + "." + jsonString;

                // Bước 6: Tạo chữ ký số với HMAC-SHA512
                var generatedSignature = CreateHmacSHA512(checksumKey, messageToSign);

                // Bước 7: So sánh chữ ký số (case-sensitive như code mẫu)
                return string.Equals(signature, generatedSignature, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Chuyển đổi object thành Dictionary<string, object> để xử lý
        /// </summary>
        private static Dictionary<string, object>? ConvertToDictionary(object? data)
        {
            if (data == null)
                return null;

            // Nếu đã là Dictionary<string, object>
            if (data is Dictionary<string, object> dict)
                return dict;

            // Nếu là JsonObject (System.Text.Json)
            if (data is JsonObject jsonObject)
            {
                var result = new Dictionary<string, object>();
                foreach (var item in jsonObject)
                {
                    result[item.Key] = ConvertValue(item.Value);
                }
                return result;
            }

            // Nếu là JsonElement (System.Text.Json)
            if (data is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var result = new Dictionary<string, object>();
                    foreach (var prop in jsonElement.EnumerateObject())
                    {
                        result[prop.Name] = ConvertJsonElement(prop.Value);
                    }
                    return result;
                }
            }

            // Thử deserialize từ JSON string
            try
            {
                var jsonString = JsonConvert.SerializeObject(data);
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Chuyển đổi JsonNode thành object
        /// </summary>
        private static object ConvertValue(JsonNode? node)
        {
            if (node == null)
                return new object();

            if (node is JsonObject jsonObj)
            {
                var dict = new Dictionary<string, object>();
                foreach (var item in jsonObj)
                {
                    dict[item.Key] = ConvertValue(item.Value);
                }
                return dict;
            }

            if (node is JsonArray jsonArray)
            {
                return jsonArray.Select(ConvertValue).ToList();
            }

            return node.GetValue<object>() ?? new object();
        }

        /// <summary>
        /// Chuyển đổi JsonElement thành object
        /// </summary>
        private static object ConvertJsonElement(System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = ConvertJsonElement(prop.Value);
                    }
                    return dict;

                case System.Text.Json.JsonValueKind.Array:
                    return element.EnumerateArray().Select(ConvertJsonElement).ToList();

                case System.Text.Json.JsonValueKind.String:
                    return element.GetString() ?? string.Empty;

                case System.Text.Json.JsonValueKind.Number:
                    if (element.TryGetInt64(out var longValue))
                        return longValue;
                    if (element.TryGetDecimal(out var decimalValue))
                        return decimalValue;
                    return element.GetDouble();

                case System.Text.Json.JsonValueKind.True:
                    return true;

                case System.Text.Json.JsonValueKind.False:
                    return false;

                case System.Text.Json.JsonValueKind.Null:
                    return new object();

                default:
                    return new object();
            }
        }

        /// <summary>
        /// Sort dictionary theo key (recursive cho nested objects)
        /// Giống hệt code mẫu của Casso
        /// </summary>
        private static Dictionary<string, object> SortObjDataByKey(Dictionary<string, object> data)
        {
            var sortedDict = new Dictionary<string, object>();
            foreach (var item in data.OrderBy(x => x.Key))
            {
                if (item.Value is Dictionary<string, object> nestedDict)
                {
                    sortedDict[item.Key] = SortObjDataByKey(nestedDict);
                }
                else if (item.Value is List<object> list)
                {
                    // Xử lý array/list
                    sortedDict[item.Key] = list.Select(x =>
                    {
                        if (x is Dictionary<string, object> dictItem)
                            return SortObjDataByKey(dictItem);
                        return x;
                    }).ToList();
                }
                else
                {
                    sortedDict[item.Key] = item.Value;
                }
            }
            return sortedDict;
        }

        /// <summary>
        /// Tạo HMAC SHA512 signature
        /// Giống hệt code mẫu của Casso
        /// </summary>
        private static string CreateHmacSHA512(string key, string message)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Xác thực secure-token (Webhook cũ)
        /// </summary>
        public static bool VerifySecureToken(string receivedToken, string expectedToken)
        {
            return string.Equals(receivedToken, expectedToken, StringComparison.Ordinal);
        }
    }
}

