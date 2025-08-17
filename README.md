# SimpleSeleniumSupport

An advanced Selenium WebDriver toolkit for .NET designed to make your UI automation more powerful, resilient, and intelligent.

## Features

- **AI-Powered Element Finding**: Use natural language descriptions like "the login button" to find elements, making your tests resistant to UI changes.
- **AI Failure Analysis**: Automatically analyze test failures, exceptions, and page source to suggest code fixes.
- **Advanced Network Capture**: Monitor all network traffic, wait for specific API calls to complete, and export traffic logs to Excel.
- **Rich Element Selectors**: A full suite of Playwright-style selectors (`GetByText`, `GetByLabel`, etc.) and ARIA role-based finders.

## Quick Start

1.  **Install the Package**:
    ```
    Install-Package SimpleSeleniumSupport
    ```

2.  **Find an Element with AI**:
    ```csharp
    // Requires a local Ollama instance
    var loginButton = driver.FindElementByAI("the main login button");
    loginButton.Click();
    ```

3.  **Capture Network Traffic**:
    ```csharp
    var capture = new Capture(driver);
    capture.StartMonitoring();
    
    // Perform actions that trigger network calls...
    
    var apiCall = capture.WaitForRequest("/api/login");
    Assert.AreEqual(200, apiCall.ResponseStatusCode);
    
    capture.StopMonitoring();
    ```