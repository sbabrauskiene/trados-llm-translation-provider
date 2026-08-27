using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Trados.LlmTranslationProvider.Terminology
{
    /// <summary>
    /// Parses a TBX (TermBase eXchange) export into <see cref="TermConcept"/> objects, and matches
    /// concepts against segment text. TBX groups terms by concept (termEntry), which is what lets
    /// us represent many-to-many entries: multiple source-language synonyms mapping to multiple
    /// valid target-language terms - see the project plan's "Termbase enforcement" section.
    ///
    /// This targets the common TBX-Basic structure (martif/text/body/termEntry/langSet/tig/term,
    /// with optional termNote elements indicating preferred/admitted status). If your export uses
    /// a different TBX dialect and terms aren't being picked up, share a sample file and this can
    /// be adjusted.
    /// </summary>
    public static class TbxTermbaseLoader
    {
        private static readonly XNamespace XmlNs = "http://www.w3.org/XML/1998/namespace";

        public static List<TermConcept> Load(string tbxFilePath, string sourceLanguageTag, string targetLanguageTag)
        {
            if (string.IsNullOrEmpty(tbxFilePath))
            {
                return new List<TermConcept>();
            }

            var sourcePrimary = PrimarySubtag(sourceLanguageTag);
            var targetPrimary = PrimarySubtag(targetLanguageTag);

            var doc = XDocument.Load(tbxFilePath);
            var concepts = new List<TermConcept>();

            foreach (var termEntry in doc.Descendants().Where(e => e.Name.LocalName == "termEntry"))
            {
                var concept = new TermConcept
                {
                    ConceptId = (string)termEntry.Attribute("id")
                };

                foreach (var langSet in termEntry.Elements().Where(e => e.Name.LocalName == "langSet"))
                {
                    var lang = (string)langSet.Attribute(XmlNs + "lang");
                    var primary = PrimarySubtag(lang);
                    if (string.IsNullOrEmpty(primary))
                    {
                        continue;
                    }

                    var isSource = string.Equals(primary, sourcePrimary, StringComparison.OrdinalIgnoreCase);
                    var isTarget = string.Equals(primary, targetPrimary, StringComparison.OrdinalIgnoreCase);
                    if (!isSource && !isTarget)
                    {
                        continue;
                    }

                    // Terms live inside a "tig" (term info group) or "ntig" (older TBX variant),
                    // each holding one <term> and any sibling <termNote> elements (status, etc).
                    foreach (var termGroup in langSet.Elements().Where(e => e.Name.LocalName == "tig" || e.Name.LocalName == "ntig"))
                    {
                        var termElement = termGroup.Elements().FirstOrDefault(e => e.Name.LocalName == "term");
                        var text = termElement?.Value?.Trim();
                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        if (isSource)
                        {
                            if (!concept.SourceTerms.Contains(text, StringComparer.OrdinalIgnoreCase))
                            {
                                concept.SourceTerms.Add(text);
                            }
                        }
                        else
                        {
                            var isPreferred = termGroup.Elements()
                                .Where(e => e.Name.LocalName == "termNote")
                                .Any(note => (note.Value ?? string.Empty).IndexOf("preferred", StringComparison.OrdinalIgnoreCase) >= 0);

                            concept.TargetTerms.Add(new TermCandidate { Text = text, IsPreferred = isPreferred });
                        }
                    }
                }

                if (concept.SourceTerms.Count > 0 && concept.TargetTerms.Count > 0)
                {
                    concepts.Add(concept);
                }
            }

            return concepts;
        }

        /// <summary>
        /// Returns every concept for which any source-language synonym appears in
        /// <paramref name="sourceText"/> as a whole word (case-insensitive).
        /// </summary>
        public static List<TermConcept> FindMatches(IEnumerable<TermConcept> concepts, string sourceText)
        {
            var matches = new List<TermConcept>();
            if (string.IsNullOrEmpty(sourceText) || concepts == null)
            {
                return matches;
            }

            foreach (var concept in concepts)
            {
                foreach (var synonym in concept.SourceTerms)
                {
                    if (ContainsWholeWord(sourceText, synonym))
                    {
                        matches.Add(concept);
                        break;
                    }
                }
            }

            return matches;
        }

        private static bool ContainsWholeWord(string text, string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return false;
            }

            var pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(word) + @"(?![\p{L}\p{N}])";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        private static string PrimarySubtag(string languageTag)
        {
            if (string.IsNullOrEmpty(languageTag))
            {
                return string.Empty;
            }

            var separatorIndex = languageTag.IndexOf('-');
            return separatorIndex > 0 ? languageTag.Substring(0, separatorIndex) : languageTag;
        }
    }
}
