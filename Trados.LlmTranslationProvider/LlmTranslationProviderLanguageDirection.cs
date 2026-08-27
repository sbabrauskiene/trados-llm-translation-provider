using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Sdl.Core.Globalization;
using Sdl.LanguagePlatform.Core;
using Sdl.LanguagePlatform.TranslationMemory;
using Sdl.LanguagePlatform.TranslationMemoryApi;
using Trados.LlmTranslationProvider.Llm;
using Trados.LlmTranslationProvider.Security;
using Trados.LlmTranslationProvider.Tagging;
using Trados.LlmTranslationProvider.Terminology;

namespace Trados.LlmTranslationProvider
{
    /// <summary>
    /// Performs the actual segment-by-segment translation work for one language direction.
    ///
    /// Inherits from <see cref="AbstractMachineTranslationProviderLanguageDirection"/>, RWS's own
    /// base class for MT-style providers, which supplies default (safe, no-op) implementations
    /// for every add/update/remove member of ITranslationProviderLanguageDirection and for the
    /// SearchResult/SearchResults construction boilerplate. The only method that needs a real
    /// implementation is <see cref="SearchMultipleSegmentsInternal"/>.
    /// </summary>
    public class LlmTranslationProviderLanguageDirection : AbstractMachineTranslationProviderLanguageDirection
    {
        private readonly LlmTranslationProvider _provider;
        private readonly LanguagePair _languageDirection;
        private readonly ILlmClient _llmClient;

        // Termbase concepts are loaded once per language direction instance and reused for every
        // segment - re-parsing the TBX file on every single segment lookup would be wasteful.
        private List<TermConcept> _termConcepts;
        private bool _termsLoaded;

        public LlmTranslationProviderLanguageDirection(LlmTranslationProvider provider, LanguagePair languageDirection)
            : base(provider, languageDirection)
        {
            _provider = provider;
            _languageDirection = languageDirection;
            _llmClient = new OpenAiClient();
        }

        /// <summary>
        /// The one method RWS's abstract base class requires us to implement. Called for both
        /// single-segment interactive lookups and batch (Pretranslate/Analyze) requests - the
        /// base class's default SearchSegment/SearchSegmentsMasked implementations both funnel
        /// through here, per the project plan's batch cost control design.
        /// </summary>
        protected override IList<SearchResults> SearchMultipleSegmentsInternal(SearchSettings settings, IList<Segment> segments)
        {
            var options = _provider.Options;
            var apiKey = ApiKeyStore.Load(ApiKeyStore.OpenAiKeyId);

            var terms = GetTermConcepts(options);
            var customInstructions = LoadCustomInstructions(options.PromptTemplatePath);

            var translations = new List<Segment>(segments.Count);

            foreach (var segment in segments)
            {
                translations.Add(TranslateOneSegment(segment, options, apiKey, terms, customInstructions));
            }

            return CreateSearchResultsFromTranslations(segments, translations);
        }

        private Segment TranslateOneSegment(
            Segment segment,
            LlmTranslationOptions options,
            string apiKey,
            IReadOnlyList<TermConcept> allTerms,
            string customInstructions)
        {
            var placeholderText = SegmentTaggingHelper.ToPlaceholderText(segment, out var tags);

            try
            {
                var matchedTerms = TbxTermbaseLoader.FindMatches(allTerms, placeholderText);

                var systemPrompt = PromptBuilder.BuildSystemPrompt(
                    SourceLanguageDisplayName(),
                    TargetLanguageDisplayName(),
                    matchedTerms,
                    customInstructions);

                // TODO: wire up real TM fuzzy-match retrieval here when
                // options.UseTranslationMemoryContext is true (see LlmTranslationOptions remarks).
                var userPrompt = PromptBuilder.BuildUserPrompt(placeholderText, tmExamples: null);

                var request = new LlmTranslationRequest
                {
                    SystemPrompt = systemPrompt,
                    UserPrompt = userPrompt,
                    Model = options.Model,
                    ApiKey = apiKey
                };

                // ITranslationProviderLanguageDirection is a synchronous interface, so we block on
                // the async HTTP call here rather than exposing async all the way through.
                var responseText = _llmClient.TranslateAsync(request).GetAwaiter().GetResult();

                var targetSegment = SegmentTaggingHelper.ToTargetSegment(
                    responseText,
                    tags,
                    _languageDirection.TargetCulture,
                    out var tagsRoundTripped);

                if (!tagsRoundTripped)
                {
                    // The LLM dropped/duplicated a placeholder - safer to surface the original,
                    // correctly-tagged source text than a translation with broken formatting.
                    return segment.Duplicate();
                }

                return targetSegment;
            }
            catch (Exception)
            {
                // Network/API failure for this segment: fall back to the untranslated source
                // rather than letting one bad segment break an entire batch pretranslate run.
                // TODO: surface this failure somewhere visible (e.g. Trados Studio's output log)
                // instead of silently swallowing it.
                return segment.Duplicate();
            }
        }

        private List<TermConcept> GetTermConcepts(LlmTranslationOptions options)
        {
            if (_termsLoaded)
            {
                return _termConcepts;
            }

            _termsLoaded = true;

            if (string.IsNullOrEmpty(options.TermbasePath) || !File.Exists(options.TermbasePath))
            {
                _termConcepts = new List<TermConcept>();
                return _termConcepts;
            }

            try
            {
                _termConcepts = TbxTermbaseLoader.Load(
                    options.TermbasePath,
                    _languageDirection.SourceCulture.TwoLetterISOLanguageName,
                    _languageDirection.TargetCulture.TwoLetterISOLanguageName);
            }
            catch (Exception)
            {
                // Malformed/unreadable termbase file: proceed without terminology enforcement
                // rather than breaking translation entirely.
                _termConcepts = new List<TermConcept>();
            }

            return _termConcepts;
        }

        private static string LoadCustomInstructions(string promptTemplatePath)
        {
            if (string.IsNullOrEmpty(promptTemplatePath) || !File.Exists(promptTemplatePath))
            {
                return null;
            }

            try
            {
                return File.ReadAllText(promptTemplatePath);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private string SourceLanguageDisplayName()
        {
            return _languageDirection.SourceCulture.EnglishName;
        }

        private string TargetLanguageDisplayName()
        {
            return _languageDirection.TargetCulture.EnglishName;
        }
    }
}
