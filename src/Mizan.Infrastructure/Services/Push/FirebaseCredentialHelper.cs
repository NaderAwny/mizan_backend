using System.Text.Json.Nodes;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace Mizan.Infrastructure.Services.Push;

public static class FirebaseCredentialHelper
{
    private static readonly object AppLock = new();

    public static bool TryInitializeFirebase(
        FirebaseOptions options,
        ILogger? logger,
        out string? projectId,
        out string? safeFailureReason)
    {
        projectId = null;
        safeFailureReason = null;

        if (FirebaseApp.DefaultInstance != null)
        {
            projectId = FirebaseApp.DefaultInstance.Options.ProjectId ?? options.ProjectId ?? "default";
            return true;
        }

        lock (AppLock)
        {
            if (FirebaseApp.DefaultInstance != null)
            {
                projectId = FirebaseApp.DefaultInstance.Options.ProjectId ?? options.ProjectId ?? "default";
                return true;
            }

            try
            {
                var credential = LoadCredential(options, out var extractedProjectId);
                projectId = !string.IsNullOrWhiteSpace(options.ProjectId)
                    ? options.ProjectId
                    : (!string.IsNullOrWhiteSpace(extractedProjectId) ? extractedProjectId : "unknown");

                var appOptions = new AppOptions
                {
                    Credential = credential,
                    ProjectId = projectId
                };

                FirebaseApp.Create(appOptions);
                return true;
            }
            catch (FileNotFoundException ex)
            {
                safeFailureReason = $"Service account file not found: {Path.GetFileName(ex.FileName ?? "unknown")}";
                logger?.LogWarning("Firebase initialization skipped: {Reason}", safeFailureReason);
                return false;
            }
            catch (Exception ex)
            {
                // Never include secret values in the failure reason
                safeFailureReason = $"Invalid credentials or initialization error ({ex.GetType().Name}: {ex.Message})";
                logger?.LogWarning(ex, "Firebase initialization failed: {Reason}", safeFailureReason);
                return false;
            }
        }
    }

    public static GoogleCredential LoadCredential(FirebaseOptions options, out string? extractedProjectId)
    {
        extractedProjectId = null;

        // 1. ServiceAccountJson (Raw JSON from environment variable or config)
        if (!string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            var rawJson = options.ServiceAccountJson.Trim();

            // If wrapped in outer quotes (from some env var systems), strip them
            if (rawJson.Length >= 2 && rawJson.StartsWith('"') && rawJson.EndsWith('"'))
            {
                rawJson = rawJson[1..^1].Replace("\\\"", "\"");
            }
            else if (rawJson.Length >= 2 && rawJson.StartsWith('\'') && rawJson.EndsWith('\''))
            {
                rawJson = rawJson[1..^1];
            }

            var normalizedJson = NormalizeServiceAccountJson(rawJson, out extractedProjectId);
#pragma warning disable CS0618
            return GoogleCredential.FromJson(normalizedJson);
#pragma warning restore CS0618
        }

        // 2. ServiceAccountPath (Local file path)
        if (!string.IsNullOrWhiteSpace(options.ServiceAccountPath))
        {
            var resolvedPath = ResolvePath(options.ServiceAccountPath);
            if (!File.Exists(resolvedPath))
            {
                // Safe check for common default external secret location as fallback
                const string externalFallback = @"C:\secrets\mizan-firebase.json";
                if (File.Exists(externalFallback))
                {
                    resolvedPath = externalFallback;
                }
                else
                {
                    throw new FileNotFoundException("Service account file not found", resolvedPath);
                }
            }

            var fileContent = File.ReadAllText(resolvedPath);
            var normalizedJson = NormalizeServiceAccountJson(fileContent, out extractedProjectId);
#pragma warning disable CS0618
            return GoogleCredential.FromJson(normalizedJson);
#pragma warning restore CS0618
        }

        // 3. Fallback to Application Default Credentials
        var adc = GoogleCredential.GetApplicationDefault();
        extractedProjectId = options.ProjectId;
        return adc;
    }

    public static string NormalizeServiceAccountJson(string json, out string? projectId)
    {
        projectId = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        // Pre-processing: If private_key has unescaped literal newlines inside quotes, convert them to \n
        var sanitized = FixUnescapedNewlinesInPrivateKey(json);

        try
        {
            var node = JsonNode.Parse(sanitized);
            if (node is JsonObject obj)
            {
                if (obj.TryGetPropertyValue("project_id", out var pidNode) && pidNode != null)
                {
                    projectId = pidNode.ToString();
                }

                if (obj.TryGetPropertyValue("private_key", out var pkNode) && pkNode != null)
                {
                    var pkString = pkNode.ToString();
                    // Handle double-escaped newlines: e.g. literal "\n" strings in the PEM block
                    if (pkString.Contains(@"\n"))
                    {
                        pkString = pkString.Replace(@"\r\n", "\n").Replace(@"\n", "\n");
                        obj["private_key"] = pkString;
                    }
                }

                return obj.ToJsonString();
            }
        }
        catch
        {
            // If JsonNode parsing fails, return sanitized and let GoogleCredential report the error
        }

        return sanitized;
    }

    private static string FixUnescapedNewlinesInPrivateKey(string json)
    {
        // Check if there is an unescaped multiline private_key:
        // If literal \r or \n exist inside the quoted string, replace them with \n
        return System.Text.RegularExpressions.Regex.Replace(
            json,
            @"""private_key""\s*:\s*""(?<key>[^""]*?)""",
            match =>
            {
                var keyContent = match.Groups["key"].Value;
                if (keyContent.Contains('\r') || keyContent.Contains('\n'))
                {
                    var fixedKey = keyContent.Replace("\r\n", @"\n").Replace("\r", @"\n").Replace("\n", @"\n");
                    return $@"""private_key"": ""{fixedKey}""";
                }
                return match.Value;
            },
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var basePath = AppContext.BaseDirectory;
        var combined = Path.Combine(basePath, path);
        if (File.Exists(combined))
        {
            return combined;
        }

        var currentDirCombined = Path.Combine(Directory.GetCurrentDirectory(), path);
        if (File.Exists(currentDirCombined))
        {
            return currentDirCombined;
        }

        // Walk up directory tree from current directory and AppContext.BaseDirectory
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, path);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        return combined;
    }
}
