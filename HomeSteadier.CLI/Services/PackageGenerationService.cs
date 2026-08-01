using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace HomeSteadier.CLI.Services;

public class PackageGenerationService
{
    public async Task GenerateAsync(IConfiguration configuration)
    {
        try
        {
            var baseUrl = (configuration["Api:BaseUrl"] ?? "http://localhost:5128").TrimEnd('/');
            var openApiUrl = $"{baseUrl}/openapi/v1.json";

            Console.WriteLine($"Fetching OpenAPI document from {openApiUrl}...");

            string json;
            using (var httpClient = new HttpClient())
            {
                try
                {
                    json = await httpClient.GetStringAsync(openApiUrl);
                }
                catch (HttpRequestException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nCould not reach the API at {openApiUrl}: {ex.Message}");
                    Console.WriteLine("Make sure the API is running in Development mode (e.g. 'dotnet run --project Homesteadier.API').");
                    Console.ResetColor();
                    return;
                }
            }

            var result = OpenApiDocument.Parse(json, "json");
            if (result.Document == null || result.Diagnostic?.Errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nFailed to parse the OpenAPI document:");
                foreach (var error in result.Diagnostic?.Errors ?? [])
                    Console.WriteLine($"  {error}");
                Console.ResetColor();
                return;
            }

            var schemas = result.Document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>();
            var (requestSchemas, responseSchemas) = ClassifySchemas(result.Document, schemas);

            var requestPath = GetModelsPath("request");
            var responsePath = GetModelsPath("response");
            Directory.CreateDirectory(requestPath);
            Directory.CreateDirectory(responsePath);

            var created = new List<string>();
            var updated = new List<string>();
            var unchanged = new List<string>();

            foreach (var (folder, names) in new[] { (requestPath, requestSchemas), (responsePath, responseSchemas) })
            {
                foreach (var name in names)
                {
                    if (!schemas.TryGetValue(name, out var schema))
                        continue;

                    var content = GenerateInterface(name, schema);
                    var filePath = Path.Combine(folder, $"{name}.ts");
                    var isNew = !File.Exists(filePath);
                    var existing = isNew ? null : await File.ReadAllTextAsync(filePath);

                    if (existing == content)
                    {
                        unchanged.Add(name);
                        continue;
                    }

                    await File.WriteAllTextAsync(filePath, content);
                    (isNew ? created : updated).Add(name);
                }
            }

            var removed = new List<string>();
            foreach (var (folder, names) in new[] { (requestPath, requestSchemas), (responsePath, responseSchemas) })
            {
                foreach (var file in Directory.GetFiles(folder, "*.ts"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!names.Contains(name))
                    {
                        File.Delete(file);
                        removed.Add(name);
                    }
                }
            }

            foreach (var name in created)
                Console.WriteLine($"Created: {name}.ts");

            foreach (var name in updated)
                Console.WriteLine($"Updated: {name}.ts");

            foreach (var name in removed)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Removed: {name}.ts (no longer exposed by the API)");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nSuccessfully generated TypeScript models! ({created.Count} created, {updated.Count} updated, {unchanged.Count} unchanged)");
            Console.ResetColor();

            Console.WriteLine();
            await GenerateApiClientsAsync(result.Document);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError generating packages: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static (HashSet<string> Request, HashSet<string> Response) ClassifySchemas(
        OpenApiDocument document, IDictionary<string, IOpenApiSchema> schemas)
    {
        var requestSchemas = new HashSet<string>();
        var responseSchemas = new HashSet<string>();

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations == null)
                continue;

            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation.RequestBody?.Content != null)
                    foreach (var media in operation.RequestBody.Content.Values)
                        CollectSchemaNames(media.Schema, requestSchemas);

                if (operation.Responses == null)
                    continue;

