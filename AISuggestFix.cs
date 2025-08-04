using OpenQA.Selenium;
using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// A static class to hold extension methods for Selenium's IWebDriver.
/// </summary>
namespace SimpleSeleniumSupport
{
    public static class AISuggestFix
    {
        // Configuration for the Ollama service
        private static readonly HttpClient httpClient = new HttpClient();
        private const string OllamaApiUrl = "http://localhost:11434/api/generate";
        private const string OllamaModel = "llama3"; // Change this to your preferred model


        public static string AnalyzeAndSuggestFix(this IWebDriver driver, Exception exception, [CallerMemberName] string testName = "")
        {
            Console.WriteLine($"🤖 Analyzing failure in test '{testName}' with AI (Sync)...");

            // 1. Capture context from the failed test
            string stackTrace = exception.ToString();
            string pageHtml = driver.PageSource;
            string pageUrl = driver.Url;

            // 2. Build the prompt for the AI model
            string prompt = BuildPrompt(stackTrace, pageHtml, pageUrl, testName);

            // 3. Send the context to the local Ollama instance synchronously
            try
            {
                var requestPayload = new
                {
                    model = OllamaModel,
                    prompt = prompt,
                    stream = false
                };

                string jsonPayload = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // This is a blocking call. It waits for the task to complete.
                HttpResponseMessage response = httpClient.PostAsync(OllamaApiUrl, content).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error: Could not reach Ollama API. Status code: {response.StatusCode}";
                }

                // This is also a blocking call.
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // 4. Deserialize the response and extract the suggestion
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("response", out JsonElement responseElement))
                    {
                        string suggestion = responseElement.GetString() ?? "No suggestion provided.";
                        Console.WriteLine("✅ AI analysis complete.");
                        return suggestion;
                    }
                }

                return "Error: Could not parse the suggestion from Ollama's response.";
            }
            catch (HttpRequestException ex)
            {
                return $"Error: Could not connect to Ollama at {OllamaApiUrl}. Is Ollama running?\nDetails: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"An unexpected error occurred during AI analysis: {ex.Message}";
            }
        }

        /// <summary>
        /// Helper method to construct a well-structured prompt for the language model.
        /// </summary>
        private static string BuildPrompt(string stackTrace, string pageHtml, string pageUrl, string testName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert Selenium C# test automation engineer. A test has failed.");
            sb.AppendLine("Your task is to analyze the following context: the failing test name, the URL at the time of failure, the exception stack trace, and the page's full HTML source.");
            sb.AppendLine("Based on all this context, provide the most likely cause for the failure and a suggested code fix in C#.");
            sb.AppendLine("\n--- START OF CONTEXT ---");
            sb.AppendLine($"\n## 1. Test Name: {testName}");
            sb.AppendLine($"\n## 2. URL at Failure: {pageUrl}");
            sb.AppendLine("\n## 3. Exception Details:");
            sb.AppendLine("```");
            sb.AppendLine(stackTrace);
            sb.AppendLine("```");
            sb.AppendLine("\n## 4. Full Page HTML Source at Time of Failure:");
            sb.AppendLine("```html");
            sb.AppendLine(pageHtml);
            sb.AppendLine("```");
            sb.AppendLine("\n--- END OF CONTEXT ---");
            sb.AppendLine("\n## Analysis and Suggested Fix:");
            sb.AppendLine("Please provide your analysis and the corrected C# code snippet.");

            return sb.ToString();
        }
    }
}
