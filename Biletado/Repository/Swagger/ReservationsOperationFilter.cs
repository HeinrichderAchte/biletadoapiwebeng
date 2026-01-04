using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Biletado.Repository.Swagger;

public class ReservationsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // match by method name (robust enough here)
        var method = context.MethodInfo;
        if (method == null || method.Name != "GetAllReservations") return;

        // Append a small note in the operation description with inline code formatting
        var note = "If `include_deleted` is set to `true`, deleted reservations will be included.";
        if (string.IsNullOrEmpty(operation.Description)) operation.Description = note;
        else operation.Description = operation.Description + "\n\n" + note;

        // Helper to set parameter description and example
        void SetParam(string name, string desc, IOpenApiAny example, string? format = null)
        {
            var p = operation.Parameters?.FirstOrDefault(x => x.Name == name);
            if (p == null) return;
            p.Description = desc;
            p.Schema ??= new OpenApiSchema();
            if (!string.IsNullOrEmpty(format)) p.Schema.Format = format;
            // set example on parameter (OpenAPI supports Example on schema)
            p.Example = example;
            if (p.Schema != null && p.Schema.Example == null) p.Schema.Example = example;
        }

        SetParam("include_deleted", "Set this `true` to include deleted reservations. Not set or anything else does not return deleted reservations.", new OpenApiBoolean(false));
        SetParam("room_id", "filter the returned reservation by the given room", new OpenApiString("510dcb67-36f6-4c1c-9133-1072c31efb92"));
        SetParam("before", "filter for reservations where `query:before > reservation:from` (use ISO date format, e.g. 2025-12-01)", new OpenApiString("2025-12-01"), "date");
        SetParam("after", "filter for reservations where `query:after < reservation:to` (use ISO date format, e.g. 2025-12-01)", new OpenApiString("2025-12-02"), "date");
    }
}

