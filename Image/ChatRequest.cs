using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SimpleSeleniumSupport.Image
{
    internal sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("messages")] public List<ChatMessage>? Messages { get; set; }
        // When "format":"json" is set, Ollama tries to produce JSON.
        [JsonPropertyName("format")] public string? Format { get; set; }
        // You can add "options" if you want (e.g., temperature)
    }
}
