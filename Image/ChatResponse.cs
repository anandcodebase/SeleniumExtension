using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SimpleSeleniumSupport.Image
{
    internal sealed class ChatResponse
    {
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
        [JsonPropertyName("message")] public ChatResponseMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }
}
