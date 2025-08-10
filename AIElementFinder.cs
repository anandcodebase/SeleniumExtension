using OpenQA.Selenium;
using System;
using System.Text;

namespace SimpleSeleniumSupport
{
    public static class AIElementFinder
    {
        /// <summary>
        /// Finds a web element using a natural language description, powered by a local AI model.
        /// </summary>
        /// <param name="driver">The IWebDriver instance.</param>
        /// <param name="description">A natural language description of the element (e.g., "the search input field").</param>
        /// <param name="ollamaModel">The Ollama model to use for generating the selector.</param>
        /// <returns>The found IWebElement.</returns>
        public static IWebElement FindElementByAI(this IWebDriver driver, string description, string ollamaModel = "llama3")
        {
            Console.WriteLine($"🤖 Finding element described as '{description}' using AI model '{ollamaModel}'...");

            string pageHtml = driver.PageSource;
            string prompt = BuildPromptForSelector(pageHtml, description);

            try
            {
                string xpath = OllamaClient.Generate(prompt, ollamaModel);

                // Clean the AI's response to ensure it's a valid XPath
                xpath = xpath.Trim().Trim('"', '`', '\'');

                if (string.IsNullOrWhiteSpace(xpath) || (!xpath.StartsWith("/") && !xpath.StartsWith("(")))
                {
                    throw new NoSuchElementException($"AI failed to generate a valid XPath for the description: '{description}'. Response: {xpath}");
                }

                Console.WriteLine($"✅ AI generated XPath: {xpath}");
                return driver.FindElement(By.XPath(xpath));
            }
            catch (Exception ex)
            {
                throw new NoSuchElementException($"Failed to find element for description '{description}'. The AI analysis may have failed or the generated XPath was invalid.", ex);
            }
        }

        private static string BuildPromptForSelector(string pageHtml, string description)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert in web automation and XPath generation.");
            sb.AppendLine("Your task is to analyze the provided HTML and generate a single, robust XPath selector for the element described.");
            sb.AppendLine("Prefer unique attributes like 'id', 'data-testid', or 'name'. Avoid brittle, absolute paths.");
            sb.AppendLine("Respond with ONLY the XPath string and nothing else.");
            sb.AppendLine("\n--- ELEMENT DESCRIPTION ---");
            sb.AppendLine(description);
            sb.AppendLine("\n--- PAGE HTML ---");
            sb.AppendLine("```html");
            sb.AppendLine(pageHtml);
            sb.AppendLine("```");
            sb.AppendLine("\n--- XPATH SELECTOR ---");

            return sb.ToString();
        }
    }
}