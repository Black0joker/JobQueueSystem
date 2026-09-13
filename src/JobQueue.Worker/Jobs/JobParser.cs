using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace JobQueue.Worker.Jobs
{
    public static class JobParser
    {
        public static JsonElement ParsePayload(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Payload should always be valid JSON; fall back to an empty object so the
                // handler's own validation surfaces the problem.
                return JsonSerializer.SerializeToElement(new { });
            }
        }
    }
}
