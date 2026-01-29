using System.Text.Json.Serialization;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Represents one content item for an Ollama chat message payload.
    /// Serializes to objects like: { "type": "text", "text": "..." } or { "type":"image", "image": "data:..."}
    /// Use the static helpers ForText(...) and ForImage(...) to create instances.
    /// </summary>
    public sealed class ChatContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        // The "text" field in the JSON payload
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        // The "image" field in the JSON payload (data URL)
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        // Factory helper - create a text content item
        public static ChatContentItem ForText(string text)
        {
            return new ChatContentItem
            {
                Type = "text",
                Text = text,
                Image = null
            };
        }

        // Factory helper - create an image content item (pass data URL)
        public static ChatContentItem ForImage(string dataUrl)
        {
            return new ChatContentItem
            {
                Type = "image",
                Image = dataUrl,
                Text = null
            };
        }

        // Optional: small convenience constructors
        public ChatContentItem() { }

        public ChatContentItem(string type, string? text = null, string? image = null)
        {
            Type = type ?? "text";
            Text = text;
            Image = image;
        }
    }
}
