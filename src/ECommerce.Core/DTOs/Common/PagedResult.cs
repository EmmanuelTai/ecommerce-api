namespace ECommerce.Core.DTOs.Common;

// One page of results plus the numbers the client needs to page through the rest.
// Controllers send Items as "data" and Meta as "meta".
public record PagedResult<T>(IReadOnlyList<T> Items, PaginationMeta Meta);
