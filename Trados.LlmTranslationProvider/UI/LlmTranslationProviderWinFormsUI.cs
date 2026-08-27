using System;
using System.Windows.Forms;
using Sdl.Core.Globalization;
using Sdl.LanguagePlatform.Core;
using Sdl.LanguagePlatform.TranslationMemoryApi;

namespace Trados.LlmTranslationProvider.UI
{
    /// <summary>
    /// Provides Trados Studio's "Add" and "Settings" dialogs for this provider. Registered via
    /// <see cref="TranslationProviderWinFormsUiAttribute"/> so the plug-in manifest generator
    /// picks it up, following RWS's documented ListTranslationProvider sample exactly (see
    /// "Controlling the Plug-in User Interface" in the Trados Studio API docs).
    /// </summary>
    [TranslationProviderWinFormsUi(
        Id = "LlmTranslationProviderWinFormsUI",
        Name = "LLM Translation Provider Settings UI",
        Description = "Settings dialog for the LLM Translation Provider.")]
    public class LlmTranslationProviderWinFormsUI : ITranslationProviderWinFormsUI
    {
        public bool SupportsEditing => true;

        public string TypeName => "LLM Translation Provider";

        public string TypeDescription => "Translates segments using an LLM (OpenAI), with TBX termbase enforcement.";

        /// <summary>Called when the user clicks "Add > LLM Translation Provider..." in Trados Studio.</summary>
        public ITranslationProvider[] Browse(IWin32Window owner, LanguagePair[] languagePairs, ITranslationProviderCredentialStore credentialStore)
        {
            using (var dialog = new LlmTranslationOptionsForm(new LlmTranslationOptions()))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    return new ITranslationProvider[] { new LlmTranslationProvider(dialog.Options) };
                }
            }

            return null;
        }

        /// <summary>Called when the user clicks the "Settings..." button for an already-added provider.</summary>
        public bool Edit(IWin32Window owner, ITranslationProvider translationProvider, LanguagePair[] languagePairs, ITranslationProviderCredentialStore credentialStore)
        {
            if (!(translationProvider is LlmTranslationProvider editProvider))
            {
                return false;
            }

            using (var dialog = new LlmTranslationOptionsForm(editProvider.Options))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    editProvider.Options = dialog.Options;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// No separate credential prompt needed: the API key is managed entirely by
        /// <see cref="Security.ApiKeyStore"/> (DPAPI-encrypted, outside Trados's own credential
        /// store) via the settings form, so this is always satisfied.
        /// </summary>
        public bool GetCredentialsFromUser(IWin32Window owner, Uri translationProviderUri, string translationProviderState, ITranslationProviderCredentialStore credentialStore)
        {
            return true;
        }

        public TranslationProviderDisplayInfo GetDisplayInfo(Uri translationProviderUri, string translationProviderState)
        {
            var options = new LlmTranslationOptions(translationProviderUri);
            return new TranslationProviderDisplayInfo
            {
                Name = "LLM Translation Provider (" + options.Model + ")",
                TooltipText = "Translates using " + options.Model + " via OpenAI, with TBX termbase enforcement."
            };
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
    }
}
