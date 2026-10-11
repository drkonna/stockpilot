// Endpoints/ApiEndpoints.cs
namespace StockPilot.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapStoreEndpoints();
        app.MapStockEndpoints();
        app.MapProductEndpoints();
        app.MapCategoryEndpoints();
        app.MapSupplierEndpoints();
        app.MapProductFamilyEndpoints();
        return app;
    }
}