using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleSeleniumSupport.Image
{
    internal sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "user";

        // For convenience: either use Content (string) or ContentItems (text + images).
        [JsonPropertyName("content")]
        public object? Content
        {
            get => ContentItems is null ? _contentString : ContentItems;
            set
            {
                if (value is string s) _contentString = s;
                else ContentItems = value as List<ChatContentItem>;
            }
        }

        [JsonIgnore] public string? _contentString;

        [JsonIgnore] public List<ChatContentItem>? ContentItems { get; set; }

        /// <summary>
        /// Base64-encoded images for vision models. Ollama expects raw base64
        /// (no data: URI prefix) in this array.
        /// </summary>
        [JsonPropertyName("images")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; set; }

        // Factory helpers
        public static ChatMessage TextOnly(string text) =>
            new ChatMessage { Role = "user", Content = text };
    }
}
