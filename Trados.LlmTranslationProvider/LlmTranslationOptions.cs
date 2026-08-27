using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Trados.LlmTranslationProvider
{
    /// <summary>
    /// Holds all non-secret settings for a configured instance of the LLM translation provider.
    /// These are serialized into the provider's <see cref="Uri"/> (the mechanism Trados Studio
    /// itself uses to persist translation provider configuration in .sdlproj/.sdltpl files), so
    /// they travel with the project. The API key is deliberately NOT stored here - see
    /// <see cref="Security.ApiKeyStore"/> - because provider URIs can end up in project packages
    /// that get shared with other people.
    /// </summary>
    public class LlmTranslationOptions
    {
        /// <summary>
        /// Unique URI scheme identifying this provider. Trados Studio matches translation
        /// provider URIs against this scheme (see LlmTranslationProviderFactory.SupportsTranslationProviderUri)
        /// to decide which factory should handle a given provider entry.
        /// </summary>
        public const string UriScheme = "llmtranslationprovider";

        public string Model { get; set; } = "gpt-4.1";

        /// <summary>Absolute path to a TBX termbase export. May be empty if no termbase is configured.</summary>
        public string TermbasePath { get; set; } = string.Empty;

        /// <summary>
        /// Absolute path to a plain-text file containing a custom system prompt template.
        /// If empty, <see cref="Llm.PromptBuilder"/> falls back to its built-in default template.
        /// </summary>
        public string PromptTemplatePath { get; set; } = string.Empty;

        /// <summary>
        /// When true, and a translation memory is attached to the same project, up to two of the
        /// best fuzzy TM matches for the current segment are included in the prompt as few-shot
        /// examples. NOTE: wiring this up to an actual TM lookup is not yet implemented in
        /// LlmTranslationProviderLanguageDirection - see the TODO there.
        /// </summary>
        public bool UseTranslationMemoryContext { get; set; }

        public LlmTranslationOptions()
        {
        }

        public LlmTranslationOptions(Uri providerUri)
        {
            if (providerUri == null)
            {
                throw new ArgumentNullException(nameof(providerUri));
            }

            var query = ParseQuery(providerUri.Query);

            if (query.TryGetValue("model", out var model) && !string.IsNullOrEmpty(model))
            {
                Model = model;
            }

            if (query.TryGetValue("terms", out var terms))
            {
                TermbasePath = terms;
            }

            if (query.TryGetValue("prompt", out var prompt))
            {
                PromptTemplatePath = prompt;
            }

            if (query.TryGetValue("useTm", out var useTm))
            {
                UseTranslationMemoryContext = string.Equals(useTm, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Builds the provider URI that Trados Studio will store in the project. Only
        /// non-secret settings are included - see the class remarks.
        /// </summary>
        public Uri ToUri()
        {
            var parts = new List<string>
            {
                "model=" + Uri.EscapeDataString(Model ?? string.Empty),
                "terms=" + Uri.EscapeDataString(TermbasePath ?? string.Empty),
                "prompt=" + Uri.EscapeDataString(PromptTemplatePath ?? string.Empty),
                "useTm=" + (UseTranslationMemoryContext ? "true" : "false")
            };

            var uriString = UriScheme + ":///?" + string.Join("&", parts);
            return new Uri(uriString);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            var trimmed = query.TrimStart('?');
            foreach (var pair in trimmed.Split('&'))
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                var kv = pair.Split(new[] { '=' }, 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}
