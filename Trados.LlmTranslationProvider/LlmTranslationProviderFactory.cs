using System;
using Sdl.LanguagePlatform.TranslationMemoryApi;

namespace Trados.LlmTranslationProvider
{
    /// <summary>
    /// Registers the LLM translation provider with Trados Studio's plug-in framework and creates
    /// instances of it on demand. Modeled directly on RWS's documented ListTranslationProvider
    /// sample factory (see "Instantiating the Plug-in" in the Trados Studio API docs).
    /// </summary>
    [TranslationProviderFactory(
        Id = "LlmTranslationProviderFactory",
        Name = "LLM Translation Provider",
        Description = "Translates using an LLM (OpenAI) with TBX termbase enforcement.")]
    public class LlmTranslationProviderFactory : ITranslationProviderFactory
    {
        public ITranslationProvider CreateTranslationProvider(
            Uri translationProviderUri,
            string translationProviderState,
            ITranslationProviderCredentialStore credentialStore)
        {
            if (!SupportsTranslationProviderUri(translationProviderUri))
            {
                throw new ArgumentException("Cannot handle URI: " + translationProviderUri, nameof(translationProviderUri));
            }

            var options = new LlmTranslationOptions(translationProviderUri);
            return new LlmTranslationProvider(options);
        }

        public bool SupportsTranslationProviderUri(Uri translationProviderUri)
        {
            if (translationProviderUri == null)
            {
                throw new ArgumentNullException(nameof(translationProviderUri));
            }

            return string.Equals(
                translationProviderUri.Scheme,
                LlmTranslationOptions.UriScheme,
                StringComparison.OrdinalIgnoreCase);
        }

        public TranslationProviderInfo GetTranslationProviderInfo(Uri translationProviderUri, string translationProviderState)
        {
            return new TranslationProviderInfo
            {
                Name = "LLM Translation Provider",
                TranslationMethod = TranslationMethod.MachineTranslation
            };
        }
    }
}
