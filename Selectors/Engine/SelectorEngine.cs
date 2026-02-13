using OpenQA.Selenium;
namespace SimpleSeleniumSupport.Selectors.Engine
{
    public static class SelectorEngine
    {
        public static IWebElement Find(
            IWebDriver driver,
            SelectorPath path,
            bool persist = false)
        {
            var result = path.Resolve(driver);

            SelectorTelemetry.Record(new
            {
                path.PathId,
                Success = result != null,
                Confidence = result?.Confidence ?? 0
            });

            if (persist)
            {
                SelectorPersistence.Flush(
                    SelectorTelemetry.Snapshot());
            }

            if (result == null)
                throw new NoSuchElementException(
                    $"SelectorPath '{path.PathId}' failed.");

            return result.Element;
        }
    }
}
