using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Biletado.Repository.Swagger;

public class UuidParameterFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation?.Parameters == null) return;

        foreach (var p in operation.Parameters)
        {
            if (p == null) continue;
            if (p.In == ParameterLocation.Path && string.Equals(p.Name, "id", StringComparison.OrdinalIgnoreCase))
            {
                p.Schema ??= new OpenApiSchema { Type = "string" };
                // Accept both upper- and lower-case hex digits for UUIDs
                p.Schema.Pattern = "^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$";
            }
        }
    }
}

