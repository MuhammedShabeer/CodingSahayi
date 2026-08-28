using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodingSahayi;

public static class VectorService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task<float[]> GetEmbeddingAsync(string text, string baseUrl = "http://localhost:11434")
    {
        try
        {
            var payload = new
            {
                model = "nomic-embed-text",
                prompt = text
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{baseUrl}/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            if (doc.RootElement.TryGetProperty("embedding", out var embeddingProp))
            {
                return embeddingProp.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
            }
        }
        catch { }
        return Array.Empty<float>();
    }

    public static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length == 0 || vectorB.Length == 0 || vectorA.Length != vectorB.Length)
            return 0f;

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0 || normB == 0) return 0f;
        return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
