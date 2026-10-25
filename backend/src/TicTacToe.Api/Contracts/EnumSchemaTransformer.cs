using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TicTacToe.Api.Contracts;

/// <summary>
/// Makes the OpenAPI document describe enums as their string names. The generator only knows the
/// built-in JsonStringEnumConverter, not <see cref="StrictEnumConverter"/>.
/// </summary>
public static class EnumSchemaTransformer
{
    public static Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type.IsEnum)
        {
            var allowsNull = schema.Type?.HasFlag(JsonSchemaType.Null) == true;
            schema.Type = allowsNull ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            schema.Enum = Names(type);
        }

        // The generator omits "items" for arrays of nullable enums (the board), so describe them inline.
        if (context.JsonTypeInfo.ElementType is { } element && Nullable.GetUnderlyingType(element) is { IsEnum: true } elementEnum)
        {
            schema.Items = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Enum = Names(elementEnum) };
        }

        return Task.CompletedTask;
    }

    private static List<JsonNode> Names(Type enumType) => [.. Enum.GetNames(enumType).Select(name => (JsonNode)name)];
}
