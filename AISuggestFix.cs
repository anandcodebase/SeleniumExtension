using OpenQA.Selenium;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace SimpleSeleniumSupport
{
    public static class AISuggestFix
    {
        /// <summary>
        /// The API endpoint for the Ollama service.
        /// This is a proxy for the centralized OllamaClient.ApiUrl.
        /// </summary>
        public static string OllamaApiUrl
        {
            get => OllamaClient.ApiUrl;
            set => OllamaClient.ApiUrl = value;
        }

        /// <summary>
        /// Analyzes a test failure using a local LLM (via Ollama) and suggests a fix.
        /// </summary>
        public static string AnalyzeAndSuggestFix(this IWebDriver driver, Exception exception, [CallerMemberName] string testName = "", string ollamaModel = "llama3")
        {
            Console.WriteLine($"🤖 Analyzing failure in test '{testName}' with AI model '{ollamaModel}'...");

            string stackTrace = exception.ToString();
            string pageHtml = driver.PageSource;
            string pageUrl = driver.Url;

            string prompt = BuildPrompt(stackTrace, pageHtml, pageUrl, testName);

            try
            {
                string suggestion = OllamaClient.Generate(prompt, ollamaModel);
                Console.WriteLine("✅ AI analysis complete.");
                return suggestion;
            }
            catch (Exception ex)
            {
                return $"An unexpected error occurred during AI analysis: {ex.Message}";
            }
        }

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