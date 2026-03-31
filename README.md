# SimpleSeleniumSupport

An advanced Selenium WebDriver toolkit for .NET 8+ that adds AI-powered element finding, network monitoring, visual regression testing, Playwright-style selectors, OCR, performance budgets, notifications, analytics, video recording, and rich reporting — all as extension methods on your existing `IWebDriver`.

[![NuGet](https://img.shields.io/nuget/v/SimpleSeleniumSupport)](https://www.nuget.org/packages/SimpleSeleniumSupport)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Table of Contents

1. [Installation](#installation)
2. [Global Configuration](#global-configuration)
3. [AI Element Finding](#ai-element-finding)
4. [Multi-Provider AI Support](#multi-provider-ai-support)
5. [AI Failure Analysis](#ai-failure-analysis)
6. [AI Prompt Sanitization](#ai-prompt-sanitization)
7. [Network Capture & Monitoring](#network-capture--monitoring)
8. [Network Request Routing](#network-request-routing)
9. [Playwright-Style Selectors](#playwright-style-selectors)
10. [Locator API](#locator-api)
11. [FrameLocator (iframe support)](#framelocator-iframe-support)
12. [Playwright-Style Assertions](#playwright-style-assertions)
13. [SeleniumPage](#seleniumpage)
14. [Mouse & Keyboard Input](#mouse--keyboard-input)
15. [Resilient Selector Engine](#resilient-selector-engine)
16. [Visual Regression Testing](#visual-regression-testing)
17. [OCR Text Extraction](#ocr-text-extraction)
18. [Web Performance Monitoring](#web-performance-monitoring)
19. [Screenshot Annotation](#screenshot-annotation)
20. [Accessibility Auditing](#accessibility-auditing)
21. [Video Recording](#video-recording)
22. [Failure Diagnostics](#failure-diagnostics)
23. [Network Reporting](#network-reporting)
24. [Test Run Report](#test-run-report)
25. [Parallel Test Reporting (TestResultCollector)](#parallel-test-reporting-testresultcollector)
26. [CI / Format Export](#ci--format-export)
27. [Notifications](#notifications)
28. [Analytics & Flakiness Detection](#analytics--flakiness-detection)
29. [Consolidated Report Builder](#consolidated-report-builder)
30. [Structured Logging](#structured-logging)
31. [Requirements](#requirements)
32. [Full Example — NUnit Test Class](#full-example--nunit-test-class)

---

## Installation

```
dotnet add package SimpleSeleniumSupport
```

Or via Package Manager Console:

```
Install-Package SimpleSeleniumSupport
```

**Required usings** (add once to your test project):

```csharp
using SimpleSeleniumSupport;
using SimpleSeleniumSupport.AI;
using SimpleSeleniumSupport.AI.Sanitization;
using SimpleSeleniumSupport.Assertions;
using SimpleSeleniumSupport.Input;
using SimpleSeleniumSupport.Network;
using SimpleSeleniumSupport.Image;
using SimpleSeleniumSupport.OCR;
using SimpleSeleniumSupport.Page;
using SimpleSeleniumSupport.Performance;
using SimpleSeleniumSupport.Selectors;
using SimpleSeleniumSupport.Selectors.Engine;
using SimpleSeleniumSupport.Accessibility;
using SimpleSeleniumSupport.Recording;
using SimpleSeleniumSupport.Reporting;
using SimpleSeleniumSupport.Diagnostics;
using SimpleSeleniumSupport.Notifications;
using SimpleSeleniumSupport.Analytics;
using SimpleSeleniumSupport.CiExport;
```

---

## Global Configuration

Set defaults once at test-suite startup (e.g. `[OneTimeSetUp]`, `ClassInitialize`, or a fixture constructor). Every library call respects these values when no explicit argument is provided.

```csharp
[OneTimeSetUp]
public static void Configure()
{
    // ── Ollama / AI ──────────────────────────────────────────
    SimpleSeleniumSupportDefaults.OllamaBaseUrl   = "http://localhost:11434/api/generate";
    SimpleSeleniumSupportDefaults.OllamaModel     = "llama3";       // any model pulled via 'ollama pull'
    SimpleSeleniumSupportDefaults.OllamaTimeoutMs = 60_000;         // 60 s per AI call
    SimpleSeleniumSupportDefaults.AiRetries       = 3;              // retry AI selector generation
    SimpleSeleniumSupportDefaults.AiSanitizeByDefault = true;       // scrub sensitive data before AI calls

    // ── Timeouts ─────────────────────────────────────────────
    SimpleSeleniumSupportDefaults.WaitTimeoutSeconds        = 15;   // WaitAndFindByAI
    SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds     = 10;   // Locator actions (Click, Fill, …)
    SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds   = 5;    // WebExpect.That(…) auto-retry
    SimpleSeleniumSupportDefaults.NetworkWaitTimeoutSeconds = 30;   // WaitForRequest

    // ── Visual regression ─────────────────────────────────────
    SimpleSeleniumSupportDefaults.VisualThreshold = 97.0;           // % similarity to pass

    // ── OCR ───────────────────────────────────────────────────
    SimpleSeleniumSupportDefaults.OcrTessdataPath = "tessdata";     // path to tessdata folder
    SimpleSeleniumSupportDefaults.OcrLanguage     = "eng";          // Tesseract language pack

    // ── Video recording ───────────────────────────────────────
    SimpleSeleniumSupportDefaults.VideoRecordingFfmpegPath      = "ffmpeg";       // or full path to ffmpeg.exe
    SimpleSeleniumSupportDefaults.VideoRecordingOutputDirectory = "Recordings";
    SimpleSeleniumSupportDefaults.VideoRecordingFrameRate       = 5;              // fps; 5 is reliable in CI

    // ── Screenshot annotation ─────────────────────────────────
    SimpleSeleniumSupportDefaults.AnnotateScreenshotsOnFailure = true;            // red banner on failure screenshots

    // ── Failure analysis (post-run batch AI classifier) ────────
    SimpleSeleniumSupportDefaults.FailureAnalysisGrouping           = FailureGroupingStrategy.PerClass;
    SimpleSeleniumSupportDefaults.FailureAnalysisMaxStackTraceChars = 1500;       // chars of stack per test in prompt
    SimpleSeleniumSupportDefaults.FailureAnalysisMaxGroupPromptChars= 8000;       // total prompt budget per group
    SimpleSeleniumSupportDefaults.FailureAnalysisIncludeScreenshot  = true;       // reference screenshot path in prompt
    SimpleSeleniumSupportDefaults.FailureAnalysisMaxParallelism     = 3;          // concurrent AI group calls

    // ── Analytics ─────────────────────────────────────────────
    SimpleSeleniumSupportDefaults.AnalyticsEnabled          = true;
    SimpleSeleniumSupportDefaults.AnalyticsHistoryDirectory = "TestReports/history";
    SimpleSeleniumSupportDefaults.AnalyticsMaxRunsKept      = 50;

    // ── Logging ───────────────────────────────────────────────
    SimpleSeleniumSupportDefaults.LoggerFactory = LoggerFactory.Create(b =>
        b.AddConsole().SetMinimumLevel(LogLevel.Debug));
}
```

---

## AI Element Finding

Requires a running [Ollama](https://ollama.com) instance (or configure another AI provider — see [Multi-Provider AI Support](#multi-provider-ai-support)). Uses the page DOM to generate XPath selectors via a local LLM.

### Basic find

```csharp
// Find using a natural-language description
IWebElement btn = driver.FindElementByAI("the blue Submit button");
btn.Click();

// Find with a specific model
IWebElement field = driver.FindElementByAI("username input field", ollamaModel: "llama3.2");
field.SendKeys("testuser");
```

### Wait until visible

```csharp
// Polls until the AI-located element is displayed (up to 20 s)
IWebElement modal = driver.WaitAndFindByAI("confirmation modal dialog", timeoutInSeconds: 20);
Assert.IsTrue(modal.Displayed);
```

### Async variants (xUnit / NUnit 3)

```csharp
// Avoids blocking the async test thread
var el = await driver.FindElementByAIAsync("password field");
await driver.WaitAndFindByAIAsync("loading spinner has disappeared", timeoutInSeconds: 15);
```

### Find multiple elements

```csharp
// Returns all matching elements for a fuzzy description
var rows = driver.FindElementsByAI("all product rows in the results table");
Console.WriteLine($"Found {rows.Count} products");
```

### Configure caching and error routing

```csharp
// Disable selector caching (useful when DOM changes between runs)
AIElementFinder.UseCache = false;

// Route internal errors to your logger
AIElementFinder.OnError = (ex, context) =>
    TestContext.WriteLine($"[AI] {context}: {ex.Message}");
```

### Mock mode (no Ollama required)

```csharp
// In CI where Ollama is unavailable — returns a deterministic mock response
OllamaClient.MockMode = true;
OllamaClient.MockResponse = "//button[@id='submit']";
```

---

## Multi-Provider AI Support

Switch the AI backend from Ollama to OpenAI, Azure OpenAI, Anthropic (Claude), or Google Gemini — all through the same `AIProviderRegistry`. Register once at suite startup; all AI calls (`FindElementByAI`, `AnalyzeAndSuggestFix`, etc.) use the active provider automatically.

### Use OpenAI (GPT-4o)

```csharp
AIProviderRegistry.Use("openai", new AIProviderConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_KEY"),
    Model  = "gpt-4o-mini"   // default if omitted: gpt-4o-mini
});
```

### Use Azure OpenAI

```csharp
AIProviderRegistry.Use("azure-openai", new AIProviderConfig
{
    ApiKey       = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"),
    BaseUrl      = "https://my-resource.openai.azure.com/openai/deployments/my-deploy/chat/completions",
    ApiVersion   = "2024-02-01",
    DeploymentId = "my-deploy"
});
```

### Use Anthropic (Claude)

```csharp
AIProviderRegistry.Use("anthropic", new AIProviderConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_KEY"),
    Model  = "claude-3-haiku-20240307"
});
```

### Use Google Gemini

```csharp
AIProviderRegistry.Use("gemini", new AIProviderConfig
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_KEY"),
    Model  = "gemini-1.5-flash"
});
```

### Register a custom provider

```csharp
// Implement IAIProvider for any backend not listed above
AIProviderRegistry.Register(new MyCustomAIProvider());

// Read the currently active provider
IAIProvider current = AIProviderRegistry.Current;
```

### AIProviderConfig reference

| Property | Description |
|----------|-------------|
| `ApiKey` | API key for authentication |
| `BaseUrl` | Override the default endpoint URL |
| `Model` | Default model for the provider |
| `TimeoutMs` | HTTP timeout (default: `SimpleSeleniumSupportDefaults.OllamaTimeoutMs`) |
| `ApiVersion` | Azure OpenAI API version (e.g. `"2024-02-01"`) |
| `DeploymentId` | Azure OpenAI deployment name |

---

## AI Failure Analysis

Two complementary AI analysis tools: **`AnalyzeAndSuggestFix`** (inline, per-test, while the browser is still open) and **`TestFailureAnalyzer`** (post-run batch classifier that determines whether failures are test bugs or product bugs).

---

### Per-test analysis — `AnalyzeAndSuggestFix`

Call in `[TearDown]` while the browser is still open. Saves diagnostics automatically (screenshot, HTML, network) and returns a free-text AI suggestion.

```csharp
[TearDown]
public void TearDown()
{
    if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
    {
        var suggestion = driver.AnalyzeAndSuggestFix(
            ex:           lastException,
            testName:     TestContext.CurrentContext.Test.Name,
            capture:      networkCapture,     // optional — attaches HAR to diagnosis
            consoleLogs:  consoleLogs,        // optional — browser console log entries
            ollamaModel:  "llama3"
        );

        Console.WriteLine("=== AI Suggestion ===");
        Console.WriteLine(suggestion);
        // Artifacts saved to: Diagnostics/failure_<timestamp>/
    }
}
```

The response includes:
- 2 probable causes for the failure
- 2 concrete code-level fixes (with XPath/CSS examples)
- An `XPATH_CANDIDATE:` line validated immediately against the live page

#### Tune DOM trimming

```csharp
AISuggestFixConfig.UseTrimmedDom = true;   // default
AISuggestFixConfig.TrimMaxNodes  = 300;    // reduce for faster AI calls
AISuggestFixConfig.TrimMaxChars  = 8_000;
```

---

### Post-run failure classifier — `TestFailureAnalyzer`

After all tests finish, `TestFailureAnalyzer` groups the failed tests, sends them to AI in batches, and classifies each failure as:

| Classification | Meaning |
|----------------|---------|
| `TestIssue` | Bug in the test code, selector, assertion logic, or test data |
| `ProductIssue` | Genuine defect in the application under test |
| `Flaky` | Intermittent failure — timing, race conditions, or retry exhaustion |
| `Infrastructure` | CI/CD, browser driver version, network, or missing dependency |
| `Uncertain` | Insufficient information to classify |

Results are written back to `TestResult.AiClassification`, `TestResult.AiConfidence`, and `TestResult.AiAnalysis`, so they appear **automatically** in every exported HTML and Excel report — no extra export call needed.

#### Classify via `TestResultCollector` (recommended)

```csharp
[OneTimeTearDown]
public static async Task ExportReports()
{
    // Analyze failures first — mutates AiClassification on each failing TestResult
    await Collector.AnalyzeFailuresAsync(new FailureAnalysisOptions
    {
        Grouping    = FailureGroupingStrategy.PerClass,   // group failures by test class
        AiModel     = null,                               // null = provider default
        Sanitization = new SanitizationOptions            // scrub passwords, tokens, paths
        {
            RedactPasswords = true,
            RedactEmails    = true,
            RedactApiTokens = true,
            RedactFilePaths = true
        }
    });

    // Then export — AI columns are already populated
    Collector.ExportSingleFile("TestReports", "My Suite");
    Collector.ExportToExcel("TestReports", "My Suite");
}
```

#### Grouping strategies — token economics

| Strategy | AI calls (100 failures, 10 classes) | Token cost | Best for |
|----------|--------------------------------------|------------|----------|
| `PerTest` | 100 | Highest | Small suites needing maximum precision |
| `PerClass` | 10 | Medium | **Default** — good balance |
| `PerCategory` | 3–5 | Low | Category-level triage |
| `PerBrowser` | 2–4 | Low | Cross-browser comparison |
| `AllTogether` | 1 | Lowest | Quick bulk triage |

#### Standalone usage (without `TestResultCollector`)

```csharp
var analyzer = new TestFailureAnalyzer(new FailureAnalysisOptions
{
    Grouping       = FailureGroupingStrategy.PerCategory,
    MaxParallelism = 5                                   // 5 concurrent AI calls
});

IReadOnlyList<FailureAnalysisResult> results =
    await analyzer.AnalyzeAsync(myTestResults);

foreach (var r in results)
{
    Console.WriteLine($"{r.TestName}: {r.Classification} ({r.Confidence})");
    Console.WriteLine($"  Reason: {r.Reasoning}");
    Console.WriteLine($"  Fix:    {r.SuggestedFix}");
}
```

#### `FailureAnalysisResult` properties

| Property | Type | Description |
|----------|------|-------------|
| `TestName` | `string` | Matches `TestResult.TestName` |
| `Classification` | `FailureClassification` | Root cause category |
| `Confidence` | `AnalysisConfidence` | `High` / `Medium` / `Low` |
| `Reasoning` | `string` | AI explanation paragraph |
| `SuggestedFix` | `string` | One actionable sentence |
| `RawResponse` | `string` | Full unparsed AI response |
| `GroupKey` | `string?` | Batch group the test belonged to |

#### `FailureAnalysisOptions` reference

| Property | Default | Description |
|----------|---------|-------------|
| `Grouping` | `PerClass` | How to batch failures before calling AI |
| `AiModel` | `null` (provider default) | Model name override |
| `IncludeScreenshot` | `true` | Include screenshot filename in prompt |
| `UseVisionModel` | `false` | Embed screenshot as base64 (vision models only) |
| `MaxStackTraceCharsPerTest` | `1500` | Stack trace budget per test |
| `MaxGroupPromptChars` | `8000` | Total prompt character budget per group |
| `MaxParallelism` | `3` | Concurrent AI calls |
| `Sanitization` | all redactions on | Set `null` to disable |
| `MutateTestResults` | `true` | Write results back to `TestResult` fields |

#### HTML report — AI column and filter

After running `AnalyzeFailuresAsync()`, every failed row in the HTML report shows:

- **AI column** in the grid — colour-coded badge: ![red](https://img.shields.io/badge/-Product%20Issue-fee2e2) ![yellow](https://img.shields.io/badge/-Test%20Issue-fef9c3) ![orange](https://img.shields.io/badge/-Flaky-ffedd5) ![purple](https://img.shields.io/badge/-Infrastructure-f3e8ff) ![grey](https://img.shields.io/badge/-Uncertain-f1f5f9) with a confidence dot (● High / ◑ Medium / ○ Low)
- **AI Classification filter** dropdown in the filter bar — click to show only `ProductIssue` failures
- **AI Analysis tab** in the row detail modal — classification badge at the top, then reasoning and suggested fix

#### Excel report — AI columns

Two new columns appear after "AI Analysis":

| Column | Content | Colour coding |
|--------|---------|---------------|
| **AI Classification** | `ProductIssue` / `TestIssue` / `Flaky` / `Infrastructure` / `Uncertain` | Rose / Yellow / Orange / Lavender / Grey |
| **AI Confidence** | `High` / `Medium` / `Low` | Plain text |

---

## AI Prompt Sanitization

`PromptSanitizer` automatically scrubs passwords, usernames, email addresses, API tokens, Bearer tokens, and file-system paths from AI prompts and DOM snapshots before they leave the process. Enable globally or call manually.

### Enable globally

```csharp
// All AI calls will auto-sanitize — opt-in per fixture is also possible
SimpleSeleniumSupportDefaults.AiSanitizeByDefault = true;
```

### Sanitize a string manually

```csharp
using SimpleSeleniumSupport.AI.Sanitization;

string raw = "user=john@example.com password=s3cr3t Bearer eyJhbGci... path=C:\\Users\\john\\";
string clean = PromptSanitizer.Sanitize(raw);
// → "user=[REDACTED_EMAIL] password=[REDACTED_PASSWORD] Bearer [REDACTED_TOKEN] path=[REDACTED_PATH]\"
```

### Custom redaction rules

```csharp
var opts = new SanitizationOptions
{
    RedactEmails    = true,
    RedactPasswords = true,
    RedactApiTokens = true,
    RedactFilePaths = false,    // keep paths for debugging
    PasswordToken   = "***"     // custom replacement token
};
// Add a custom regex rule
opts.CustomRules.Add((new Regex(@"SSN-\d{9}"), "[REDACTED_SSN]"));

string clean = PromptSanitizer.Sanitize(rawPrompt, opts);
```

### Sanitize DOM JSON

```csharp
// Blanks the 'text' of password input nodes in DOM snapshots from DomTrimmer
string cleanDom = PromptSanitizer.SanitizeDomJson(domJson);
```

---

## Network Capture & Monitoring

Wraps Selenium DevTools to capture every request and response. Works with `ChromeDriver` and `EdgeDriver`.

### Start / stop monitoring

```csharp
var capture = new Capture(driver);
capture.StartMonitoring();

driver.Navigate().GoToUrl("https://example.com/login");
driver.FindElement(By.Id("username")).SendKeys("user");
driver.FindElement(By.Id("password")).SendKeys("pass");
driver.FindElement(By.Id("login")).Click();

capture.StopMonitoring();

// Get all traffic as a flat list
var traffic = capture.GetCombinedNetworkInfo();
```

### Wait for a specific request

```csharp
// Wait up to 30 s for a request whose URL contains "/api/login"
var loginCall = capture.WaitForRequest("/api/login");
Assert.AreEqual(200, loginCall.ResponseStatusCode);
Console.WriteLine(loginCall.ResponseBody);
```

### Wait with a custom predicate

```csharp
// Wait for a POST to /api/orders that returned 201
var orderCall = capture.WaitForRequest(
    info => info.RequestMethod == "POST"
         && info.RequestUrl?.Contains("/api/orders") == true
         && info.ResponseStatusCode == 201,
    timeoutSeconds: 20);

Console.WriteLine($"Order ID in response: {orderCall.ResponseBody}");
```

### Clear between tests

```csharp
// Restart monitoring and discard previous captures
capture.StartMonitoring(clearPrevious: true);
```

### IDisposable support

```csharp
using var capture = new Capture(driver);
capture.StartMonitoring();
// ... test actions ...
// StopMonitoring called automatically on dispose
```

### Export captured traffic

```csharp
var traffic = capture.GetCombinedNetworkInfo();

// Excel .xlsx
ExcelExporter.ExportNetworkInfoToExcel(traffic, "output/network.xlsx");

// HAR 1.2 (importable in Chrome DevTools, Charles Proxy, etc.)
HarExporter.ExportToHar(traffic, "output/network.har");

// Interactive HTML report (single file)
HtmlExporterSingleWithGridjs.ExportNetworkInfoToSingleHtml(
    traffic, outFolderRoot: "Reports", reportName: "Login Flow");

// Interactive HTML report (chunked — better for large captures)
HtmlExporterChunkedWithGridjs.ExportNetworkInfoToChunkedHtml(
    traffic, outFolderRoot: "Reports", pageSize: 500);
```

---

## Network Request Routing

`NetworkRouter` provides Playwright-style request interception — mock API responses, block resources, or proxy requests with header/body overrides. Works with Chrome and Edge (DevTools required).

### Start routing via extension method

```csharp
using SimpleSeleniumSupport.Network;

// Creates and starts a router in one call — dispose to stop
using var router = driver.StartRouting();
```

### Mock an API endpoint

```csharp
router.Route("**/api/users", route =>
    route.Fulfill(
        statusCode:  200,
        body:        """[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]""",
        contentType: "application/json"));

driver.Navigate().GoToUrl("https://myapp.com/users");
// The page receives the mocked JSON instead of hitting the real API
```

### Abort requests (block images, ads, trackers)

```csharp
// Block all PNG images
router.Route("**/*.png", route => route.Abort());

// Block third-party analytics
router.Route("**/analytics/**", route => route.Abort());
```

### Modify a request before forwarding (Continue with overrides)

```csharp
router.Route("**/api/**", route =>
    route.Continue(
        headers: new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer test-token",
            ["X-Test-Mode"]   = "1"
        }));
```

### Wait for a request

```csharp
// Run an action and wait until a matching network request is made
var loginCall = router.WaitForRequest("**/api/login", () =>
    driver.GetByRole("button", "Sign in").Click());

Assert.AreEqual(200, loginCall.ResponseStatusCode);
```

### Wait for a response

```csharp
var orderResponse = router.WaitForResponse("**/api/orders", () =>
    driver.GetByRole("button", "Place order").Click());

Assert.AreEqual(201, orderResponse.ResponseStatusCode);
```

### One-liner wait (no explicit router needed)

```csharp
// Driver extension — router is created, started, and disposed automatically
var info = driver.WaitForRequest("**/api/search",
    () => driver.GetByRole("button", "Search").Click());

Console.WriteLine($"Search URL: {info.RequestUrl}");
```

### Remove a route

```csharp
// Unregister a specific pattern; remaining routes stay active
router.RemoveRoute("**/*.png");
```

### RoutePattern syntax

| Pattern | Matches |
|---------|---------|
| `**/api/users` | any URL ending with `/api/users` |
| `**/*.png` | any URL whose path ends with `.png` |
| `https://example.com/**` | any URL under `example.com` |
| `^https://api\\.example\\.com/` | raw regex (starts with `^`) |

### Route decision methods

| Method | Effect |
|--------|--------|
| `route.Fulfill(statusCode, body, contentType, headers)` | Return a mocked response to the browser |
| `route.Abort()` | Cancel the request — browser sees a network error |
| `route.Continue(url?, method?, headers?, postData?)` | Forward with optional overrides |

---

## Playwright-Style Selectors

All selectors support a `LocatorOptions` parameter for timeout, polling, and wait strategy.

### LocatorOptions

```csharp
var opts = new LocatorOptions
{
    TimeoutSeconds = 15,
    PollingMs      = 100,
    Wait           = WaitUntil.Visible,   // Exists | Visible | Enabled | Clickable
    ExactMatch     = true,
    CaseSensitive  = false
};
```

### GetByRole (ARIA roles)

```csharp
// Get a button by its accessible role and name
var submitBtn = driver.GetByRole("button", "Submit");
submitBtn.Click();

// Try-get variant — returns null instead of throwing
var maybeDialog = driver.TryGetByRole("dialog");

// Boolean out-param variant
if (driver.TryGetByRole("alert", out var alert))
    Console.WriteLine(alert.Text);

// Tuple variant
var (found, el) = driver.TryGetByRoleTuple("progressbar");
if (found) Assert.IsTrue(el!.Displayed);

// Get all matching elements
var buttons = driver.GetAllByRole("button");
var links   = driver.TryGetAllByRole("link");

// With full options
var nav = driver.GetByRole("navigation", options: new LocatorOptions { TimeoutSeconds = 5 });
```

### GetByText

```csharp
// Find an element by its visible text (exact match by default)
var heading = driver.GetByText("Welcome back");

// Contains match
var anyBtn = driver.GetByText("Continue", new LocatorOptions { ExactMatch = false });

// Try variants
var el = driver.TryGetByText("Sign in");
bool ok = driver.TryGetByText("Accept", out var acceptBtn);
var (f, cookieEl) = driver.TryGetByTextTuple("Accept cookies");
```

### GetByLabel, GetByPlaceholder, GetByAlt, GetByTitle

```csharp
// Find form controls by their associated label text
var emailField = driver.GetByLabel("Email address");
emailField.SendKeys("user@example.com");

// Find by placeholder attribute
var search = driver.GetByPlaceholder("Search products…");
search.SendKeys("laptop");

// Find an image by its alt text
var logo = driver.GetByAlt("Company logo");

// Find by title attribute
var helpIcon = driver.GetByTitle("Help");
```

### GetByTestId

```csharp
// Matches data-testid, data-test-id, or data-test attributes
var loginForm = driver.GetByTestId("login-form");
driver.GetByTestId("submit-button").Click();
```

### By factory (use as Selenium By)

```csharp
// Use ByEx to get a standard Selenium By object (composable with FindElement/FindElements)
var by = ByEx.Role("button", "Save");
driver.FindElement(by).Click();

var byText   = ByEx.Text("Confirm");
var byTestId = ByEx.TestId("modal-close");
```

### Scoped search within an element

```csharp
// All selectors work on IWebElement to scope the search
var form = driver.FindElement(By.Id("login-form"));

var emailInput  = form.GetByRole("textbox", "Email");
var submitBtn   = form.GetByRole("button", "Sign in");
var errorLabel  = form.TryGetByText("Invalid credentials");
var fieldByTest = form.GetByTestId("password-field");
```

### Element wait extensions

```csharp
// Chain waits fluently
driver.GetByRole("button", "Load more")
      .WaitUntilVisible()
      .Click();

// Wait until clickable (visible + enabled)
driver.GetByTestId("submit-btn")
      .WaitUntilClickable(timeoutSeconds: 10)
      .Click();
```

---

## Locator API

`Locator` is a lazy, chainable, auto-retrying element handle — the Playwright-style counterpart to Selenium's eager `FindElement`. The DOM is not queried until an action (`Click`, `Fill`, …) or state query (`IsVisible`, `Count`, …) is called. Every call re-evaluates the selector fresh, so stale-element references are recovered automatically.

Obtain a Locator via `driver.Locator(selector)` or any `GetBy*` call on the driver or on another Locator.

### Basic usage

```csharp
using SimpleSeleniumSupport.Selectors;

// CSS or XPath — auto-detected
driver.Locator("[data-testid='submit']").Click();
driver.Locator("//input[@name='email']").Fill("user@example.com");

// Scoped (parent >> child)
driver.Locator("form#checkout").GetByRole("button", "Pay Now").Click();
```

### ActionOptions — timeout and force

```csharp
var opts = new ActionOptions
{
    TimeoutSeconds = 20,    // override per-action timeout
    PollingMs      = 200,   // polling interval while waiting
    Force          = true   // skip actionability checks (visible/enabled)
};

driver.Locator("#submit").Click(opts);
```

### Actions

```csharp
var loc = driver.Locator("#email");

loc.Click();                            // left-click (waits visible + enabled)
loc.DblClick();                         // double-click
loc.RightClick();                       // context-menu click

loc.Fill("user@example.com");           // clear + type (fast)
loc.Type("user@example.com");           // character-by-character key events
loc.PressSequentially("abc", delayMs: 50);
loc.Clear();                            // clear value only
loc.Press("Enter");                     // keyboard shortcut
loc.Press("Control+A");

loc.Check();                            // tick a checkbox
loc.Uncheck();                          // untick a checkbox

loc.SelectOption("value");              // <select> by value
loc.SelectOption(index: 2);            // <select> by index
loc.SelectOption(label: "Australia");  // <select> by visible text

loc.Hover();
loc.Focus();
loc.Blur();

loc.ScrollIntoView();

loc.DragTo(driver.Locator("#target")); // drag-and-drop to another locator

loc.DispatchEvent("click");            // fire a synthetic DOM event
loc.Evaluate<string>("el => el.id");   // run JS in element scope

byte[] png = loc.Screenshot();         // screenshot of the element's bounding box
loc.SetInputFiles("/path/to/file.txt");// file input upload
```

### State queries (no throw)

```csharp
bool visible  = loc.IsVisible();
bool hidden   = loc.IsHidden();
bool enabled  = loc.IsEnabled();
bool disabled = loc.IsDisabled();
bool checked_ = loc.IsChecked();
bool editable = loc.IsEditable();

int   count   = driver.Locator("li.item").Count();
```

### Reading content

```csharp
string? text      = loc.TextContent();
string? inner     = loc.InnerHTML();
string? attr      = loc.GetAttribute("href");
string? val       = loc.InputValue();
```

### Filtering

```csharp
// Keep only rows that contain "Alice"
driver.Locator("tr").Filter(hasText: "Alice").GetByRole("button").Click();

// Exclude rows containing "disabled"
driver.Locator("tr").Filter(hasNotText: "disabled").First.Click();

// Keep only rows that contain a visible checkbox
driver.Locator("tr").Filter(has: driver.Locator("input[type=checkbox]")).Count();
```

### Indexing

```csharp
driver.Locator("tr").First.TextContent();
driver.Locator("tr").Last.TextContent();
driver.Locator("tr").Nth(2).GetByRole("checkbox").Check();   // 0-based
```

### Combining locators

```csharp
// AND — element must match both locators
var strictBtn = driver.Locator("button").And(driver.Locator(".primary"));
strictBtn.Click();

// OR — element matching either locator
var anyToggle = driver.Locator("input[type=checkbox]").Or(driver.Locator("input[type=radio]"));
Console.WriteLine($"Toggles found: {anyToggle.Count()}");
```

### Wait helpers

```csharp
// Wait until state changes (polls up to LocatorTimeoutSeconds)
loc.WaitFor(LocatorState.Visible);
loc.WaitFor(LocatorState.Hidden);
loc.WaitFor(LocatorState.Enabled);
loc.WaitFor(LocatorState.Detached);   // element removed from DOM
```

### `driver.Locator()` extension

```csharp
// Top-level factory — available without additional using
var btn = driver.Locator("button.submit");
```

---

## FrameLocator (iframe support)

`FrameLocator` scopes element searches inside an iframe. The driver switches into the frame before each operation and back to default content afterwards.

### Single iframe

```csharp
// Obtain via driver extension
driver.FrameLocator("#checkout-frame")
      .GetByRole("button", "Pay")
      .Click();
```

### Nested iframes

```csharp
driver.FrameLocator("#outer-frame")
      .InnerFrame("#inner-frame")
      .Locator("input[name=card]")
      .Fill("4111111111111111");
```

### All locator factories work inside a frame

```csharp
var frame = driver.FrameLocator("#widget");

frame.Locator(".price").TextContent();
frame.GetByRole("button", "Submit").Click();
frame.GetByText("Confirm").IsVisible();
frame.GetByTestId("close-btn").Click();
```

---

## Playwright-Style Assertions

`WebExpect.That(locator)` and `WebExpect.That(driver)` provide auto-retrying assertions that poll the DOM for up to `SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds` (default: 5 s) before failing — giving the page time to settle without explicit waits.

### Element assertions

```csharp
using SimpleSeleniumSupport.Assertions;

var submit = driver.Locator("#submit");

WebExpect.That(submit).IsVisible();
WebExpect.That(submit).IsEnabled();
WebExpect.That(submit).IsChecked();
WebExpect.That(submit).IsEditable();
WebExpect.That(submit).IsDisabled();
WebExpect.That(submit).IsHidden();
WebExpect.That(submit).IsEmpty();
```

### Content assertions

```csharp
WebExpect.That(driver.Locator("h1")).HasText("Welcome back");
WebExpect.That(driver.Locator(".subtitle")).ContainsText("logged in");
WebExpect.That(driver.Locator("#qty")).HasValue("3");
WebExpect.That(driver.Locator(".badge")).HasAttribute("aria-label", "5 items");
WebExpect.That(driver.Locator(".badge")).HasClass("active");
WebExpect.That(driver.Locator("li")).HasCount(5);
WebExpect.That(driver.Locator("#panel")).ContainsHTML("<strong>important</strong>");
```

### Negation with `.Not`

```csharp
WebExpect.That(driver.Locator("#error")).Not.IsVisible();
WebExpect.That(driver.Locator("#spinner")).Not.IsVisible();
WebExpect.That(driver.Locator("#status")).Not.HasText("Error");
```

### Page assertions

```csharp
WebExpect.That(driver).HasTitle("Dashboard — MyApp");
WebExpect.That(driver).TitleContains("MyApp");
WebExpect.That(driver).HasUrl("https://myapp.com/dashboard");
WebExpect.That(driver).UrlContains("/dashboard");
WebExpect.That(driver).UrlStartsWith("https://myapp.com");

// Negation
WebExpect.That(driver).Not.UrlContains("/login");
```

### Assertion timeout

```csharp
// Override globally
SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds = 10;
```

### Soft assertions — collect all failures

Use `SoftAssertions` when you want to run every check and see all failures at once rather than stopping at the first one.

```csharp
using var soft = new SoftAssertions(driver);

soft.Expect(driver.Locator("h1")).HasText("Welcome");
soft.Expect(driver.Locator("#badge")).HasCount(3);
soft.Expect(driver.Locator(".status")).ContainsText("active");
soft.Expect(driver).HasTitle("Dashboard");
soft.Expect(driver).UrlContains("/dashboard");

// All failures thrown together as SoftAssertionException at end of using block
// soft.AssertAll();  // or call explicitly before Dispose
```

Inspect failures before throwing:

```csharp
using var soft = new SoftAssertions(driver);
// ... assertions ...
if (soft.FailureCount > 0)
    Console.WriteLine($"{soft.FailureCount} assertion(s) failed — see log.");
soft.AssertAll();   // throws SoftAssertionException listing all failures
```

---

## SeleniumPage

`SeleniumPage` wraps an `IWebDriver` with a Playwright-style page API covering navigation, waits, JavaScript evaluation, screenshots, cookies, local/session storage, dialogs, downloads, viewport, and geolocation. Obtain via `driver.AsPage()`.

```csharp
using SimpleSeleniumSupport.Page;

var page = driver.AsPage();
```

### Navigation

```csharp
page.GoTo("https://example.com");
page.Reload();
page.GoBack();
page.GoForward();

Console.WriteLine(page.Url);
Console.WriteLine(page.Title);
Console.WriteLine(page.Content());   // page source HTML
```

### Wait helpers

```csharp
// Wait until URL matches a glob or regex
page.WaitForUrl("**/dashboard");
page.WaitForUrl("^https://myapp\\.com/user/\\d+");

// Wait for a load state
page.WaitForLoadState(LoadState.Load);
page.WaitForLoadState(LoadState.DomContentLoaded);
page.WaitForLoadState(LoadState.NetworkIdle);

// Wait for a CSS/XPath selector
page.WaitForSelector("#confirm-dialog");

// Wait for a custom JS condition
page.WaitForFunction("return document.readyState === 'complete' && !window.__loading");
```

### JavaScript evaluation

```csharp
string title    = page.Evaluate<string>("document.title")!;
long  itemCount = page.Evaluate<long>("document.querySelectorAll('li').length");
page.Evaluate("window.scrollTo(0, document.body.scrollHeight)");
```

### Page content

```csharp
// Replace entire page DOM with custom HTML (useful for isolated component testing)
page.SetContent("<h1>Hello World</h1><button id='btn'>Click me</button>");
WebExpect.That(driver.Locator("h1")).HasText("Hello World");
```

### Screenshot

```csharp
byte[] png = page.Screenshot();                      // in-memory bytes
page.Screenshot("screenshots/landing.png");          // save to file
```

### Cookies

```csharp
page.AddCookie("session", "abc123");
page.AddCookie("pref", "dark", domain: ".example.com");

var cookies = page.GetCookies();

page.DeleteCookie("session");
page.ClearCookies();
```

### Local storage & session storage

```csharp
page.SetLocalStorageItem("theme", "dark");
string? theme = page.GetLocalStorageItem("theme");
page.RemoveLocalStorageItem("theme");
page.ClearLocalStorage();

page.SetSessionStorageItem("cart-id", "XYZ-9");
string? cartId = page.GetSessionStorageItem("cart-id");
```

### Dialogs (alert / confirm / prompt)

```csharp
// Register a handler — called on a background thread when a dialog appears
page.OnDialog(dialog =>
{
    Console.WriteLine($"Dialog type: {dialog.Type}  Message: {dialog.Message}");
    dialog.Accept();          // or dialog.Accept("my input") for prompts
    // dialog.Dismiss();
});

driver.FindElement(By.Id("delete-btn")).Click();  // triggers confirm dialog

// Or wait for a dialog explicitly and handle it inline
var dialog = page.WaitForDialog(timeoutMs: 5000);
Assert.AreEqual("Are you sure?", dialog.Message);
dialog.Accept();
```

### Downloads

```csharp
page.SetDownloadDirectory(@"C:\Temp\Downloads");

// Run an action that triggers a download, then wait for the file
DownloadInfo dl = page.ExpectDownload(() =>
    driver.FindElement(By.Id("export-btn")).Click(),
    timeoutMs: 30_000);

Console.WriteLine($"Downloaded: {dl.FilePath}  Size: {dl.SizeBytes} bytes");
dl.Dispose();   // deletes the temp file
```

### Viewport & emulation

```csharp
page.SetViewportSize(375, 812);         // iPhone 13 portrait
var size = page.GetViewportSize();      // System.Drawing.Size

page.SetGeolocation(latitude: 51.5, longitude: -0.1);  // London
page.SetUserAgent("Mozilla/5.0 (compatible; MyTestBot/1.0)");
```

### Locator factories on SeleniumPage

`SeleniumPage` exposes the same locator factories as the driver:

```csharp
page.Locator("#submit").Click();
page.GetByRole("button", "Pay").Click();
page.GetByText("Welcome").IsVisible();
page.GetByTestId("nav-menu").Hover();
page.FrameLocator("#payment-frame").GetByRole("button", "Confirm").Click();
```

---

## Mouse & Keyboard Input

Low-level absolute-coordinate and element-relative mouse/keyboard operations. Obtain via `driver.Mouse()` and `driver.Keyboard()`.

### Mouse — clicks at coordinates

```csharp
using SimpleSeleniumSupport.Input;

driver.Mouse().Click(100, 200);
driver.Mouse().DblClick(300, 400);
driver.Mouse().RightClick(100, 200);
driver.Mouse().Hover(500, 300);
```

### Mouse — element-based

```csharp
var element = driver.FindElement(By.Id("canvas-item"));
driver.Mouse().Click(element);
driver.Mouse().DblClick(element);
driver.Mouse().Hover(element);
```

### Drag-and-drop

```csharp
// Element-to-element drag
var source = driver.FindElement(By.Id("card-1"));
var target = driver.FindElement(By.Id("column-done"));
driver.Mouse().DragTo(source, target);

// Manual drag with MouseChain
driver.Mouse()
      .Down()            // click-hold
      .MoveTo(target)    // move cursor to element
      .Up()              // release
      .Perform();
```

### Scroll

```csharp
// Scroll the page
driver.Mouse().Wheel(deltaX: 0, deltaY: 500);    // scroll down 500 px

// Scroll inside a specific element (e.g. a scrollable div)
var list = driver.FindElement(By.Id("lazy-list"));
driver.Mouse().Wheel(list, deltaX: 0, deltaY: 300);
```

### Keyboard — type and press

```csharp
// Focus an element then type
driver.FindElement(By.Id("search")).Click();
driver.Keyboard().Type("wireless headphones");
driver.Keyboard().Press("Enter");

// Compound shortcuts
driver.Keyboard().Press("Control+A");
driver.Keyboard().Press("Control+C");
driver.Keyboard().Press("Escape");
driver.Keyboard().Press("F5");
```

### Modifier keys

```csharp
// Shift-select text
driver.Keyboard().Down("Shift");
driver.Keyboard().Press("End");
driver.Keyboard().Up("Shift");
```

### Fast text injection (no key events)

```csharp
// Equivalent to a clipboard paste — no individual keydown/keyup events
driver.FindElement(By.Id("address")).Click();
driver.Keyboard().InsertText("123 Main Street, Anytown, CA 90210");
```

### Slow typing (character-by-character with delay)

```csharp
driver.Keyboard().PressSequentially("Hello World", delayMs: 100);
```

---

## Resilient Selector Engine

`SelectorPath` / `SelectorNode` / `SelectorEngine` provide a multi-strategy element location system with automatic confidence scoring, telemetry recording, and optional disk persistence. It tries multiple selector strategies (XPath, CSS, etc.) and picks the highest-confidence match.

### Build and use a SelectorPath

```csharp
using SimpleSeleniumSupport.Selectors.Engine;

// A SelectorNode tries each strategy in order; highest-confidence element wins
var loginButtonNode = new SelectorNode("loginButton", new ISelectorStrategy[]
{
    new SeleniumByStrategy(By.Id("login-btn"),               baseConfidence: 0.95),
    new SeleniumByStrategy(By.CssSelector("button.login"),   baseConfidence: 0.80),
    new SeleniumByStrategy(By.XPath("//button[.='Sign in']"), baseConfidence: 0.70)
});

// A SelectorPath chains multiple nodes (scope → child)
var formPath = new SelectorPath("loginFormFlow", new[]
{
    new SelectorNode("loginForm",   new[] { new SeleniumByStrategy(By.Id("login-form"), 0.99) }),
    loginButtonNode
});

// Resolve — walks the chain and returns the highest-confidence element
IWebElement btn = SelectorEngine.Find(driver, formPath);
btn.Click();

// Resolve with telemetry flushed to disk after the call
IWebElement btn2 = SelectorEngine.Find(driver, formPath, persist: true);
```

### Confidence scoring

Confidence is computed per-element: `strategy.BaseConfidence + 0.1 (if Displayed) + 0.05 (if Enabled)`, capped at 1.0. Elements are ranked descending; the best candidate is returned.

### Telemetry

```csharp
// Read all recorded events (PathId, Success, Confidence)
var events = SelectorTelemetry.Snapshot();
foreach (var ev in events)
    Console.WriteLine(ev);

// Clear after each test
SelectorTelemetry.Clear();
```

### Persistence

```csharp
// Change the output directory for selector history JSON files
SelectorPersistence.OutputDirectory = "selector-history";
```

---

## Visual Regression Testing

Compare screenshots pixel-by-pixel using SSIM + edge detection. Generates heatmaps and side-by-side diff images.

### Simple screenshot comparison

```csharp
// Take screenshots as byte arrays
var baseline = File.ReadAllBytes("baselines/homepage.png");
var actual   = ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;

var options = new ImageComparisonOptions
{
    Threshold  = 95.0,   // % — fail below this
    SsimWeight = 0.7,    // SSIM contribution
    EdgeWeight = 0.3,    // Edge similarity contribution
};

ImageComparisonResult result = ImageComparator.Compare(baseline, actual, options);

Console.WriteLine($"Similarity: {result.SimilarityPercent:F2}%");
Console.WriteLine($"Passed:     {result.Passed}");
Console.WriteLine($"SSIM:       {result.SsimPercent:F2}%");
Console.WriteLine($"Edge:       {result.EdgePercent:F2}%");
Console.WriteLine($"Pixel diff: {result.PixelDiffCount}/{result.TotalPixels}");
```

### File-based comparison

```csharp
var result = ImageComparator.Compare(
    baselinePath: "baselines/checkout.png",
    actualPath:   "actuals/checkout.png",
    options: new ImageComparisonOptions { Threshold = 98.0 });

Assert.IsTrue(result.Passed, $"Visual diff too large: {result.SimilarityPercent:F1}%");
```

### Compare a specific region

```csharp
using System.Drawing;

var options = new ImageComparisonOptions
{
    Mode    = ImageComparator.ComparisonMode.RegionOnly,
    Region  = new Rectangle(x: 0, y: 0, width: 800, height: 200),  // header only
    Threshold = 99.0
};

var result = ImageComparator.Compare(baselineBytes, actualBytes, options);
```

### Ignore dynamic regions

```csharp
var options = new ImageComparisonOptions
{
    Threshold = 95.0
};
// Exclude regions with ads, timestamps, or animated content
options.IgnoreRegions.Add(new Rectangle(1100, 0,  200, 80));   // top-right ad banner
options.IgnoreRegions.Add(new Rectangle(0,    900, 300, 50));   // footer timestamp

var result = ImageComparator.Compare(baselineBytes, actualBytes, options);
```

### Tolerance for anti-aliasing and small pixel shifts

```csharp
var options = new ImageComparisonOptions
{
    AntiAliasingTolerance = 3,   // ignore AA differences within 3-pixel radius
    PixelTolerance        = 10,  // treat per-channel differences ≤ 10 as identical
    Threshold             = 95.0
};
```

### Save heatmap and diff image

```csharp
var options = new ImageComparisonOptions
{
    Threshold                = 95.0,
    VisualDiffOutputDirectory = "TestArtifacts/VisualDiffs"
};

var result = ImageComparator.Compare(baselineBytes, actualBytes, options);

// Paths to generated images (null if unchanged/not saved)
Console.WriteLine($"Heatmap:    {result.HeatmapPath}");
Console.WriteLine($"SideBySide: {result.DiffImagePath}");
```

### Baseline management

```csharp
// Resolve a baseline (creates it on first run when AUTO_BASELINE=1)
var size     = new Size(driver.Manage().Window.Size.Width, driver.Manage().Window.Size.Height);
var shotPath = Path.Combine("actuals", "homepage.png");
File.WriteAllBytes(shotPath, ((ITakesScreenshot)driver).GetScreenshot().AsByteArray);

var ctx = VisualBaselineManager.Resolve(
    testId:         "HomepageTest",
    browser:        "Chrome",
    viewport:       size,
    artifactsRoot:  "TestArtifacts",
    actualImagePath: shotPath);

if (!ctx.BaselineCreated && !ctx.BaselineUpdated)
{
    var result = ImageComparator.Compare(ctx.BaselinePath!, ctx.ActualPath!,
                     new ImageComparisonOptions { Threshold = 95.0 });
    Assert.IsTrue(result.Passed, $"Visual regression: {result.SimilarityPercent:F1}%");
}
```

Set `AUTO_BASELINE=1` environment variable to automatically create/update baselines:

```bash
# PowerShell
$env:AUTO_BASELINE = "1"
dotnet test

# Linux / macOS
AUTO_BASELINE=1 dotnet test
```

### AI-enhanced comparison (optional)

```csharp
var options = new ImageComparisonOptions
{
    Threshold   = 95.0,
    EnableAI    = true,              // adds AI reasoning to the result
    Provider    = "ollama",
    OllamaModel = "llava"            // use a vision-capable model
};

var result = ImageComparator.Compare(baselineBytes, actualBytes, options);
Console.WriteLine($"AI says: {result.AiReasoning}");
```

### Generate a visual diff HTML report

```csharp
var html = new StringBuilder();
html.AppendLine(VisualDiffHtmlReport.CreateDocument("Sprint 42 Visual Report", content =>
{
    foreach (var (ctx, result) in testResults)
    {
        VisualDiffHtmlReport.Append(content, ctx, result);
    }
}));
File.WriteAllText("Reports/visual-report.html", html.ToString());
```

Features: sticky summary bar, sidebar TOC, pass/fail card borders, dark-mode toggle, click-to-zoom lightbox.

### Visual grid report (tabular)

```csharp
var rows = testResults.Select(r => new VisualGridRow
{
    TestId        = r.ctx.TestId,
    Browser       = r.ctx.Browser,
    Viewport      = $"{r.ctx.Viewport.Width}x{r.ctx.Viewport.Height}",
    Similarity    = r.result.SimilarityPercent,
    Passed        = r.result.Passed,
    Threshold     = r.result.Threshold,
    BaselineImage = r.ctx.BaselinePath,
    ActualImage   = r.ctx.ActualPath,
    HeatmapImage  = r.result.HeatmapPath,
    DiffImage     = r.result.DiffImagePath,
    Reasoning     = r.result.Reasoning,
    AiReasoning   = r.result.AiReasoning,
    SsimPercent   = r.result.SsimPercent,
    EdgePercent   = r.result.EdgePercent,
    PixelDiffPercent = r.result.PixelDiffPercent,
}).ToList();

HtmlGridJsReportExporter.ExportToHtml(rows, "Reports/visual-grid.html");
```

---

## OCR Text Extraction

Extract text from screenshots or specific page regions using Tesseract OCR. Useful for validating text rendered as images (canvas, SVG, custom fonts, CAPTCHAs).

### Prerequisites

Install Tesseract and download a language pack:

```bash
# Windows (via winget)
winget install UB-Mannheim.TesseractOCR

# macOS
brew install tesseract

# Ubuntu / Debian
sudo apt install tesseract-ocr

# Download English tessdata to your project tessdata/ folder
# https://github.com/tesseract-ocr/tessdata
```

Set the path once:

```csharp
SimpleSeleniumSupportDefaults.OcrTessdataPath = "tessdata";  // relative to working dir
SimpleSeleniumSupportDefaults.OcrLanguage     = "eng";
```

### Extract text from full page screenshot

```csharp
// Takes a screenshot and runs OCR over the whole image
string pageText = driver.GetScreenshotText();
Assert.That(pageText, Does.Contain("Welcome"));
```

### Extract text from a region

```csharp
using System.Drawing;

// OCR only the header area (0,0 → 1280×80)
string headerText = driver.GetRegionText(
    new Rectangle(0, 0, 1280, 80),
    new OcrOptions { Language = "eng", PageSegMode = 7 }  // PSM 7 = single text line
);
Console.WriteLine($"Header: {headerText}");
```

### Extract text from an element

```csharp
// Crop the screenshot to the element bounding box and OCR it
var priceTag = driver.GetByTestId("product-price");
string priceText = priceTag.GetElementText(driver);
Assert.That(priceText, Does.Match(@"\$\d+\.\d{2}"));
```

### Assert element text via OCR

```csharp
// Throws InvalidOperationException when text is not found
var badge = driver.GetByTestId("status-badge");
badge.AssertTextContains(driver, "Active");

// Pattern match
badge.AssertTextMatches(driver, new Regex(@"Active|Pending"));
```

### OcrOptions reference

```csharp
var opts = new OcrOptions
{
    Language           = "eng",     // Tesseract language pack
    PageSegMode        = 3,         // 3=auto, 6=block, 7=line, 8=word, 10=char, 11=sparse
    WhitelistChars     = "0123456789",  // restrict to digits (e.g. for OTP fields)
    TessdataPath       = "tessdata",
    NormalizeWhitespace = true      // collapse whitespace in returned text
};
```

---

## Web Performance Monitoring

Collect Navigation Timing, Paint Timing, and Core Web Vitals from the browser and evaluate them against a `PerformanceBudget`.

### Collect metrics

```csharp
driver.Navigate().GoToUrl("https://example.com");

PerformanceResult perf = driver.CollectPerformanceMetrics();

Console.WriteLine($"FCP:       {perf.FirstContentfulPaintMs:F0} ms");
Console.WriteLine($"LCP:       {perf.LargestContentfulPaintMs:F0} ms");
Console.WriteLine($"DOMLoaded: {perf.DomContentLoadedMs:F0} ms");
Console.WriteLine($"FullLoad:  {perf.FullPageLoadMs:F0} ms");
Console.WriteLine($"TTFB:      {perf.ServerResponseMs:F0} ms");
Console.WriteLine($"DNS:       {perf.DnsLookupMs:F0} ms");
```

### Evaluate against a budget

```csharp
var budget = new PerformanceBudget
{
    MaxFirstContentfulPaintMs   = 1_800,
    MaxLargestContentfulPaintMs = 2_500,
    MaxFullPageLoadMs           = 5_000,
    MaxServerResponseMs         = 600
};

PerformanceResult perf = driver.CollectPerformanceMetrics(budget);

if (!perf.BudgetPassed)
{
    foreach (var violation in perf.BudgetViolations)
        Console.WriteLine($"Budget violated: {violation}");
}

Assert.IsTrue(perf.BudgetPassed, "Page load exceeded performance budget");
```

### Assert budget (throws on violation)

```csharp
// Throws PerformanceBudgetException with violation details if any threshold is exceeded
driver.AssertPerformanceBudget(PerformanceBudget.GoogleGoodThresholds());
```

### Google's "Good" thresholds preset

```csharp
// Pre-configured to Google Core Web Vitals "Good" category thresholds
var budget = PerformanceBudget.GoogleGoodThresholds();
// FCP ≤ 1800 ms, LCP ≤ 2500 ms, DOM ≤ 3000 ms, Full load ≤ 5000 ms, TTFB ≤ 600 ms
```

### Async variant

```csharp
PerformanceResult perf = await driver.CollectPerformanceMetricsAsync(budget);
```

---

## Screenshot Annotation

Draw failure overlays onto screenshots — red top banner with the exception message, grey info bar with test name and timestamp, and optional element highlight box. Uses `System.Drawing.Common` (already a library dependency; no extra NuGet needed).

### Standard failure annotation

```csharp
byte[] rawPng = ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;

byte[] annotated = ScreenshotAnnotator.AnnotateFailure(
    rawPng,
    failureMessage: "ElementClickInterceptedException: element not interactable",
    testName:       "CheckoutFlow_PaymentStep");

File.WriteAllBytes("Artifacts/checkout_fail_annotated.png", annotated);
```

### Custom annotation

```csharp
using System.Drawing;

byte[] annotated = ScreenshotAnnotator.Annotate(rawPng, new AnnotationOptions
{
    TopBanner    = "FAILURE: Button not found",
    BottomBanner = "Test: LoginFlow | Build: CI-4221",
    AddTimestamp = true,

    // Highlight the failing element's bounding box
    HighlightRegion = new Rectangle(x: 320, y: 200, width: 150, height: 40),
    HighlightColor  = Color.Red,
    HighlightWidth  = 3,

    BannerColor = Color.FromArgb(200, 220, 30, 30),
    TextColor   = Color.White,
    FontSize    = 13f
});
```

### Auto-annotate on failure (global toggle)

```csharp
// When true, FailureDiagnostics.SaveDiagnostics automatically annotates screenshots
SimpleSeleniumSupportDefaults.AnnotateScreenshotsOnFailure = true;
```

---

## Accessibility Auditing

`AxeRunOptions` and `AxeResult` provide the data model for axe-core accessibility audits. Use `AxeRunOptions` to configure which WCAG rules to evaluate.

### AxeRunOptions reference

```csharp
var options = new AxeRunOptions
{
    // Limit to specific WCAG conformance levels
    Tags          = new[] { "wcag2a", "wcag2aa" },

    // Or run only specific rules by ID
    RunOnly       = new[] { "color-contrast", "image-alt", "label" },

    // Disable specific rules
    DisabledRules = new[] { "region" },

    // Scope to a CSS selector instead of the full page
    Scope         = "#main-content",

    // Minimum impact to report: minor | moderate | serious | critical
    MinImpact     = "serious",

    // Throw AxeAccessibilityException when violations are found
    ThrowOnFail   = true,

    // How to inject axe-core: EmbeddedResource (default) | Cdn | AlreadyLoaded
    InjectionMode = AxeInjectionMode.EmbeddedResource,
    TimeoutMs     = 30_000
};
```

### AxeResult properties

```csharp
// AxeResult is populated by your axe runner
AxeResult result = /* ... your axe integration ... */;

Console.WriteLine($"URL:          {result.Url}");
Console.WriteLine($"Violations:   {result.ViolationCount}");
Console.WriteLine($"Passed rules: {result.PassedRules.Count}");
Console.WriteLine($"Passed:       {result.Passed}");

foreach (var v in result.Violations)
    Console.WriteLine($"[{v.Impact.ToUpperInvariant()}] {v.Id}: {v.Description} " +
                      $"({v.TargetSelectors.Count} element(s)) — {v.HelpUrl}");
```

---

## Video Recording

Capture test execution as an MP4 video — works in **headless and headed** Chrome, Edge, and Firefox, locally and on Selenium Grid. No OS-level display capture is used; frames are taken with `ITakesScreenshot.GetScreenshot()` and encoded in real-time by FFmpeg.

### Prerequisites

FFmpeg must be installed and on PATH, or its full path must be set:

```bash
# Verify FFmpeg is available
ffmpeg -version
```

Download from [ffmpeg.org](https://ffmpeg.org/download.html) or install via package manager:

```bash
# Windows (winget)
winget install --id=Gyan.FFmpeg

# macOS
brew install ffmpeg

# Ubuntu / Debian
sudo apt install ffmpeg
```

Set a custom path once at suite startup if FFmpeg is not on PATH:

```csharp
SimpleSeleniumSupportDefaults.VideoRecordingFfmpegPath      = @"C:\tools\ffmpeg\bin\ffmpeg.exe";
SimpleSeleniumSupportDefaults.VideoRecordingOutputDirectory = "Recordings";
SimpleSeleniumSupportDefaults.VideoRecordingFrameRate       = 5;   // fps (5 is reliable in CI)
```

### Resolution enum

| Value | Dimensions | Use case |
|-------|-----------|----------|
| `VideoResolution.R360p` | 640 × 360 | Low bandwidth, fast CI |
| `VideoResolution.R720p` | 1280 × 720 | HD — recommended default |
| `VideoResolution.R1080p` | 1920 × 1080 | Full HD, larger files |

### Local recording (headless or headed)

```csharp
using var recorder = driver.StartLocalRecording(new VideoRecordingOptions
{
    Resolution      = VideoResolution.R720p,
    TestName        = "CheckoutFlow",
    OutputDirectory = "Recordings"
});

// … run test actions …

recorder.Stop();
result.ScreencastPath = recorder.VideoPath;   // attach to TestResult
```

All `VideoRecordingOptions` properties have defaults from `SimpleSeleniumSupportDefaults`:

```csharp
public class VideoRecordingOptions
{
    VideoResolution Resolution      // default: R720p
    int             FrameRate       // default: SimpleSeleniumSupportDefaults.VideoRecordingFrameRate (5)
    string          OutputDirectory // default: SimpleSeleniumSupportDefaults.VideoRecordingOutputDirectory ("Recordings")
    string?         TestName        // default: null → auto timestamp
    string          FfmpegPath      // default: SimpleSeleniumSupportDefaults.VideoRecordingFfmpegPath ("ffmpeg")
}
```

### Discard on pass — record only failures

```csharp
using var recorder = driver.StartLocalRecording(options);
try
{
    RunTest();
    recorder.Stop(discard: true);      // test passed — delete the video
}
catch
{
    recorder.Stop();                   // test failed — keep the video
    result.ScreencastPath = recorder.VideoPath;
    throw;
}
```

### Async stop (xUnit / NUnit 3 async teardown)

```csharp
await recorder.StopAsync();
result.ScreencastPath = recorder.VideoPath;
```

### Grid recording (server-side)

Selenium Grid 4 can record video server-side. The flow is:

```csharp
// ① Enable recording BEFORE creating the RemoteWebDriver
var opts = new ChromeOptions();
GridVideoRecorder.EnableRecording(opts, VideoResolution.R1080p);
var driver = new RemoteWebDriver(new Uri("http://grid:4444"), opts);

// ② Create the downloader while the session is still live (captures SessionId)
var recorder = driver.CreateGridRecorder("http://grid:4444", "Recordings");

// ③ Run the test, then end the session (Grid finalises video on session end)
driver.Quit();

// ④ Download — retries up to 120 s if Grid is still processing
string? videoPath = await recorder.DownloadVideoAsync("CheckoutFlow");
result.ScreencastPath = videoPath;
```

`DownloadVideoAsync` polls `GET {hub}/session/{sessionId}/video` with linear back-off (2 s, 4 s, 6 s … up to 30 s per attempt) until the file is available or the timeout elapses.

### Integrate with NUnit TearDown

```csharp
private IVideoRecorder? _recorder;

[SetUp]
public void Setup()
{
    // … create driver …
    _recorder = driver.StartLocalRecording(new VideoRecordingOptions
    {
        TestName        = TestContext.CurrentContext.Test.Name,
        OutputDirectory = "Recordings"
    });
}

[TearDown]
public void TearDown()
{
    bool passed = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed;
    _recorder?.Stop(discard: passed);   // discard video if test passed
    result.ScreencastPath = _recorder?.VideoPath;
    _recorder = null;
    driver?.Quit();
}
```

---

## Failure Diagnostics

Save a full set of failure artifacts in one call:

```csharp
// Saves to: Diagnostics/failure_<timestamp>/
//   screenshot.png              (raw)
//   screenshot_annotated.png    (red banner overlay — when AnnotateScreenshotsOnFailure=true)
//   page.html
//   console_logs.txt
//   network_info.xlsx
//   network_info.har

string diagFolder = FailureDiagnostics.SaveDiagnostics(
    driver,
    capture:     networkCapture,   // optional
    consoleLogs: consoleLogs,      // optional
    baseFolder:  "Diagnostics");

Console.WriteLine($"Artifacts at: {diagFolder}");
```

---

## Network Reporting

### Interactive network HTML report (single file)

```csharp
string reportPath = HtmlExporterSingleWithGridjs.ExportNetworkInfoToSingleHtml(
    items:          traffic,
    outFolderRoot:  "Reports",
    reportName:     "API Smoke Test",
    pageSize:       50,
    maxFieldLength: 300);

Console.WriteLine($"Report: {reportPath}");
```

Features: stats bar (Total/2xx/3xx/4xx/5xx), method/resource filter dropdowns, coloured status/latency badges, tabbed detail modal, copy-URL button, keyboard shortcuts (`/` = search, `Esc` = clear).

### Chunked HTML report (for large captures)

```csharp
string indexPath = HtmlExporterChunkedWithGridjs.ExportNetworkInfoToChunkedHtml(
    items:          traffic,
    outFolderRoot:  "Reports",
    pageSize:       500,
    maxFieldLength: 200,
    reportName:     "Full Session Capture");
```

### Excel export

```csharp
ExcelExporter.ExportNetworkInfoToExcel(traffic, "Reports/network.xlsx");
// Columns: RequestId, URL, Method, Timestamps, Latency, Status, Headers, Bodies
// AutoFilter and freeze-pane enabled on header row
```

### HAR export

```csharp
HarExporter.ExportToHar(traffic, "Reports/network.har");
// HAR 1.2 — importable into Chrome DevTools, Charles Proxy, Fiddler, etc.
```

---

## Test Run Report

`TestRunReportExporter` produces a production-ready interactive HTML report for any number of test results. All data is loaded client-side from a single `data.json`, so the report scales comfortably to tens of thousands of tests without pagination overhead.

### Output options

Three export modes — pick the one that fits your workflow:

| Method | Output | Best for |
|--------|--------|----------|
| `Export()` | Folder: `index.html` + `data.json` + `manifest.json` | Large suites; CI artefact folders |
| `ExportSingleFile()` | One self-contained `.html` file | E-mail, Slack, quick sharing |
| `ExportToExcel()` | One `.xlsx` workbook | Post-processing, pivot tables, stakeholder reports |

### Folder report (`Export`)

```
Reports/my-suite-20250301_143022/
├── index.html      ← open this in a browser
├── data.json       ← all rows (client-side filtering + paging)
└── manifest.json   ← summary metadata + per-run breakdown (when RunName is set)
```

```csharp
string folder = TestRunReportExporter.Export(
    results:       results,
    outFolderRoot: "Reports",
    reportName:    "Regression Suite — Sprint 42");

Console.WriteLine($"Report: {folder}\\index.html");
```

### Single-file HTML (`ExportSingleFile`)

Produces one `.html` file with `data.json` and `manifest.json` embedded inline — no external dependencies. Open directly from disk, attach to a CI e-mail, or drag into a browser.

> For very large result sets (5 000+ rows) the file size can exceed several MB. Use `Export()` in those cases.

```csharp
string htmlFile = TestRunReportExporter.ExportSingleFile(
    results:       results,
    outFolderRoot: "Reports",
    reportName:    "Regression Suite — Sprint 42");
```

### Excel report (`ExportToExcel`)

One row per test result. Colour-coded Status column (green / red / amber / grey). Columns: # · Run · Test Name · Suite · Full Name · Category · Tags · Status · Duration (ms) · Start Time · Browser · Environment · Machine · Exception Type · Exception Message · Stack Trace · Assert Message · Skip Reason · Screenshot · Screencast · Diagnostics Folder · Network HAR · Network Excel · AI Analysis · **AI Classification** · **AI Confidence** · Custom Properties.

`AI Classification` is colour-coded (Rose = `ProductIssue`, Yellow = `TestIssue`, Orange = `Flaky`, Lavender = `Infrastructure`, Grey = `Uncertain`) matching the HTML report badges exactly. These columns are populated when `TestFailureAnalyzer` (or `AnalyzeFailuresAsync`) is run before exporting.

```csharp
string xlsxFile = TestRunReportExporter.ExportToExcel(
    results:       results,
    outFolderRoot: "Reports",
    reportName:    "Regression Suite — Sprint 42");
```

### TestResult model

Populate a `TestResult` for every test and hand the collection to the exporter.

```csharp
using SimpleSeleniumSupport.Reporting;

// ── Minimal (pass) ─────────────────────────────────────────────────────
var passed = new TestResult
{
    TestName    = "LoginFlow_WithValidCredentials",
    TestSuite   = "AuthTests",
    Status      = TestStatus.Pass,
    DurationMs  = 1240,
    Browser     = "Chrome 124",
    Environment = "QA"
};

// ── From an exception (fail / error) ───────────────────────────────────
var failed = new TestResult
{
    TestName    = "CheckoutFlow_PaymentDeclined",
    TestSuite   = "CheckoutTests",
    Category    = "Regression",
    Tags        = "payment,critical",
    Browser     = "Firefox 125",
    Environment = "Staging",
    MachineName = Environment.MachineName,

    // Run label — set when combining multiple runs into one consolidated report
    RunName     = "Firefox 126 — Staging",

    // Screenshot / screencast / network artifacts
    ScreenshotPath   = "artifacts/checkout_fail.png",
    ScreencastPath   = "artifacts/checkout_fail.mp4",
    NetworkHarPath   = "artifacts/checkout.har",
    NetworkExcelPath = "artifacts/checkout_network.xlsx",

    // AI analysis (populated by AnalyzeAndSuggestFix)
    AiAnalysis = "**Root cause**: Element #pay-btn detached before click.\n" +
                 "XPATH_CANDIDATE: //button[@data-testid='pay-btn']",

    // Arbitrary key-value metadata shown in Custom Properties tab
    CustomProperties = new()
    {
        ["Build"]   = "CI-4221",
        ["Sprint"]  = "2025-Q2-S3",
        ["StoryId"] = "PROJ-987"
    }
}.WithException(lastException!)   // populates ExceptionType / ExceptionMessage / StackTrace / Status
 .WithDuration(testStartTime);    // sets DurationMs from start UTC
```

### Report features

| Feature | Detail |
|---------|--------|
| **Stats bar** | Total / Pass / Fail / Skip / Error / Pass Rate / Total Duration / Avg Duration |
| **Runs summary bar** | Appears when `RunName` is set — one card per run with pass/fail counts and a mini progress bar; click to filter |
| **Filter bar** | Status pills · Suite / Category / Browser / Environment / Run dropdowns · live search · page-size |
| **Sortable grid** | GridJS paginated table; click any row to open the detail panel |
| **Run column** | Shown automatically when any result has `RunName` set |
| **Overview tab** | All identity + environment fields in a key-value table |
| **Failure tab** | Exception type+message · assertion detail · collapsible stack trace with **Copy** button |
| **AI Analysis tab** | Styled panel — `**bold**` rendered, `XPATH_CANDIDATE:` lines highlighted |
| **Screenshot tab** | Inline image with click-to-zoom lightbox |
| **Screencast tab** | `<video>` player for `.mp4 / .webm / .ogv / .mov`; clickable link for other URLs |
| **Network tab** | Download HAR / Excel buttons (only visible when paths are set) |
| **Custom Properties tab** | Key/value table from `TestResult.CustomProperties` |
| **CSV / JSON export** | Exports the currently filtered row set |
| **Excel export** | Colour-coded Status, DateTime cells, AutoFilter, freeze-pane |
| **Single-file HTML** | Data embedded inline; shareable without extra files |
| **Keyboard shortcuts** | `/` = focus search · `Esc` = clear search or close lightbox |

### Integrate with NUnit TearDown

```csharp
private readonly List<TestResult> _results = new();
private DateTime _testStart;
private Exception? _lastEx;

[SetUp]
public void Setup()
{
    _testStart = DateTime.UtcNow;
    _lastEx    = null;
    // ... driver setup
}

[TearDown]
public void TearDown()
{
    var result = new TestResult
    {
        TestName    = TestContext.CurrentContext.Test.Name,
        TestSuite   = GetType().Name,
        FullName    = TestContext.CurrentContext.Test.FullName,
        Browser     = "Chrome",
        Environment = "QA",
        MachineName = System.Environment.MachineName,
    }.WithDuration(_testStart);

    if (_lastEx != null)
    {
        result.WithException(_lastEx);

        // Screenshot
        var ssPath = Path.Combine("Artifacts", $"{result.TestName}.png");
        Directory.CreateDirectory("Artifacts");
        File.WriteAllBytes(ssPath, ((ITakesScreenshot)driver).GetScreenshot().AsByteArray);
        result.ScreenshotPath = ssPath;

        // AI analysis
        result.AiAnalysis = driver.AnalyzeAndSuggestFix(_lastEx,
            testName: result.TestName, capture: capture);
    }
    else
    {
        result.Status = TestStatus.Pass;
    }

    _results.Add(result);
    driver?.Quit();
}

[OneTimeTearDown]
public void ExportReport()
{
    string folder = TestRunReportExporter.Export(
        results:       _results,
        outFolderRoot: "Reports",
        reportName:    "My Test Suite");

    Console.WriteLine($"Test report: {folder}\\index.html");
}
```

### Cross-run consolidated report via RunName

```csharp
foreach (var r in chromeResults)  r.RunName = "Chrome 124";
foreach (var r in firefoxResults) r.RunName = "Firefox 126";
foreach (var r in edgeResults)    r.RunName = "Edge 124";

var allResults = chromeResults.Concat(firefoxResults).Concat(edgeResults).ToList();
string folder = TestRunReportExporter.Export(
    results:       allResults,
    outFolderRoot: "Reports",
    reportName:    "Cross-Browser Suite — Sprint 42");
```

---

## Parallel Test Reporting (TestResultCollector)

`TestResultCollector` is a thread-safe accumulator for parallel test runs where multiple tests execute concurrently and a single report is needed after all tests complete.

### Basic usage (NUnit parallel)

```csharp
// Shared fixture-level field — safe to use from all parallel test threads
private static readonly TestResultCollector _collector = new();

[TearDown]
public void TearDown()
{
    // Called concurrently from parallel tests — fully thread-safe
    _collector.Add(new TestResult
    {
        TestName  = TestContext.CurrentContext.Test.Name,
        Status    = TestStatus.Pass,
        // ...
    });
}

[OneTimeTearDown]
public static void ExportAll()
{
    _collector.Export("TestReports", "My Suite");
    _collector.ExportSingleFile("TestReports", "My Suite");
    _collector.ExportToExcel("TestReports", "My Suite");

    // Or export all three at once:
    _collector.ExportAll("TestReports", "My Suite");
}
```

### Process-wide singleton

```csharp
// Access the shared singleton from anywhere in the process
TestResultCollector.Shared.Add(result);

// Reset before each suite run
TestResultCollector.ResetShared();
```

### AddRange and Count

```csharp
_collector.AddRange(batchResults);
Console.WriteLine($"Collected {_collector.Count} results so far");
```

### Analyze failures with AI before exporting

Call `AnalyzeFailuresAsync()` (or its synchronous wrapper `AnalyzeFailures()`) **before** any `Export*` call. It groups failed tests, sends them to the AI provider, and writes `AiClassification`, `AiConfidence`, and `AiAnalysis` back onto each failing `TestResult` so every exported format automatically includes the classification.

```csharp
[OneTimeTearDown]
public static async Task ExportAll()
{
    // Step 1: classify failures — mutates TestResult.AiClassification on all Fail/Error results
    await _collector.AnalyzeFailuresAsync(new FailureAnalysisOptions
    {
        Grouping       = FailureGroupingStrategy.PerClass,  // one AI call per test class
        MaxParallelism = 3                                  // run 3 groups concurrently
    });

    // Step 2: export — AI columns are already populated in all output formats
    _collector.ExportSingleFile("TestReports", "My Suite");  // HTML with AI badge column
    _collector.ExportToExcel("TestReports", "My Suite");     // Excel with AI Classification col
}
```

To skip AI analysis and just export:

```csharp
_collector.ExportAll("TestReports", "My Suite");
```

#### Fluent chaining

`AnalyzeFailuresAsync` returns `this`, so you can chain export calls:

```csharp
await (await _collector.AnalyzeFailuresAsync())
    .ExportSingleFile("TestReports", "My Suite");
```

### Export to CI formats

```csharp
// Export JUnit XML
_collector.ExportToJUnit("TestReports/junit-results.xml", "My Suite");

// Export NUnit 3 XML
_collector.ExportToNUnitXml("TestReports/nunit-results.xml", "My Suite");

// Export TRX (Azure DevOps)
_collector.ExportToTrx("TestReports/results.trx", "My Suite");
```

---

## CI / Format Export

Export test results to standard CI-compatible formats that integrate with Jenkins, GitLab CI, GitHub Actions, Azure DevOps, and TeamCity.

### JUnit XML (Jenkins, GitLab CI, GitHub Actions)

```csharp
using SimpleSeleniumSupport.CiExport;

JUnitXmlExporter.Export(
    results:    results,
    outputPath: "TestReports/junit-results.xml",
    suiteName:  "Regression Suite");
```

The output is JUnit 4 Surefire format: `<testsuites>` → `<testsuite>` (grouped by `TestSuite`) → `<testcase>`. Failures include `<failure>` elements with message, type, and stack trace. Skipped tests emit `<skipped>`. AI analysis is written to `<system-out>`.

### NUnit 3 XML (Azure DevOps, TeamCity)

```csharp
NUnitXmlExporter.Export(
    results:    results,
    outputPath: "TestReports/nunit-results.xml",
    runName:    "Regression Suite");
```

### TRX (Azure DevOps native format)

```csharp
TrxExporter.Export(
    results:    results,
    outputPath: "TestReports/results.trx",
    runName:    "Regression Suite");
```

### Allure JSON (Allure Report / Allure TestOps)

```csharp
using SimpleSeleniumSupport.Notifications;

// Writes {uuid}-result.json files to the allure-results folder
AllureJsonExporter.Export(results, outputDirectory: "allure-results");
```

Then run `allure generate allure-results --clean` to build the Allure HTML report. Labels (suite, testClass, browser, epic, tag) are mapped automatically from `TestResult` fields.

### Complete CI pipeline example (GitHub Actions)

```yaml
- name: Run tests
  run: dotnet test --logger "trx;LogFileName=results.trx"

- name: Export JUnit XML (for GitHub test annotations)
  run: |
    dotnet script export.csx
    # export.csx:
    # JUnitXmlExporter.Export(results, "TestReports/junit.xml");

- name: Publish test results
  uses: dorny/test-reporter@v1
  with:
    name: Test Results
    path: TestReports/junit.xml
    reporter: java-junit
```

---

## Notifications

Send test-run summaries to Slack, Microsoft Teams, or email after each suite run. All notifiers implement `INotificationProvider` and can be used via extension methods.

### Slack (Incoming Webhook)

```csharp
using SimpleSeleniumSupport.Notifications;

var slack = new SlackNotifier("https://hooks.slack.com/services/T.../B.../...");

bool sent = await results.NotifyAsync(
    slack,
    runName:   "Nightly Regression",
    reportUrl: "https://ci.example.com/jobs/123/report.html");
```

The Slack message uses Block Kit: header with status emoji, facts section (Total/Passed/Failed/Pass Rate/Duration/Environment/Branch/Build), and an optional "View Report" button.

### Microsoft Teams (Incoming Webhook)

```csharp
var teams = new TeamsNotifier("https://outlook.office.com/webhook/.../IncomingWebhook/...");

bool sent = await results.NotifyAsync(
    teams,
    runName:   "Nightly Regression",
    reportUrl: "https://ci.example.com/report.html");
```

The Teams message sends an Adaptive Card (v1.5) with a FactSet and an optional "View Report" action.

### SMTP Email

```csharp
var email = new SmtpEmailNotifier(new SmtpEmailConfig
{
    Host       = "smtp.example.com",
    Port       = 587,
    UseSsl     = true,
    Username   = "ci@example.com",
    Password   = Environment.GetEnvironmentVariable("SMTP_PASS"),
    From       = "ci@example.com",
    To         = "team@example.com, qa-lead@example.com",
    Subject    = "Test Run: [[RUN_NAME]] — [[STATUS]]"
});

await results.NotifyAsync(email, runName: "Nightly Regression");
```

### Notify multiple channels in parallel

```csharp
var providers = new INotificationProvider[] { slack, teams, email };

var outcomes = await results.NotifyAllAsync(
    providers,
    runName:   "Nightly Regression",
    reportUrl: "https://ci.example.com/report.html");

foreach (var (channelId, success) in outcomes)
    Console.WriteLine($"{channelId}: {(success ? "sent" : "failed")}");
```

### NotificationPayload — extra context fields

```csharp
// Populate environment/branch/build for richer notification messages
var payload = new NotificationPayload
{
    RunName     = "Nightly Regression",
    Environment = "Staging",
    Branch      = "release/2.4.0",
    BuildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER"),
    ReportUrl   = "https://ci.example.com/report.html",
    // CustomFields appear in templates that support them
    CustomFields = { ["DeployTarget"] = "EU-West" }
};

await slack.SendAsync(payload);
```

---

## Analytics & Flakiness Detection

Track test results across runs in a JSON history file, compute per-test flakiness scores, and export an interactive analytics dashboard with trend charts.

### Append a run to history

```csharp
using SimpleSeleniumSupport.Analytics;

// Called in OneTimeTearDown after tests complete
HistoryStore.Append(results, runName: "Nightly — 2025-03-20");
```

Configure where history is stored:

```csharp
SimpleSeleniumSupportDefaults.AnalyticsEnabled          = true;
SimpleSeleniumSupportDefaults.AnalyticsHistoryDirectory = "TestReports/history";
SimpleSeleniumSupportDefaults.AnalyticsMaxRunsKept      = 50;   // prune older runs
```

### Load and inspect history

```csharp
List<TestRunRecord> history = HistoryStore.Load();
Console.WriteLine($"Runs in history: {history.Count}");

foreach (var run in history.OrderByDescending(r => r.Timestamp))
    Console.WriteLine($"{run.Timestamp:g}  {run.RunName}  " +
                      $"PassRate={run.Statistics.PassRate:F1}%  " +
                      $"Tests={run.Statistics.TotalTests}");
```

### Compute flakiness scores

```csharp
FlakinessReport report = FlakinessAnalyzer.Analyse(history, minimumRuns: 3);

Console.WriteLine($"Runs analysed: {report.RunsAnalysed}");
Console.WriteLine($"Flaky tests (>20%): {report.FlakyTests.Count(f => f.FlakinessScore > 0.2)}");

foreach (var ft in report.FlakyTests.Take(10))
    Console.WriteLine($"{ft.TestName,-50}  score={ft.FlakinessScore:P0}  " +
                      $"fail={ft.FailCount}/{ft.TotalRuns}  " +
                      $"recent=[{string.Join(",", ft.RecentStatuses)}]");
```

### Export the analytics dashboard

```csharp
AnalyticsDashboardExporter.Export(
    history,
    outputPath:     "TestReports/analytics.html",
    dashboardTitle: "Test Analytics — My Suite");

Console.WriteLine("Dashboard: TestReports/analytics.html");
```

The HTML dashboard includes four Chart.js charts:
- **Pass Rate Trend** — line chart across runs
- **Duration Trend** — Median + P95 ms per run
- **Top Flaky Tests** — horizontal bar chart (top 10 by flakiness score)
- **Latest Run Status Distribution** — doughnut chart (Passed / Failed / Skipped)

### FlakyTest properties

| Property | Description |
|----------|-------------|
| `TestName` | Short test name |
| `FullName` | Full qualified name |
| `FlakinessScore` | 0.0 = always consistent, 1.0 = alternates pass/fail every run |
| `PassCount` / `FailCount` | Absolute counts |
| `TotalRuns` | Total runs analysed for this test |
| `RecentStatuses` | Status string for last 10 runs (oldest → newest) |

### RunStatistics properties

| Property | Description |
|----------|-------------|
| `TotalTests` | Test count |
| `Passed` / `Failed` / `Skipped` | Absolute counts |
| `PassRate` | 0.0–100.0 % |
| `MinDurationMs` / `MaxDurationMs` | Fastest and slowest test |
| `MedianDurationMs` | P50 duration |
| `P95DurationMs` | P95 duration |
| `TotalDurationMs` | Sum of all durations |

---

## Consolidated Report Builder

`ConsolidatedReportBuilder` scans a root folder for all previously generated SimpleSeleniumSupport reports — folder-based **and** single-file HTML — deserializes their results, and produces one merged report. No shared in-memory list is required; every source report is read from disk.

This is the right tool when different CI jobs, machines, or test sessions each write their own report and you want a single merged view afterwards.

### Discovery rules

| Source type | How it is detected |
|-------------|-------------------|
| **Folder-based** (`Export()`) | `data.json` present with a sibling `manifest.json` |
| **Single-file HTML** (`ExportSingleFile()`) | File contains `<meta name="generator" content="SimpleSeleniumSupport">` |

`index.html` files are always skipped.

### Produce a consolidated folder report

```csharp
string consolidated = ConsolidatedReportBuilder.Export(
    rootFolder:   "Reports",
    outputFolder: "Reports",
    reportName:   "All Runs — Consolidated");

Console.WriteLine($"Consolidated report: {consolidated}\\index.html");
```

### Produce a single-file consolidated report

```csharp
string htmlFile = ConsolidatedReportBuilder.ExportSingleFile(
    rootFolder:   "Reports",
    outputFolder: "Reports",
    reportName:   "All Runs — Consolidated");
```

### Discover without exporting

```csharp
IReadOnlyList<DiscoveredReport> found = ConsolidatedReportBuilder.Discover("Reports");

foreach (var r in found)
    Console.WriteLine($"[{r.SourceType,-11}] {r.ReportName} — {r.TestCount} tests  ({r.SourcePath})");
```

`DiscoveredReport` properties:

| Property | Type | Description |
|----------|------|-------------|
| `ReportName` | `string` | Name from `manifest.json` or the `sss:report-name` meta tag |
| `SourcePath` | `string` | Absolute path to the report folder or `.html` file |
| `SourceType` | `ReportSourceType` | `FolderBased` or `SingleFile` |
| `TestCount` | `int` | Number of test results in that source |
| `Results` | `IReadOnlyList<TestResult>` | Fully deserialized `TestResult` objects |

### Produce a consolidated report with AI failure classification

`AnalyzeAndExportSingleFileAsync` and `AnalyzeAndExportAsync` scan, merge, classify failures with AI, then export — all in one call. The AI provider analyses all failures from every discovered report together using the configured grouping strategy.

```csharp
// Single-file HTML — AI classification badges included
string htmlFile = await ConsolidatedReportBuilder.AnalyzeAndExportSingleFileAsync(
    rootFolder:      "Reports",
    outputFolder:    "Reports/consolidated",
    reportName:      "All Runs — AI Classified",
    analysisOptions: new FailureAnalysisOptions
    {
        Grouping       = FailureGroupingStrategy.PerCategory,
        MaxParallelism = 5
    });

// Folder report variant
string folder = await ConsolidatedReportBuilder.AnalyzeAndExportAsync(
    rootFolder:   "Reports",
    outputFolder: "Reports/consolidated",
    reportName:   "All Runs — AI Classified");
```

If you need to run analysis separately (e.g. to inspect results before exporting):

```csharp
// 1. Discover all reports
var discovered = ConsolidatedReportBuilder.Discover("Reports");
var allResults = discovered.SelectMany(d => d.Results).ToList();

// 2. Classify failures
var analyzer = new TestFailureAnalyzer(new FailureAnalysisOptions {
    Grouping = FailureGroupingStrategy.PerClass });
await analyzer.AnalyzeAsync(allResults);

// 3. Inspect before exporting
var productBugs = allResults.Where(r => r.AiClassification == "ProductIssue").ToList();
Console.WriteLine($"Genuine product bugs found: {productBugs.Count}");

// 4. Export with classification data already populated
TestRunReportExporter.ExportSingleFile(allResults, "Reports/consolidated", "All Runs");
```

### CI pipeline example

Each matrix job writes its own report; a final step merges them with AI classification:

```yaml
jobs:
  test:
    strategy:
      matrix:
        browser: [chrome, firefox, edge]
    steps:
      - run: dotnet test --filter "Browser=${{ matrix.browser }}"
        # Each job writes to Reports/<browser>-<timestamp>/

  consolidate:
    needs: test
    steps:
      - run: |
          dotnet script consolidate.csx
          # consolidate.csx:
          # ConsolidatedReportBuilder.ExportSingleFile(
          #     "Reports", "Reports/consolidated", "Full Matrix");
      - uses: actions/upload-artifact@v4
        with:
          name: consolidated-report
          path: Reports/consolidated/*.html
```

---

## Structured Logging

Wire a `Microsoft.Extensions.Logging` `ILoggerFactory` to receive structured log output from all SimpleSeleniumSupport internals (AI calls, network events, selector resolutions, video recording, etc.). By default all output is suppressed via `NullLoggerFactory`.

### Console logging (development)

```csharp
using Microsoft.Extensions.Logging;

SimpleSeleniumSupportDefaults.LoggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug));
```

### NUnit TestContext sink

```csharp
SimpleSeleniumSupportDefaults.LoggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddProvider(new NUnitLoggerProvider())   // any ILoggerProvider targeting test output
        .SetMinimumLevel(LogLevel.Information));
```

### Serilog integration

```csharp
using Serilog;
using Serilog.Extensions.Logging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/sss-.txt", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Debug()
    .CreateLogger();

SimpleSeleniumSupportDefaults.LoggerFactory =
    new SerilogLoggerFactory(Log.Logger);
```

---

## Requirements

| Requirement | Detail |
|-------------|--------|
| .NET | 8.0+ |
| Selenium | 4.40+ (`Selenium.WebDriver`, `Selenium.Support`) |
| Browser | Chrome or Edge (for network capture; DevTools required) |
| Ollama | [ollama.com](https://ollama.com) — only for AI features with Ollama provider |
| LLM models | Text: `llama3` · Vision: `llava` (AI image comparison) |
| FFmpeg | [ffmpeg.org](https://ffmpeg.org/download.html) — only for local video recording |
| Tesseract | Required for OCR features — download tessdata language packs separately |
| OS | Windows (System.Drawing.Common is Windows-only) |

### Ollama setup

```bash
# Install and start Ollama
ollama serve

# Pull a text model for AI element finding / failure analysis
ollama pull llama3

# Pull a vision model for AI image comparison (optional)
ollama pull llava
```

---

## Full Example — NUnit Test Class

```csharp
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport;
using SimpleSeleniumSupport.AI;
using SimpleSeleniumSupport.Assertions;
using SimpleSeleniumSupport.Input;
using SimpleSeleniumSupport.Network;
using SimpleSeleniumSupport.Image;
using SimpleSeleniumSupport.OCR;
using SimpleSeleniumSupport.Page;
using SimpleSeleniumSupport.Performance;
using SimpleSeleniumSupport.Selectors;
using SimpleSeleniumSupport.Recording;
using SimpleSeleniumSupport.Reporting;
using SimpleSeleniumSupport.Diagnostics;
using SimpleSeleniumSupport.Notifications;
using SimpleSeleniumSupport.Analytics;
using SimpleSeleniumSupport.CiExport;
using System.Drawing;

[TestFixture]
public class ExampleTests
{
    private IWebDriver driver = null!;
    private Capture capture = null!;
    private IVideoRecorder? recorder;
    private Exception? lastException;
    private DateTime testStart;

    // Thread-safe accumulator for parallel test runs
    private static readonly TestResultCollector Collector = new();

    [OneTimeSetUp]
    public static void GlobalSetup()
    {
        // ── Logging ───────────────────────────────────────────────────────────
        SimpleSeleniumSupportDefaults.LoggerFactory = LoggerFactory.Create(b =>
            b.AddConsole().SetMinimumLevel(LogLevel.Information));

        // ── AI provider ───────────────────────────────────────────────────────
        // Option A: Ollama (local, free)
        SimpleSeleniumSupportDefaults.OllamaModel = "llama3";

        // Option B: OpenAI (cloud)
        // AIProviderRegistry.Use("openai", new AIProviderConfig
        // {
        //     ApiKey = Environment.GetEnvironmentVariable("OPENAI_KEY"),
        //     Model  = "gpt-4o-mini"
        // });

        // ── Other defaults ────────────────────────────────────────────────────
        SimpleSeleniumSupportDefaults.VisualThreshold              = 95.0;
        SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds        = 10;
        SimpleSeleniumSupportDefaults.AnnotateScreenshotsOnFailure = true;
        SimpleSeleniumSupportDefaults.AnalyticsEnabled             = true;
        SimpleSeleniumSupportDefaults.AiSanitizeByDefault          = true;
    }

    [SetUp]
    public void Setup()
    {
        testStart     = DateTime.UtcNow;
        lastException = null;
        driver        = new ChromeDriver();
        capture       = new Capture(driver);
        capture.StartMonitoring(clearPrevious: true);
        recorder      = driver.StartLocalRecording(new VideoRecordingOptions
        {
            TestName        = TestContext.CurrentContext.Test.Name,
            OutputDirectory = "Recordings"
        });
    }

    [TearDown]
    public void TearDown()
    {
        capture.StopMonitoring();

        // ── Build TestResult ───────────────────────────────────────────────
        var result = new TestResult
        {
            TestName    = TestContext.CurrentContext.Test.Name,
            FullName    = TestContext.CurrentContext.Test.FullName,
            TestSuite   = nameof(ExampleTests),
            Browser     = "Chrome",
            Environment = "QA",
            MachineName = System.Environment.MachineName,
        }.WithDuration(testStart);

        if (lastException != null)
        {
            result.WithException(lastException);

            // Keep video for failures
            recorder?.Stop(discard: false);
            result.ScreencastPath = recorder?.VideoPath;

            // Annotated screenshot (red banner)
            Directory.CreateDirectory("Artifacts");
            var ssBytes = ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;
            var ssAnnotated = ScreenshotAnnotator.AnnotateFailure(
                ssBytes, lastException.Message, result.TestName);
            var ssPath = Path.Combine("Artifacts", $"{result.TestName}_{testStart:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(ssPath, ssAnnotated);
            result.ScreenshotPath = ssPath;

            // Full diagnostics bundle
            result.DiagnosticsFolder = FailureDiagnostics.SaveDiagnostics(
                driver, capture: capture, baseFolder: "Diagnostics");

            result.NetworkHarPath   = Path.Combine(result.DiagnosticsFolder, "network_info.har");
            result.NetworkExcelPath = Path.Combine(result.DiagnosticsFolder, "network_info.xlsx");

            // AI failure analysis
            result.AiAnalysis = driver.AnalyzeAndSuggestFix(
                lastException, testName: result.TestName, capture: capture);
            TestContext.WriteLine($"[AI] {result.AiAnalysis}");
        }
        else
        {
            result.Status = TestStatus.Pass;
            recorder?.Stop(discard: true);   // delete video for passing tests
        }

        recorder = null;
        Collector.Add(result);
        driver.Quit();
    }

    [OneTimeTearDown]
    public static async Task ExportReports()
    {
        // ── AI failure classification ──────────────────────────────────────
        // Classify each Fail/Error result before exporting — writes AiClassification,
        // AiConfidence, and AiAnalysis back onto each TestResult so every export
        // format (HTML badge column, Excel colour-coded cell) includes the data.
        await Collector.AnalyzeFailuresAsync(new FailureAnalysisOptions
        {
            Grouping             = FailureGroupingStrategy.PerClass,  // group by test class
            MaxParallelism       = 3,                                  // concurrent AI calls
            IncludeScreenshot    = true,                               // attach screenshot filename
            Sanitization         = new SanitizationOptions            // scrub credentials
            {
                ScrubPasswords = true,
                ScrubApiTokens = true,
                RedactFilePaths = true
            }
        });

        // ── HTML report (folder + data.json) ──────────────────────────────
        var folder = Collector.Export("Reports", "Example Suite");
        Console.WriteLine($"Report: {folder}\\index.html");

        // ── Single-file HTML ───────────────────────────────────────────────
        Collector.ExportSingleFile("Reports", "Example Suite");

        // ── Excel ──────────────────────────────────────────────────────────
        Collector.ExportToExcel("Reports", "Example Suite");

        // ── JUnit XML for GitHub Actions ───────────────────────────────────
        Collector.ExportToJUnit("TestReports/junit.xml", "Example Suite");

        // ── Analytics history ─────────────────────────────────────────────
        var results = Collector.Results.ToList();
        HistoryStore.Append(results, "Example Suite — " + DateTime.UtcNow.ToString("yyyy-MM-dd"));
        var history = HistoryStore.Load();
        AnalyticsDashboardExporter.Export(history, "Reports/analytics.html");

        // ── Slack notification ─────────────────────────────────────────────
        var slackUrl = System.Environment.GetEnvironmentVariable("SLACK_WEBHOOK");
        if (!string.IsNullOrEmpty(slackUrl))
        {
            var slack = new SlackNotifier(slackUrl);
            await results.NotifyAsync(slack, "Example Suite",
                reportUrl: $"file://{Path.GetFullPath(folder)}/index.html");
        }
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Test]
    public void LoginFlow_NetworkAndSelectors()
    {
        try
        {
            var page = driver.AsPage();
            page.GoTo("https://demo.example.com/login");
            page.WaitForLoadState(LoadState.NetworkIdle);

            // Playwright-style Locator actions
            driver.Locator("#email").Fill("user@example.com");
            driver.Locator("#password").Fill("secret");
            driver.Locator("button[type=submit]").Click();

            // Wait for the API call to complete
            var loginResponse = capture.WaitForRequest(
                i => i.RequestUrl?.Contains("/api/auth") == true && i.ResponseStatusCode == 200,
                timeoutSeconds: 15);
            Assert.AreEqual(200, loginResponse.ResponseStatusCode);

            // Auto-retrying assertions (polls up to AssertionTimeoutSeconds)
            WebExpect.That(driver.Locator("h1")).ContainsText("Welcome");
            WebExpect.That(driver).UrlContains("/dashboard");
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void CheckoutFlow_LocatorChainingAndAssertions()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com/shop");

            // Chained locator: scope product grid → filter by name → click add
            driver.Locator(".product-card")
                  .Filter(hasText: "Wireless Headphones")
                  .GetByRole("button", "Add to cart")
                  .Click();

            WebExpect.That(driver.Locator(".cart-count")).HasText("1");

            // Soft assertions — collect all failures before throwing
            using var soft = new SoftAssertions(driver);
            soft.Expect(driver.Locator(".cart-icon")).IsVisible();
            soft.Expect(driver.Locator(".cart-count")).HasText("1");
            soft.Expect(driver).UrlContains("/shop");
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void NetworkMocking_ApiResponse()
    {
        try
        {
            using var router = driver.StartRouting();

            // Mock the products API to return a single item
            router.Route("**/api/products", route =>
                route.Fulfill(
                    body:        """[{"id":1,"name":"Mocked Widget","price":9.99}]""",
                    contentType: "application/json"));

            driver.Navigate().GoToUrl("https://demo.example.com/shop");

            WebExpect.That(driver.Locator(".product-card")).HasCount(1);
            WebExpect.That(driver.Locator(".product-card h2")).HasText("Mocked Widget");
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void IframeCheckout_FrameLocator()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com/checkout");

            // Interact with elements inside an iframe
            driver.FrameLocator("#payment-frame")
                  .Locator("input[name=cardNumber]")
                  .Fill("4111111111111111");

            driver.FrameLocator("#payment-frame")
                  .GetByRole("button", "Pay securely")
                  .Click();

            WebExpect.That(driver.Locator(".confirmation")).IsVisible();
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void SearchPage_MouseAndKeyboard()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com/search");
            driver.FindElement(By.Id("search-box")).Click();

            // Type with key events then submit
            driver.Keyboard().Type("selenium");
            driver.Keyboard().Press("Enter");

            WebExpect.That(driver.Locator(".result-count")).IsVisible();

            // Scroll results list
            driver.Mouse().Wheel(0, 500);
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void Homepage_VisualRegression()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com");

            // Performance check
            var perf = driver.CollectPerformanceMetrics(PerformanceBudget.GoogleGoodThresholds());
            if (!perf.BudgetPassed)
                TestContext.WriteLine($"[Perf] violations: {string.Join(", ", perf.BudgetViolations)}");

            var shotBytes = ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;
            var shotPath  = Path.Combine("Actuals", "homepage.png");
            Directory.CreateDirectory("Actuals");
            File.WriteAllBytes(shotPath, shotBytes);

            var size = new Size(driver.Manage().Window.Size.Width,
                                driver.Manage().Window.Size.Height);

            var ctx = VisualBaselineManager.Resolve(
                "HomepageTest", "Chrome", size, "TestArtifacts", shotPath);

            if (!ctx.BaselineCreated)
            {
                var options = new ImageComparisonOptions
                {
                    Threshold = 95.0,
                    VisualDiffOutputDirectory = "TestArtifacts/Diffs"
                };
                options.IgnoreRegions.Add(new Rectangle(0, 0, 200, 50)); // top banner

                var result = ImageComparator.Compare(ctx.BaselinePath!, ctx.ActualPath!, options);
                Assert.IsTrue(result.Passed,
                    $"Visual regression: {result.SimilarityPercent:F1}% " +
                    $"(threshold {result.Threshold}%). Heatmap: {result.HeatmapPath}");
            }
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public async Task ProductSearch_AIFinder()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com/shop");

            var searchBox = await driver.FindElementByAIAsync("the product search input");
            searchBox.SendKeys("wireless headphones");

            driver.WaitAndFindByAI("search button", timeoutInSeconds: 5).Click();
            capture.WaitForRequest("/api/search", timeoutInSeconds: 10);

            var products = driver.FindElementsByAI("all product card titles in the results");
            Assert.Greater(products.Count, 0, "No products returned");
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void PriceTag_OcrValidation()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com/product/123");

            // Extract text from an element rendered as an image
            var priceEl  = driver.GetByTestId("product-price");
            string price = priceEl.GetElementText(driver,
                new OcrOptions { WhitelistChars = "$0123456789." });

            Assert.That(price, Does.Match(@"\$\d+\.\d{2}"),
                $"OCR price text did not match expected format: '{price}'");
        }
        catch (Exception ex) { lastException = ex; throw; }
    }
}
```

---

## License

Open Source — see [LICENSE](LICENSE) for details.
