namespace SimpleSeleniumSupport.Image
{
    public interface IImageComparisonProvider
    {
        string Name { get; }

        string CompareImages(
            byte[] expected,
            byte[] actual,
            ImageComparisonOptions options);

        string ExtractText(
            byte[] image,
            ImageComparisonOptions options);
    }
}
