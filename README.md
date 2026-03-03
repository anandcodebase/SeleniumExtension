# SimpleSeleniumSupport

An advanced Selenium WebDriver toolkit for .NET 8+ that adds AI-powered element finding, network monitoring, visual regression testing, Playwright-style selectors, video recording, and rich reporting — all as extension methods on your existing `IWebDriver`.

[![NuGet](https://img.shields.io/nuget/v/SimpleSeleniumSupport)](https://www.nuget.org/packages/SimpleSeleniumSupport)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Table of Contents

1. [Installation](#installation)
2. [Global Configuration](#global-configuration)
3. [AI Element Finding](#ai-element-finding)
4. [AI Failure Analysis](#ai-failure-analysis)
5. [Network Capture & Monitoring](#network-capture--monitoring)
6. [Playwright-Style Selectors](#playwright-style-selectors)
7. [Visual Regression Testing](#visual-regression-testing)
8. [Video Recording](#video-recording)
9. [Failure Diagnostics](#failure-diagnostics)
10. [Network Reporting](#network-reporting)
11. [Test Run Report](#test-run-report)
12. [Consolidated Report Builder](#consolidated-report-builder)
13. [Requirements](#requirements)

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
using SimpleSeleniumSupport.Network;
using SimpleSeleniumSupport.Image;
using SimpleSeleniumSupport.Selectors;
using SimpleSeleniumSupport.Recording;
using SimpleSeleniumSupport.Reporting;
using SimpleSeleniumSupport.Diagnostics;
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

    // ── Timeouts ─────────────────────────────────────────────
    SimpleSeleniumSupportDefaults.WaitTimeoutSeconds        = 15;   // WaitAndFindByAI
    SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds     = 10;   // GetByRole / GetByText / etc.
    SimpleSeleniumSupportDefaults.NetworkWaitTimeoutSeconds = 30;   // WaitForRequest

    // ── Visual regression ─────────────────────────────────────
    SimpleSeleniumSupportDefaults.VisualThreshold = 97.0;           // % similarity to pass

    // ── Video recording ───────────────────────────────────────
    SimpleSeleniumSupportDefaults.VideoRecordingFfmpegPath      = "ffmpeg";       // or full path to ffmpeg.exe
    SimpleSeleniumSupportDefaults.VideoRecordingOutputDirectory = "Recordings";
    SimpleSeleniumSupportDefaults.VideoRecordingFrameRate       = 5;              // fps; 5 is reliable in CI
}
```

---

## AI Element Finding

Requires a running [Ollama](https://ollama.com) instance. Uses the page DOM to generate XPath selectors via a local LLM.

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

## AI Failure Analysis

When a test fails, call `AnalyzeAndSuggestFix` to get AI-powered explanations and remediation steps. Diagnostics (screenshot, HTML, network) are saved automatically.

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

The AI response includes:
- 2 probable causes for the failure
- 2 concrete code-level fixes (with XPath/CSS examples)
- An `XPATH_CANDIDATE:` line that is immediately validated against the live page

### Tune DOM trimming

```csharp
AISuggestFixConfig.UseTrimmedDom = true;   // default
AISuggestFixConfig.TrimMaxNodes  = 300;    // reduce for faster AI calls
AISuggestFixConfig.TrimMaxChars  = 8_000;
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
Console.WriteLine($"Heatmap:  {result.HeatmapPath}");
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
    Threshold = 95.0,
    EnableAI  = true,              // adds AI reasoning to the result
    Provider  = "ollama",
    OllamaModel = "llava"          // use a vision-capable model
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

## Video Recording

Capture test execution as an MP4 video — works in **headless and headed** Chrome, Edge, and Firefox,
locally and on Selenium Grid. No OS-level display capture is used; frames are taken with
`ITakesScreenshot.GetScreenshot()` and encoded in real-time by FFmpeg.

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

`DownloadVideoAsync` polls `GET {hub}/session/{sessionId}/video` with linear back-off
(2 s, 4 s, 6 s … up to 30 s per attempt) until the file is available or the timeout elapses.
Returns `null` if the video is still unavailable after all retries.

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
//   screenshot.png
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

Produces one `.html` file with `data.json` and `manifest.json` embedded inline — no external dependencies.
Open directly from disk, attach to a CI e-mail, or drag into a browser.

> For very large result sets (5 000+ rows) the file size can exceed several MB. Use `Export()` in those cases.

```csharp
string htmlFile = TestRunReportExporter.ExportSingleFile(
    results:       results,
    outFolderRoot: "Reports",
    reportName:    "Regression Suite — Sprint 42");

Console.WriteLine($"Report: {htmlFile}");
```

### Excel report (`ExportToExcel`)

One row per test result. Colour-coded Status column (green / red / amber / grey).
Columns: # · Run · Test Name · Suite · Full Name · Category · Tags · Status · Duration (ms) ·
Start Time · Browser · Environment · Machine · Exception Type · Exception Message ·
Stack Trace · Assert Message · Skip Reason · Screenshot · Screencast · Diagnostics Folder ·
Network HAR · Network Excel · AI Analysis · Custom Properties.

```csharp
string xlsxFile = TestRunReportExporter.ExportToExcel(
    results:       results,
    outFolderRoot: "Reports",
    reportName:    "Regression Suite — Sprint 42");

Console.WriteLine($"Excel: {xlsxFile}");
```

To export all three at once:

```csharp
string folder   = TestRunReportExporter.Export(          results, "Reports", "Sprint 42");
string htmlFile = TestRunReportExporter.ExportSingleFile(results, "Reports", "Sprint 42");
string xlsxFile = TestRunReportExporter.ExportToExcel(   results, "Reports", "Sprint 42");
```

### TestResult model

Populate a `TestResult` for every test and hand the collection to the exporter.

```csharp
using SimpleSeleniumSupport.Reporting;

// ── Minimal (pass) ─────────────────────────────────────────────────────
var passed = new TestResult
{
    TestName  = "LoginFlow_WithValidCredentials",
    TestSuite = "AuthTests",
    Status    = TestStatus.Pass,
    DurationMs = 1240,
    Browser   = "Chrome 124",
    Environment = "QA"
};

// ── From an exception (fail / error) ───────────────────────────────────
var failed = new TestResult
{
    TestName  = "CheckoutFlow_PaymentDeclined",
    TestSuite = "CheckoutTests",
    Category  = "Regression",
    Tags      = "payment,critical",
    Browser   = "Firefox 125",
    Environment = "Staging",
    MachineName = Environment.MachineName,

    // Run label — set when combining multiple runs into one consolidated report
    RunName = "Firefox 126 — Staging",

    // Screenshot / screencast / network artifacts
    ScreenshotPath = "artifacts/checkout_fail.png",
    ScreencastPath = "artifacts/checkout_fail.mp4",   // or https:// URL
    NetworkHarPath = "artifacts/checkout.har",
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
| **Screencast tab** | `<video>` player for `.mp4 / .webm / .ogv / .mov` paths and direct video URLs; clickable link for all other URLs (e.g. Jira, Confluence) |
| **Network tab** | Download HAR / Excel buttons (only visible when paths are set) |
| **Custom Properties tab** | Key/value table from `TestResult.CustomProperties` |
| **CSV / JSON export** | Exports the currently filtered row set; includes `runName` column |
| **Excel export** | `ExportToExcel()` — colour-coded Status, DateTime cells, AutoFilter, freeze-pane |
| **Single-file HTML** | `ExportSingleFile()` — data embedded inline; shareable without extra files |
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

### Consolidated report — multiple runs in one report

Set `RunName` on each `TestResult` to identify which test run it came from. When any result in the collection has `RunName` set, `Export()` automatically adds:

- **Runs summary bar** — one card per run showing pass/fail counts and a mini progress bar; click a card to filter to that run
- **Run column** in the grid
- **Run filter** dropdown in the filter bar
- **Run breakdown** in `manifest.json`

```csharp
// ── Tag results from each run ─────────────────────────────────────────
foreach (var r in chromeResults)  r.RunName = "Chrome 124";
foreach (var r in firefoxResults) r.RunName = "Firefox 126";
foreach (var r in edgeResults)    r.RunName = "Edge 124";

// ── One consolidated report ───────────────────────────────────────────
var allResults = chromeResults.Concat(firefoxResults).Concat(edgeResults).ToList();
string folder = TestRunReportExporter.Export(
    results:       allResults,
    outFolderRoot: "Reports",
    reportName:    "Cross-Browser Suite — Sprint 42");
```

To export **both** an individual report per run **and** a consolidated one:

```csharp
// Individual run reports (no RunName needed — each is its own Export call)
TestRunReportExporter.Export(chromeResults,  "Reports", "Chrome Run");
TestRunReportExporter.Export(firefoxResults, "Reports", "Firefox Run");

// Consolidated — tag first, then export all together
foreach (var r in chromeResults)  r.RunName = "Chrome";
foreach (var r in firefoxResults) r.RunName = "Firefox";
TestRunReportExporter.Export(
    chromeResults.Concat(firefoxResults).ToList(),
    "Reports", "All Browsers — Consolidated");
```

> **Tip — cross-session / cross-job consolidation:** when each test run writes its own report file (different process, different CI job, different machine), use [`ConsolidatedReportBuilder`](#consolidated-report-builder) to scan the output folder and merge all reports automatically — no need to share an in-memory `List<TestResult>`.

### Screencast path guidance

The `ScreencastPath` property accepts any of:

```csharp
// Local video file — rendered as <video> player in the report
result.ScreencastPath = @"C:\recordings\test-12345.mp4";
result.ScreencastPath = "recordings/test-12345.webm";

// Remote video URL — also rendered as <video> player
result.ScreencastPath = "https://cdn.example.com/recordings/test-12345.mp4";

// Non-video URL (Jira, Confluence, Selenium Grid, etc.) — rendered as clickable link
result.ScreencastPath = "https://jira.example.com/browse/PROJ-456";
result.ScreencastPath = "https://grid.example.com/session/abc123/video";
```

Supported video extensions: `.mp4`, `.webm`, `.ogv`, `.ogg`, `.mov`.

---

## Consolidated Report Builder

`ConsolidatedReportBuilder` scans a root folder for all previously generated SimpleSeleniumSupport reports — folder-based **and** single-file HTML — deserializes their results, and produces one merged report. No shared in-memory list is required; every source report is read from disk.

This is the right tool when different CI jobs, machines, or test sessions each write their own report and you want a single merged view afterwards.

### Discovery rules

| Source type | How it is detected |
|-------------|-------------------|
| **Folder-based** (`Export()`) | `data.json` present with a sibling `manifest.json` |
| **Single-file HTML** (`ExportSingleFile()`) | File contains `<meta name="generator" content="SimpleSeleniumSupport">` |

`index.html` files are always skipped. HTML files inside a folder that already yielded a folder-based report are also skipped (they are `index.html` of that report).

The `RunName` on every merged result is set to the originating report's name when the original result didn't already carry one — so the consolidated report's Run summary bar and Run filter dropdown identify each source.

### Produce a consolidated folder report

```csharp
// Point at the folder that contains your individual run reports.
// Both folder-based and single-file HTML sources are found automatically.
string consolidated = ConsolidatedReportBuilder.Export(
    rootFolder:   "Reports",
    outputFolder: "Reports",
    reportName:   "All Runs — Consolidated");   // optional; defaults to "Consolidated Report (N runs)"

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

Inspect which reports were found before deciding what to do with them:

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

### Parameters

All three methods share the same signature shape:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `rootFolder` | — | Root directory to scan (required) |
| `outputFolder` | — | Directory where the merged report is written (required) |
| `reportName` | `null` | Display name; defaults to `"Consolidated Report (N runs)"` |
| `recursive` | `true` | When `true`, searches all subdirectories; `false` = top level only |

### CI pipeline example

Each matrix job writes its own report; a final aggregation step merges them:

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

## Requirements

| Requirement | Detail |
|-------------|--------|
| .NET | 8.0+ |
| Selenium | 4.40+ (`Selenium.WebDriver`, `Selenium.Support`) |
| Browser | Chrome or Edge (for network capture; DevTools required) |
| Ollama | [ollama.com](https://ollama.com) — only for AI features |
| LLM models | Text: `llama3` · Vision: `llava` (AI image comparison) |
| FFmpeg | [ffmpeg.org](https://ffmpeg.org/download.html) — only for local video recording |
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

## Full example — NUnit test class

```csharp
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SimpleSeleniumSupport;
using SimpleSeleniumSupport.AI;
using SimpleSeleniumSupport.Network;
using SimpleSeleniumSupport.Image;
using SimpleSeleniumSupport.Selectors;
using SimpleSeleniumSupport.Recording;
using SimpleSeleniumSupport.Reporting;
using SimpleSeleniumSupport.Diagnostics;
using System.Drawing;

[TestFixture]
public class ExampleTests
{
    private IWebDriver driver = null!;
    private Capture capture = null!;
    private IVideoRecorder? recorder;
    private Exception? lastException;
    private DateTime testStart;

    // Shared across all tests in the fixture — exported in OneTimeTearDown
    private static readonly List<TestResult> Results = new();

    [OneTimeSetUp]
    public static void GlobalSetup()
    {
        SimpleSeleniumSupportDefaults.OllamaModel           = "llama3";
        SimpleSeleniumSupportDefaults.VisualThreshold       = 95.0;
        SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds = 10;
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

        // ── Build TestResult for this test ─────────────────────────────────
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

            // Keep video for failures; stop before screenshot so FFmpeg is flushed
            recorder?.Stop(discard: false);
            result.ScreencastPath = recorder?.VideoPath;

            // Capture screenshot
            Directory.CreateDirectory("Artifacts");
            var ssPath = Path.Combine("Artifacts", $"{result.TestName}_{testStart:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(ssPath, ((ITakesScreenshot)driver).GetScreenshot().AsByteArray);
            result.ScreenshotPath = ssPath;

            // Save full failure diagnostics (screenshot + HTML + console + network)
            result.DiagnosticsFolder = FailureDiagnostics.SaveDiagnostics(
                driver, capture: capture, baseFolder: "Diagnostics");

            // Network artifacts
            result.NetworkHarPath   = Path.Combine(result.DiagnosticsFolder, "network_info.har");
            result.NetworkExcelPath = Path.Combine(result.DiagnosticsFolder, "network_info.xlsx");

            // AI analysis
            result.AiAnalysis = driver.AnalyzeAndSuggestFix(
                lastException,
                testName: result.TestName,
                capture:  capture);

            TestContext.WriteLine($"[AI] {result.AiAnalysis}");
        }
        else
        {
            result.Status = TestStatus.Pass;
            recorder?.Stop(discard: true);   // test passed — delete the video
        }

        recorder = null;
        Results.Add(result);
        driver.Quit();
    }

    [OneTimeTearDown]
    public static void ExportReport()
    {
        // ── Individual run report ──────────────────────────────────────────
        var folder = TestRunReportExporter.Export(
            results:       Results,
            outFolderRoot: "Reports",
            reportName:    "Example Suite");

        Console.WriteLine($"Test report: {folder}\\index.html");

        // ── Consolidated report (merges all reports already in the folder) ─
        // Call this after every suite has written its own report.
        // ConsolidatedReportBuilder scans for both folder-based and single-file
        // HTML reports automatically — no in-memory list sharing required.
        // var consolidated = ConsolidatedReportBuilder.Export(
        //     rootFolder:   "Reports",
        //     outputFolder: "Reports",
        //     reportName:   "All Runs — Consolidated");
        // Console.WriteLine($"Consolidated: {consolidated}\\index.html");

        // ── Network report for the session (optional) ──────────────────────
        // HtmlExporterSingleWithGridjs.ExportNetworkInfoToSingleHtml(
        //     combinedTraffic, "Reports", reportName: "Full Session Traffic");
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Test]
    public void LoginFlow_NetworkAndSelectors()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com/login");

            // Playwright-style selectors
            driver.GetByLabel("Email").SendKeys("user@example.com");
            driver.GetByLabel("Password").SendKeys("secret");
            driver.GetByRole("button", "Sign in").Click();

            // Wait for the API call to complete
            var loginResponse = capture.WaitForRequest(
                i => i.RequestUrl?.Contains("/api/auth") == true && i.ResponseStatusCode == 200,
                timeoutSeconds: 15);

            Assert.AreEqual(200, loginResponse.ResponseStatusCode);

            // Verify UI after login
            var greeting = driver.GetByText("Welcome", new LocatorOptions { ExactMatch = false });
            Assert.IsTrue(greeting.Displayed);
        }
        catch (Exception ex) { lastException = ex; throw; }
    }

    [Test]
    public void Homepage_VisualRegression()
    {
        try
        {
            driver.Navigate().GoToUrl("https://demo.example.com");

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
}
```

---

## License

Open Source — see [LICENSE](LICENSE) for details.
