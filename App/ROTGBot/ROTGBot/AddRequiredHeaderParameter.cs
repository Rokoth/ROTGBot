using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ROTGBot
{
    public class AddRequiredHeaderParameter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            CheckOperation(operation);
            operation.Parameters.Add(CreateOpenApiParameter());
        }

        private static OpenApiParameter CreateOpenApiParameter()
        {
            return new OpenApiParameter
            {
                Name = AddRequiredHeaderOptions.AuthorizationName,
                In = ParameterLocation.Header,
                Description = AddRequiredHeaderOptions.DefaultDescription,
                Required = true,
                Schema = CreateOpenApiSchema()
            };
        }

        private static OpenApiSchema CreateOpenApiSchema()
        {
            return new OpenApiSchema
            {
                Type = AddRequiredHeaderOptions.StringType,
                Default = new OpenApiString(AddRequiredHeaderOptions.BearerDefaultApiString)
            };
        }

        private static void CheckOperation(OpenApiOperation operation)
        {
            operation.Parameters ??= [];
        }
    }
}
