using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ROTGBot
{
    public class AddRequiredHeaderParameter : IOperationFilter
    {
        private const string AuthorizationName = "Authorization";
        private const string DefaultDescription = "access token";
        private const string StringType = "string";
        private const string BearerDefaultApiString = "Bearer ";

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            CheckOperation(operation);
            operation.Parameters.Add(CreateOpenApiParameter());
        }

        private static OpenApiParameter CreateOpenApiParameter()
        {
            return new OpenApiParameter
            {
                Name = AuthorizationName,
                In = ParameterLocation.Header,
                Description = DefaultDescription,
                Required = true,
                Schema = CreateOpenApiSchema()
            };
        }

        private static OpenApiSchema CreateOpenApiSchema()
        {
            return new OpenApiSchema
            {
                Type = StringType,
                Default = new OpenApiString(BearerDefaultApiString)
            };
        }

        private static void CheckOperation(OpenApiOperation operation)
        {
            operation.Parameters ??= [];
        }
    }
}
