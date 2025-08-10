using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Internal client for handling communication with a local Ollama instance.
    /// </summary>
    internal static class OllamaClient
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// The API endpoint for the Ollama service.
        /// </summary>
        public static string ApiUrl { get; set; } = "http://localhost:11434/api/generate";

        /// <summary>
        /// Sends a prompt to the Ollama API and returns the generated response.
        /// </summary>
        public static string Generate(string prompt, string model)
        {
            try
            {
                var requestPayload = new
                {
                    model,
                    prompt,
                    stream = false
                };

                string jsonPayload = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = httpClient.PostAsync(ApiUrl, content).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Could not reach Ollama API. Status code: {response.StatusCode}");
                }

                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        // Clean the response to return only the core text
                        return responseElement.GetString()?.Trim() ?? string.Empty;
                    }
                }

                throw new InvalidOperationException("Could not parse the suggestion from Ollama's response.");
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Could not connect to Ollama at {ApiUrl}. Is Ollama running? Details: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An unexpected error occurred during AI generation: {ex.Message}", ex);
            }
        }
    }
}