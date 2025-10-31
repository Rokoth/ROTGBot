using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ROTGBot
{
    /// <summary>
    /// Параметры сваггер
    /// </summary>
    public class AddRequiredHeaderParameter : IOperationFilter
    {
        private const string AuthorizationName = "Authorization";
        private const string DefaultDescription = "access token";
        private const string StringType = "string";
        private const string BearerDefaultApiString = "Bearer ";

        /// <summary>
        /// Применить параметры
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="context"></param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            CheckOperation(operation);
            operation.Parameters.Add(CreateOpenApiParameter());
        }

        /// <summary>
        /// Создание параметра OpenApi
        /// </summary>
        /// <returns></returns>
        private static OpenApiParameter CreateOpenApiParameter() => new()
        {
            Name = AuthorizationName,
            In = ParameterLocation.Header,
            Description = DefaultDescription,
            Required = true,
            Schema = CreateOpenApiSchema()
        };

        /// <summary>
        /// Создание схемы OpenApi
        /// </summary>
        /// <returns></returns>
        private static OpenApiSchema CreateOpenApiSchema() => new()
        {
            Type = StringType,
            Default = new OpenApiString(BearerDefaultApiString)
        };

        /// <summary>
        /// Проверка параметра OpenApi
        /// </summary>
        /// <param name="operation"></param>
        private static void CheckOperation(OpenApiOperation operation) => operation.Parameters ??= [];
    }
}
