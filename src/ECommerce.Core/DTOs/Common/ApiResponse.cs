using System.Text.Json.Serialization;

namespace ECommerce.Core.DTOs.Common;

// Every successful response is wrapped the same way: { "success": true, "data": ..., "meta"?: ... }.
public class ApiResponse<T>
{
    public bool Success { get; init; } = true;
    public T? Data { get; init; }

    // Only paginated lists have meta; leave the property out of the JSON instead of sending null.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationMeta? Meta { get; init; }
}

// Non-generic helper so controllers can write ApiResponse.Ok(dto) and let C# infer T.
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, PaginationMeta? meta = null) => new() { Data = data, Meta = meta };
}
