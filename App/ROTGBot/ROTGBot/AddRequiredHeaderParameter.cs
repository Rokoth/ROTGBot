using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ROTGBot
{
    public class AddRequiredHeaderParameter : IOperationFilter
    {
        private const string OpenApiParameterName = "Authorization";
        private const string OpenApiParameterDescription = "access token";
        private const string OpenApiParameterType = "string";
        private const string OpenApiParameterBearerDefaultApiString = "Bearer ";

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            CheckOperation(operation);
            operation.Parameters.Add(CreateOpenApiParameter());
        }

        private static OpenApiParameter CreateOpenApiParameter() => new()
        {
            Name = OpenApiParameterName,
            In = ParameterLocation.Header,
            Description = OpenApiParameterDescription,
            Required = true,
            Schema = CreateOpenApiSchema()
        };

        private static OpenApiSchema CreateOpenApiSchema()
        {
            return new OpenApiSchema
            {
                Type = OpenApiParameterType,
                Default = new OpenApiString(OpenApiParameterBearerDefaultApiString)
            };
        }

        private static void CheckOperation(OpenApiOperation operation)
        {
            operation.Parameters ??= [];
        }
    }
}
