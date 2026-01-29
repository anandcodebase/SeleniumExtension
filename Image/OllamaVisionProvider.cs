using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI
{
    public sealed class OllamaVisionProvider : IImageComparisonProvider
    {
        public string Name => "Ollama";

        public string CompareImages(byte[] expected, byte[] actual, ImageComparisonOptions opt)
        {
            string content = BuildContent(
                "Compare expected and actual images. Return STRICT JSON: { \"similarity_percent\": number (0..100), \"reasoning\": \"...\" }",
                ("Expected", expected),
                ("Actual", actual));

            return Post(opt.OllamaBaseUrl + "/api/chat", new
            {
                model = opt.OllamaModel,
                format = "json",
                messages = new[] { new { role = "user", content } }
            }, opt);
        }

        public string ExtractText(byte[] image, ImageComparisonOptions opt)
        {
            string content = BuildContent(
                "Extract readable text. Return STRICT JSON: { \"text\": \"...\" }",
                ("Image", image));

            return Post(opt.OllamaBaseUrl + "/api/chat", new
            {
                model = opt.OllamaModel,
                format = "json",
                messages = new[] { new { role = "user", content } }
            }, opt);
        }

        private static string BuildContent(string prompt, params (string label, byte[] img)[] images)
        {
            var sb = new StringBuilder(prompt).AppendLine();
            foreach (var (label, img) in images)
            {
                sb.AppendLine($"--- {label} ---");
                sb.AppendLine($"data:image/jpeg;base64,{Convert.ToBase64String(img)}");
            }
            return sb.ToString();
        }

        private static string Post(string url, object payload, ImageComparisonOptions opt)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds)
            };

            if (!string.IsNullOrWhiteSpace(opt.OllamaApiKey))
            {
                var value = string.IsNullOrWhiteSpace(opt.OllamaAuthScheme)
                    ? opt.OllamaApiKey
                    : $"{opt.OllamaAuthScheme} {opt.OllamaApiKey}";

                client.DefaultRequestHeaders.Add(opt.OllamaAuthHeader, value);
            }

            var json = JsonSerializer.Serialize(payload);
            var resp = client.PostAsync(url,
                new StringContent(json, Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();

            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(body);

            return body;
        }
    }
}
