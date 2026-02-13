using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Collections.Concurrent;


namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// AI Element Finder 
    /// </summary>
    public static class AIElementFinder
    {
        // Simple in-memory cache: key = pageUrl + description -> xpath
        /// <summary>
        /// The selector cache
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> _selectorCache = new();

        // Configurable defaults
        /// <summary>
        /// Gets or sets the default retries.
        /// </summary>
        /// <value>
        /// The default retries.
        /// </value>
        public static int DefaultRetries { get; set; } = 2;
        /// <summary>
        /// Gets or sets the default retry delay ms.
        /// </summary>
        /// <value>
        /// The default retry delay ms.
        /// </value>
        public static int DefaultRetryDelayMs { get; set; } = 500;
        /// <summary>
        /// Gets or sets the default wait timeout seconds.
        /// </summary>
        /// <value>
        /// The default wait timeout seconds.
        /// </value>
        public static int DefaultWaitTimeoutSeconds { get; set; } = 15;
        /// <summary>
        /// Gets or sets a value indicating whether [use cache].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [use cache]; otherwise, <c>false</c>.
        /// </value>
        public static bool UseCache { get; set; } = true;

        /// <summary>
        /// Find a single element by a natural language description (immediate).
        /// Throws NoSuchElementException if not found or AI returns invalid result.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="description">The description.</param>
        /// <param name="ollamaModel">The ollama model.</param>
        /// <returns></returns>
        public static IWebElement FindElementByAI(this IWebDriver driver, string description, string ollamaModel = "llama3")
            => FindElementByAI(driver, description, ollamaModel, DefaultRetries);

        /// <summary>
        /// Finds the element by ai.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="description">The description.</param>
        /// <param name="ollamaModel">The ollama model.</param>
        /// <param name="retries">The retries.</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.NoSuchElementException">Failed to find element for description '{description}'. See inner for details.</exception>
        public static IWebElement FindElementByAI(this IWebDriver driver, string description, string ollamaModel, int retries)
        {
            string key = CacheKey(driver, description);
            if (UseCache && _selectorCache.TryGetValue(key, out var cachedXPath))
            {
                try
                {
                    return driver.FindElement(By.XPath(cachedXPath));
                }
                catch
                {
                    // Cache stale - fallthrough to regenerate
                    _selectorCache.TryRemove(key, out _);
                }
            }

            Exception lastException = null;
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    Console.WriteLine($"🤖 AI Find (attempt {attempt + 1}) for '{description}' using model '{ollamaModel}'");
                    string pageHtml = driver.PageSource;
                    string prompt = BuildPromptForSelector(pageHtml, description);

                    string xpath = OllamaClient.Generate(prompt, ollamaModel);
                    xpath = NormalizeXpath(xpath);

                    ValidateXpathOrThrow(xpath, description);

                    var element = driver.FindElement(By.XPath(xpath));

                    if (UseCache)
                        _selectorCache[key] = xpath;

                    Console.WriteLine($"✅ AI generated XPath: {xpath}");
                    return element;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine($"AI selector attempt failed: {ex.Message}");
                    Thread.Sleep(DefaultRetryDelayMs);
                }
            }

            // Final fallback: try a heuristic search by ID/name/data-* tokens found in the description
            try
            {
                var fallback = HeuristicFind(driver, description);
                if (fallback != null) return fallback;
            }
            catch (Exception ex)
            {
                // swallow - will throw below
                Console.WriteLine($"Fallback heuristic failed: {ex.Message}");
            }

            throw new NoSuchElementException($"Failed to find element for description '{description}'. See inner for details.", lastException);
        }

        /// <summary>
        /// Waits up to timeoutInSeconds for AI to return a selector and the element to be present.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="description">The description.</param>
        /// <param name="timeoutInSeconds">The timeout in seconds.</param>
        /// <param name="ollamaModel">The ollama model.</param>
        /// <returns></returns>
        public static IWebElement WaitAndFindByAI(this IWebDriver driver, string description, int timeoutInSeconds = -1, string ollamaModel = "llama3")
        {
            if (timeoutInSeconds <= 0) timeoutInSeconds = DefaultWaitTimeoutSeconds;
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            return wait.Until(d =>
            {
                try
                {
                    var el = FindElementByAI(d, description, ollamaModel, DefaultRetries);
                    return (el != null && el.Displayed) ? el : null;
                }
                catch
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Returns multiple matching elements for a fuzzy description (e.g., "all product titles").
        /// Uses AI to produce an XPath that may match many elements.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="description">The description.</param>
        /// <param name="ollamaModel">The ollama model.</param>
        /// <returns></returns>
        public static IReadOnlyCollection<IWebElement> FindElementsByAI(this IWebDriver driver, string description, string ollamaModel = "llama3")
        {
            string pageHtml = driver.PageSource;
            string prompt = BuildPromptForSelector(pageHtml, description) + "\nNote: If multiple matching elements exist, return an XPath that selects all matching nodes.";

            string xpath = OllamaClient.Generate(prompt, ollamaModel);
            xpath = NormalizeXpath(xpath);

            try
            {
                return driver.FindElements(By.XPath(xpath)).ToList().AsReadOnly();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to run XPath for FindElementsByAI: {ex.Message}");
                return Array.Empty<IWebElement>();
            }
        }

        #region Helpers

        /// <summary>
        /// Caches the key.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="description">The description.</param>
        /// <returns></returns>
        private static string CacheKey(IWebDriver driver, string description)
            => (driver.Url ?? "[unknown_url]") + "|" + description;

        /// <summary>
        /// Normalizes the xpath.
        /// </summary>
        /// <param name="raw">The raw.</param>
        /// <returns></returns>
        public static string NormalizeXpath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            // Remove code fences or surrounding commentary if AI returned them
            raw = raw.Trim().Trim('"', '`', '\'');
            // If AI returns "xpath: //div..." or "XPath = //div", normalize
            var idx = raw.IndexOf("//", StringComparison.Ordinal);
            if (idx >= 0)
                raw = raw.Substring(idx);
            return raw;
        }

        /// <summary>
        /// Validates the xpath or throw.
        /// </summary>
        /// <param name="xpath">The xpath.</param>
        /// <param name="description">The description.</param>
        /// <exception cref="System.InvalidOperationException">AI failed to generate a valid XPath for '{description}'. Response: '{xpath}'</exception>
        private static void ValidateXpathOrThrow(string xpath, string description)
        {
            if (string.IsNullOrWhiteSpace(xpath) || (!xpath.StartsWith("/") && !xpath.StartsWith("(") && !xpath.StartsWith("//")))
            {
                throw new InvalidOperationException($"AI failed to generate a valid XPath for '{description}'. Response: '{xpath}'");
            }
        }

        /// <summary>
        /// Heuristics the find.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="description">The description.</param>
        /// <returns></returns>
        private static IWebElement HeuristicFind(IWebDriver driver, string description)
        {
            // Very small heuristic: look for id="..." or name="..." tokens in the description
            // Example: "login button" -> try //button[contains(translate(.,"ABCDEFGHIJKLMNOPQRSTUVWXYZ","abcdefghijklmnopqrstuvwxyz"), 'login')]
            var tokens = description.Split(new[] { ' ', '\t', '\n', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Where(t => t.Length > 2).Select(t => t.ToLowerInvariant()).ToList();
            if (!tokens.Any()) return null;

            // try searching by text contains for common tags
            var tags = new[] { "button", "a", "label", "span", "div", "input" };
            foreach (var tag in tags)
            {
                var xpath = $"//{tag}[{string.Join(" and ", tokens.Select(t => $"contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{t}')"))}]";
                try
                {
                    var el = driver.FindElement(By.XPath(xpath));
                    if (el != null) return el;
                }
                catch { /* continue */ }
            }

            // try id or name token
            foreach (var t in tokens)
            {
                var byId = $"//*[@id and contains(translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), '{t}')]";
                try
                {
                    var el = driver.FindElement(By.XPath(byId));
                    if (el != null) return el;
                }
                catch { }
            }

            return null;
        }

        private static string BuildPromptForSelector(string pageHtml, string description)
        {
            // Keep prompt concise; the full page HTML is passed but you can later add an option to trim it.
            return $"""
            You are an expert in web automation and XPath generation.
            Your task is to analyze the provided HTML and generate a single, robust XPath selector for the element described.
            /// <summary>
            /// Initializes a new instance of the <see cref="$Program"/> class.
            /// </summary>
            Prefer unique attributes like 'id', 'data-testid', or 'name'. Avoid brittle, absolute paths.
            Respond with ONLY the XPath string and nothing else.

            --- ELEMENT DESCRIPTION ---
            {description}

            --- PAGE HTML ---
            ```html
            {pageHtml}
            ```

            --- XPATH SELECTOR ---
            """;
        }

        #endregion
    }
}
