using System.Collections.Generic;
using System.Text;
using Trados.LlmTranslationProvider.Terminology;

namespace Trados.LlmTranslationProvider.Llm
{
    /// <summary>A single source/target pair pulled from a project TM, used as a few-shot example.</summary>
    public class TmExample
    {
        public string Source { get; set; }

        public string Target { get; set; }
    }

    /// <summary>
    /// Builds the system and user prompts for a single segment translation request.
    ///
    /// Design (see project plan): placeholder-preservation and terminology rules live in the
    /// system prompt, since they apply uniformly to every call for a given provider instance.
    /// TM few-shot examples and the actual source segment go in the user prompt, since they
    /// change per call. The model is instructed to return only the translated text, so the
    /// caller does not need to parse structured output.
    /// </summary>
    public static class PromptBuilder
    {
        public static string BuildSystemPrompt(
            string sourceLanguageName,
            string targetLanguageName,
            IReadOnlyList<TermConcept> matchedTerms,
            string customInstructions)
        {
            var sb = new StringBuilder();

            sb.Append("You are a professional ").Append(sourceLanguageName)
              .Append("-to-").Append(targetLanguageName)
              .AppendLine(" translator working inside a CAT tool.");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("1. Translate the given segment naturally and accurately, preserving meaning, register, and tone.");
            sb.AppendLine("2. The segment may contain numbered placeholders like {1}, {2} representing inline formatting");
            sb.AppendLine("   tags. Reproduce every placeholder EXACTLY as written, unchanged, the same number of times,");
            sb.AppendLine("   positioned where the corresponding formatted text would naturally fall in the translation.");
            sb.AppendLine("   Never translate, remove, merge, or invent placeholders.");

            if (matchedTerms != null && matchedTerms.Count > 0)
            {
                sb.AppendLine("3. Terminology constraints for this segment must be honored:");
                foreach (var concept in matchedTerms)
                {
                    AppendTerminologyConstraint(sb, concept);
                }
                sb.AppendLine("4. Output ONLY the translated segment text - no explanations, no quotation marks, no notes.");
            }
            else
            {
                sb.AppendLine("3. Output ONLY the translated segment text - no explanations, no quotation marks, no notes.");
            }

            if (!string.IsNullOrWhiteSpace(customInstructions))
            {
                sb.AppendLine();
                sb.AppendLine(customInstructions.Trim());
            }

            return sb.ToString();
        }

        public static string BuildUserPrompt(string placeholderSourceText, IReadOnlyList<TmExample> tmExamples)
        {
            var sb = new StringBuilder();

            if (tmExamples != null && tmExamples.Count > 0)
            {
                sb.AppendLine("Reference translations from this project's translation memory (for consistency, not verbatim reuse):");
                foreach (var example in tmExamples)
                {
                    sb.Append("- SRC: \"").Append(example.Source).Append("\" -> TGT: \"").Append(example.Target).AppendLine("\"");
                }
                sb.AppendLine();
            }

            sb.AppendLine("Translate this segment:");
            sb.Append(placeholderSourceText);

            return sb.ToString();
        }

        private static void AppendTerminologyConstraint(StringBuilder sb, TermConcept concept)
        {
            // Use the first source synonym for readability in the prompt; any of the concept's
            // synonyms is what actually triggered the match (see TbxTermbaseLoader.FindMatches).
            var sourceLabel = concept.SourceTerms.Count > 0 ? concept.SourceTerms[0] : concept.ConceptId;
            var preferred = concept.PreferredTarget;

            if (preferred != null)
            {
                // Hard constraint: exactly one preferred target term.
                sb.Append("   - \"").Append(sourceLabel).Append("\" -> must translate as \"").Append(preferred.Text).AppendLine("\"");
            }
            else if (concept.TargetTerms.Count > 0)
            {
                // Soft constraint: multiple valid targets, no single preferred one - let the model
                // pick whichever fits the sentence best, rather than forcing an awkward phrasing.
                sb.Append("   - \"").Append(sourceLabel).Append("\" -> use one of: ");
                for (var i = 0; i < concept.TargetTerms.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append('"').Append(concept.TargetTerms[i].Text).Append('"');
                }
                sb.AppendLine(" (whichever fits context best)");
            }
        }
    }
}
