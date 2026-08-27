using System;
using System.Collections.Generic;
using Sdl.Core.Globalization;
using Sdl.LanguagePlatform.Core;
using Sdl.LanguagePlatform.TranslationMemoryApi;

namespace Trados.LlmTranslationProvider
{
    /// <summary>
    /// Represents one configured instance of the LLM translation engine (a given model + termbase
    /// + prompt combination).
    ///
    /// Inherits from <see cref="AbstractMachineTranslationProvider"/> - RWS's own base class for
    /// MT-style providers - rather than implementing <see cref="ITranslationProvider"/> directly.
    /// This gives sensible defaults for everything (IsReadOnly=true, SupportsUpdate=false,
    /// TranslationMethod=MachineTranslation, etc.) and leaves only a handful of members that must
    /// actually be implemented here.
    /// </summary>
    public class LlmTranslationProvider : AbstractMachineTranslationProvider
    {
        public LlmTranslationOptions Options { get; set; }

        public LlmTranslationProvider(LlmTranslationOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override string Name => "LLM Translation Provider (" + Options.Model + ")";

        public override Uri Uri => Options.ToUri();

        /// <summary>
        /// The LLM can attempt any language pair the configured model supports, so we don't
        /// maintain an explicit list - this returns empty and relies on
        /// <see cref="SupportsLanguageDirection"/> (which always returns true) for the actual
        /// language-direction check Trados Studio performs.
        /// </summary>
        public override IList<LanguagePair> SupportedLanguageDirections => new List<LanguagePair>();

        public override bool SupportsLanguageDirection(LanguagePair languageDirection)
        {
            return true;
        }

        public override ITranslationProviderLanguageDirection GetLanguageDirection(LanguagePair languageDirection)
        {
            return new LlmTranslationProviderLanguageDirection(this, languageDirection);
        }

        protected override ProviderStatusInfo GetStatusInfo()
        {
            // No live connection/session to check - each translation request is a self-contained
            // HTTP call, so the provider is always considered ready.
            return new ProviderStatusInfo(true, "Available");
        }
    }
}
