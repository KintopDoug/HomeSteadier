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
