using System;
using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Biletado.Repository.Swagger;

public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        if (type == null) return;
        if (!type.IsEnum) return;

        // Represent enums as string values (names) in OpenAPI
        schema.Type = "string";
        schema.Enum = Enum.GetNames(type)
            .Select(n => (IOpenApiAny)new OpenApiString(n))
            .ToList();

        // Optionally set the format to empty
        schema.Format = null;
    }
}

