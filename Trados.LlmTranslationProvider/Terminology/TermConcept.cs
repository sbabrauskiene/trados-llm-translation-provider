using System.Collections.Generic;

namespace Trados.LlmTranslationProvider.Terminology
{
    /// <summary>
    /// A single terminology concept as modeled by TBX: one idea that may be expressed by several
    /// interchangeable source-language synonyms, and translated by one or more target-language
    /// terms (some of which may be flagged "preferred" over others).
    /// </summary>
    public class TermConcept
    {
        public string ConceptId { get; set; }

        public List<string> SourceTerms { get; } = new List<string>();

        public List<TermCandidate> TargetTerms { get; } = new List<TermCandidate>();

        /// <summary>The preferred target term if one is flagged, otherwise null.</summary>
        public TermCandidate PreferredTarget
        {
            get
            {
                foreach (var candidate in TargetTerms)
                {
                    if (candidate.IsPreferred)
                    {
                        return candidate;
                    }
                }

                return null;
            }
        }
    }

    /// <summary>A single target-language term for a concept, with its preference status.</summary>
    public class TermCandidate
    {
        public string Text { get; set; }

        public bool IsPreferred { get; set; }
    }
}
