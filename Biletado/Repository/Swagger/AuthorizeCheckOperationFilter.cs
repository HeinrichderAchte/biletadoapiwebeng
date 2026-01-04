using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Biletado.Repository.Swagger;

public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation == null || context == null || context.MethodInfo == null) return;

        var methodInfo = context.MethodInfo;
        var hasAuthorize = methodInfo.DeclaringType != null && methodInfo.DeclaringType.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any()
                           || methodInfo.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any();

        var allowAnonymous = methodInfo.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>().Any()
                                || (methodInfo.DeclaringType != null && methodInfo.DeclaringType.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>().Any());

        if (!hasAuthorize || allowAnonymous) return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();

        var bearerScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearer" }
        };

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [ bearerScheme ] = new string[] { }
        });
    }
}