                foreach (var response in operation.Responses.Values)
                {
                    if (response.Content == null)
                        continue;

                    foreach (var media in response.Content.Values)
                        CollectSchemaNames(media.Schema, responseSchemas);
                }
            }
        }

        // Expand transitively through nested $ref properties so every schema an
        // interface imports also gets its own generated file, colocated in the
        // same folder (avoids cross request/response folder relative imports).
        ExpandTransitively(requestSchemas, schemas);
        ExpandTransitively(responseSchemas, schemas);

        return (requestSchemas, responseSchemas);
    }

    private static void ExpandTransitively(HashSet<string> schemaNames, IDictionary<string, IOpenApiSchema> schemas)
    {
        var toProcess = new Queue<string>(schemaNames);
        while (toProcess.Count > 0)
        {
            var name = toProcess.Dequeue();
            if (!schemas.TryGetValue(name, out var schema) || schema.Properties == null)
                continue;

            foreach (var propertySchema in schema.Properties.Values)
            {
                var nested = new HashSet<string>();
                CollectSchemaNames(propertySchema, nested);
                foreach (var nestedName in nested)
                {
                    if (schemaNames.Add(nestedName))
                        toProcess.Enqueue(nestedName);
                }
            }
        }
    }

    private static void CollectSchemaNames(IOpenApiSchema? schema, HashSet<string> into)
    {
        if (schema == null)
            return;

        if (schema is OpenApiSchemaReference reference && reference.Reference?.Id != null)
        {
            into.Add(reference.Reference.Id);
            return;
        }

        if (schema.Type?.HasFlag(JsonSchemaType.Array) == true)
            CollectSchemaNames(schema.Items, into);
    }

    private static string GenerateInterface(string name, IOpenApiSchema schema)
    {
        var properties = schema.Properties ?? new Dictionary<string, IOpenApiSchema>();
        var required = schema.Required ?? new HashSet<string>();

        var imports = new SortedSet<string>();
        var propertyLines = new List<string>();

        foreach (var (propertyName, propertySchema) in properties.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var isOptional = !required.Contains(propertyName);
            var tsType = MapType(propertySchema, imports);
            propertyLines.Add($"  {ToCamelCase(propertyName)}{(isOptional ? "?" : "")}: {tsType};");
        }

        var content = new System.Text.StringBuilder();
        content.AppendLine("// Auto-generated by 'packages gen' from the API's OpenAPI document. Do not hand-edit.");
        foreach (var import in imports)
            content.AppendLine($"import type {{ {import} }} from \"./{import}\";");
        if (imports.Count > 0)
            content.AppendLine();
        content.AppendLine($"export interface {name} {{");
        foreach (var line in propertyLines)
            content.AppendLine(line);
        content.Append('}').AppendLine();

        return content.ToString();
    }

    private static string MapType(IOpenApiSchema schema, SortedSet<string> imports)
    {
        if (schema is OpenApiSchemaReference reference && reference.Reference?.Id != null)
        {
            imports.Add(reference.Reference.Id);
            return reference.Reference.Id;
        }

        var type = schema.Type;
        if (type == null)
            return "unknown";

        var isNullable = type.Value.HasFlag(JsonSchemaType.Null);

        // A schema's "type" is a flag combination (e.g. OpenAPI 3.1's ["integer", "string"]
        // union for values that may round-trip as either), so union every matching
        // primitive rather than picking just the first one that matches.
        var candidateTypes = new List<string>();

        if (type.Value.HasFlag(JsonSchemaType.Array))
            candidateTypes.Add(schema.Items != null ? $"{MapType(schema.Items, imports)}[]" : "unknown[]");
        if (type.Value.HasFlag(JsonSchemaType.String))
            candidateTypes.Add("string");
        if (type.Value.HasFlag(JsonSchemaType.Integer) || type.Value.HasFlag(JsonSchemaType.Number))
            candidateTypes.Add("number");
        if (type.Value.HasFlag(JsonSchemaType.Boolean))
            candidateTypes.Add("boolean");

        if (candidateTypes.Count == 0)
            candidateTypes.Add("unknown");

        if (isNullable)
            candidateTypes.Add("null");

        return string.Join(" | ", candidateTypes.Distinct());
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
            return propertyName;

        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    private static string GetModelsPath(string subfolder)
    {
        var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
        return Path.Combine(solutionRoot, "ReactApp", "src", "models", subfolder);
    }

    private static string GetApiClientsPath()
    {
        var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
        return Path.Combine(solutionRoot, "ReactApp", "src", "api");
    }

    private record ApiOperation(string MethodName, string Route, HttpMethod HttpMethod, OpenApiOperation Operation);

    private static async Task GenerateApiClientsAsync(OpenApiDocument document)
    {
        var apiClientsPath = GetApiClientsPath();
        Directory.CreateDirectory(apiClientsPath);

        var grouped = GroupOperationsByTag(document);

        var created = new List<string>();
        var updated = new List<string>();
        var unchanged = new List<string>();

        foreach (var (tag, operations) in grouped)
        {
            var className = $"{tag}Api";
            var content = GenerateApiClient(tag, operations);
            var filePath = Path.Combine(apiClientsPath, $"{className}.tsx");
            var isNew = !File.Exists(filePath);
            var existing = isNew ? null : await File.ReadAllTextAsync(filePath);

            if (existing == content)
            {
                unchanged.Add(className);
                continue;
            }

            await File.WriteAllTextAsync(filePath, content);
            (isNew ? created : updated).Add(className);
        }

        var expectedFileNames = grouped.Keys.Select(tag => $"{tag}Api").ToHashSet();
        var removed = new List<string>();
        foreach (var file in Directory.GetFiles(apiClientsPath, "*Api.tsx"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!expectedFileNames.Contains(name))
            {
                File.Delete(file);
                removed.Add(name);
            }
        }

        foreach (var name in created)
            Console.WriteLine($"Created: {name}.tsx");

        foreach (var name in updated)
            Console.WriteLine($"Updated: {name}.tsx");

        foreach (var name in removed)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Removed: {name}.tsx (controller no longer exposed by the API)");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated TypeScript API clients! ({created.Count} created, {updated.Count} updated, {unchanged.Count} unchanged)");
        Console.ResetColor();
    }

    private static SortedDictionary<string, List<ApiOperation>> GroupOperationsByTag(OpenApiDocument document)
    {
        var grouped = new SortedDictionary<string, List<ApiOperation>>(StringComparer.Ordinal);

        foreach (var (route, pathItem) in document.Paths)
        {
            if (pathItem.Operations == null)
                continue;

            foreach (var (httpMethod, operation) in pathItem.Operations)
            {
                var tag = operation.Tags?.FirstOrDefault()?.Name;
                if (string.IsNullOrEmpty(tag))
                    continue;

                // Reliable thanks to the OperationId operation transformer registered in
                // Homesteadier.API's AddOpenApi() call, which sets it to the controller
                // action name. This fallback only kicks in if that ever regresses.
                var methodName = string.IsNullOrEmpty(operation.OperationId)
                    ? $"{httpMethod.Method}{route.Replace("/", "_")}"
                    : operation.OperationId;

                if (!grouped.TryGetValue(tag, out var list))
                    grouped[tag] = list = [];

                list.Add(new ApiOperation(methodName, route, httpMethod, operation));
            }
        }

        return grouped;
    }

    private static string GenerateApiClient(string tag, List<ApiOperation> operations)
    {
        var className = $"{tag}Api";
        var clientClassName = $"{tag}ApiClient";

        var requestImports = new SortedSet<string>();
        var responseImports = new SortedSet<string>();
        var methodBlocks = operations
            .OrderBy(o => o.MethodName, StringComparer.Ordinal)
            .Select(op => GenerateMethod(op, requestImports, responseImports))
            .ToList();

        var content = new System.Text.StringBuilder();
        content.AppendLine("// Auto-generated by 'packages gen' from the API's OpenAPI document. Do not hand-edit.");
        content.AppendLine("import { httpClient } from \"./httpClient\";");
        foreach (var import in requestImports)
            content.AppendLine($"import type {{ {import} }} from \"../models/request/{import}\";");
        foreach (var import in responseImports)
            content.AppendLine($"import type {{ {import} }} from \"../models/response/{import}\";");
        content.AppendLine();
        content.AppendLine($"class {clientClassName} {{");
        for (var i = 0; i < methodBlocks.Count; i++)
        {
            content.Append(methodBlocks[i]);
            if (i < methodBlocks.Count - 1)
                content.AppendLine();
        }
        content.AppendLine("}");
        content.AppendLine();
        content.AppendLine($"export const {className} = new {clientClassName}();");

        return content.ToString();
    }

    private static string GenerateMethod(ApiOperation op, SortedSet<string> requestImports, SortedSet<string> responseImports)
    {
        var operation = op.Operation;
        var parameters = operation.Parameters ?? [];
        var pathParams = parameters.Where(p => p.In == ParameterLocation.Path).ToList();
        var queryParams = parameters.Where(p => p.In == ParameterLocation.Query).ToList();

        // Path/query parameter schemas are almost always primitives (route segments,
        // filter values); a $ref here would point at a schema ClassifySchemas never
        // saw (it only inspects request/response bodies), so there's no generated
        // model file to import. We still resolve the TS type name via MapType, we
        // just never wire it into an import statement.
        var paramImports = new SortedSet<string>();

        var parameterDeclarations = new List<string>();

        foreach (var param in pathParams)
        {
            var tsType = param.Schema != null ? MapType(param.Schema, paramImports) : "string";
            parameterDeclarations.Add($"{ToCamelCase(param.Name!)}: {tsType}");
        }

        string? requestBodyParamName = null;
        var bodySchema = operation.RequestBody?.Content?.Values.FirstOrDefault()?.Schema;
        if (bodySchema != null)
        {
            var requestBodyType = MapType(bodySchema, requestImports);
            requestBodyParamName = "request";
            parameterDeclarations.Add($"{requestBodyParamName}: {requestBodyType}");
        }

        var queryParamDeclarations = queryParams
            .Select(param =>
            {
                var tsType = param.Schema != null ? MapType(param.Schema, paramImports) : "string";
                var optional = param.Required ? "" : "?";
                return $"{ToCamelCase(param.Name!)}{optional}: {tsType}";
            })
            .ToList();

        string? queryParamName = null;
        if (queryParamDeclarations.Count > 0)
        {
            queryParamName = "query";
            parameterDeclarations.Add($"{queryParamName}: {{ {string.Join("; ", queryParamDeclarations)} }}");
        }

        string? responseType = null;
        foreach (var (statusCode, response) in operation.Responses ?? new OpenApiResponses())
        {
            if (!statusCode.StartsWith('2'))
                continue;

            var schema = response.Content?.Values.FirstOrDefault()?.Schema;
            if (schema == null)
                continue;

            responseType = MapType(schema, responseImports);
            break;
        }

        var returnType = responseType ?? "void";
        var httpVerb = op.HttpMethod.Method.ToLowerInvariant();
        var route = BuildRouteExpression(op.Route, pathParams);

        var axiosArgs = new List<string> { route };
        var configParts = new List<string>();

        if (requestBodyParamName != null)
        {
            if (httpVerb is "post" or "put" or "patch")
                axiosArgs.Add(requestBodyParamName);
            else
                configParts.Add($"data: {requestBodyParamName}");
        }
        else if (httpVerb is "post" or "put" or "patch")
        {
            axiosArgs.Add("undefined");
        }

        if (queryParamName != null)
            configParts.Add($"params: {queryParamName}");

        if (configParts.Count > 0)
            axiosArgs.Add($"{{ {string.Join(", ", configParts)} }}");

        var callExpression = responseType != null
            ? $"httpClient.{httpVerb}<{responseType}>({string.Join(", ", axiosArgs)})"
            : $"httpClient.{httpVerb}({string.Join(", ", axiosArgs)})";

        var body = new System.Text.StringBuilder();
        body.AppendLine($"  async {op.MethodName}({string.Join(", ", parameterDeclarations)}): Promise<{returnType}> {{");
        if (responseType != null)
        {
            body.AppendLine($"    const response = await {callExpression};");
            body.AppendLine("    return response.data;");
        }
        else
        {
            body.AppendLine($"    await {callExpression};");
        }
        body.AppendLine("  }");

        return body.ToString();
    }

    private static string BuildRouteExpression(string route, List<IOpenApiParameter> pathParams)
    {
        if (pathParams.Count == 0)
            return $"\"{route}\"";

        var interpolated = route;
        foreach (var param in pathParams)
            interpolated = interpolated.Replace($"{{{param.Name}}}", $"${{{ToCamelCase(param.Name!)}}}");

        return $"`{interpolated}`";
    }

    private static string FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HomeSteadier.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return startPath;
    }
}
