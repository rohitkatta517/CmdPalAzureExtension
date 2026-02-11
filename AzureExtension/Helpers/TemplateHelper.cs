// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Text;

namespace AzureExtension.Helpers;

public static class TemplateHelper
{
    private static readonly ConcurrentDictionary<string, string> _rawTemplateCache = new();

    public static string GetTemplatePath(string page)
    {
        return page switch
        {
            "AuthTemplate" => "Controls\\Templates\\AuthTemplate.json",
            "SavePullRequestSearch" => "Controls\\Templates\\SavePullRequestSearchTemplate.json",
            "SaveQuery" => "Controls\\Templates\\SaveQueryTemplate.json",
            "SavePipelineSearch" => "Controls\\Templates\\SavePipelineSearchTemplate.json",
            "SaveProjectSettings" => "Controls\\Templates\\SaveProjectSettingsTemplate.json",
            "SaveBoardLink" => "Controls\\Templates\\SaveBoardLinkTemplate.json",
            _ => throw new NotImplementedException($"Template for page '{page}' is not implemented."),
        };
    }

    public static string LoadTemplateJsonFromTemplateName(string templateName, Dictionary<string, string> substitutions)
    {
        var rawTemplate = _rawTemplateCache.GetOrAdd(templateName, name =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, GetTemplatePath(name));
            return File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);
        });

        return substitutions.Count > 0 ? FillInTemplate(rawTemplate, substitutions) : rawTemplate;
    }

    public static string FillInTemplate(string jsonTemplate, Dictionary<string, string> substitutions)
    {
        foreach (var substitution in substitutions)
        {
            jsonTemplate = jsonTemplate.Replace(substitution.Key, substitution.Value);
        }

        return jsonTemplate;
    }
}
