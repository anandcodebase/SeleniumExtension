using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleSeleniumSupport.Image
{
    public sealed class OllamaVisionProvider : IImageComparisonProvider
    {
        public string Name => "Ollama";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private const string ComparePrompt =
            "You are a visual regression testing assistant. Compare the two provided images. " +
            "The first image is the BASELINE (expected). The second image is the ACTUAL (current). " +
            "Analyze these dimensions: " +
            "1. Layout: Are elements positioned the same way? " +
            "2. Colors: Are colors and gradients consistent? " +
            "3. Text: Is text content and rendering the same? " +
            "4. Elements: Are all UI elements present, sized correctly, and visually identical? " +
            "Return STRICT JSON with this schema: " +
            "{ \"similarity_percent\": <number 0-100>, " +
            "\"layout_match\": <bool>, " +
            "\"color_match\": <bool>, " +
            "\"text_match\": <bool>, " +
            "\"elements_match\": <bool>, " +
            "\"differences\": [\"<brief description of each difference found>\"], " +
            "\"reasoning\": \"<one-paragraph summary>\" }";

        private const string ExtractPrompt =
            "Extract all readable text from this image. " +
            "Return STRICT JSON: { \"text\": \"...\" }";

        public string CompareImages(byte[] expected, byte[] actual, ImageComparisonOptions opt)
        {
            var request = new ChatRequest
            {
                Model = opt.OllamaModel,
                Format = "json",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "user",
                        Content = ComparePrompt,
                        Images = new List<string>
                        {
                            Convert.ToBase64String(expected),
                            Convert.ToBase64String(actual)
                        }
                    }
                }
            };

            string responseJson = Post(opt.OllamaBaseUrl + "/api/chat", request, opt);
            return ParseAiResponse(responseJson);
        }

        public string ExtractText(byte[] image, ImageComparisonOptions opt)
        {
            var request = new ChatRequest
            {
                Model = opt.OllamaModel,
                Format = "json",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "user",
                        Content = ExtractPrompt,
                        Images = new List<string>
                        {
                            Convert.ToBase64String(image)
                        }
                    }
                }
            };

            string responseJson = Post(opt.OllamaBaseUrl + "/api/chat", request, opt);
            return ParseExtractedText(responseJson);
        }

        // ================= RESPONSE PARSING =================

        private static string ParseAiResponse(string rawJson)
        {
            try
            {
                var response = JsonSerializer.Deserialize<ChatResponse>(rawJson);
                return response?.Message?.Content ?? rawJson;
            }
            catch (JsonException)
            {
                return rawJson;
            }
        }

        private static string ParseExtractedText(string rawJson)
        {
            try
            {
                var response = JsonSerializer.Deserialize<ChatResponse>(rawJson);
                string content = response?.Message?.Content ?? "";

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                    return textProp.GetString() ?? "";
            }
            catch (JsonException) { }

            return rawJson;
        }

        // ================= HTTP =================

        private static string Post(string url, ChatRequest payload, ImageComparisonOptions opt)
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

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var resp = client.PostAsync(url,
                new StringContent(json, Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();

            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Ollama returned {(int)resp.StatusCode}: {body}");

            return body;
        }
    }
}
