using System.Drawing;

namespace SimpleSeleniumSupport.Image
{
    public sealed class VisualBaselineContext
    {
        public string TestId { get; init; }
        public string Browser { get; init; }
        public Size Viewport { get; init; }

        public string BaselinePath { get; init; }
        public string ActualPath { get; init; }
        public string HeatmapPath { get; set; }

        public bool BaselineCreated { get; init; }
        public bool BaselineUpdated { get; init; }
    }
}
