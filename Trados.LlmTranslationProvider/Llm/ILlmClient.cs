using System.Threading.Tasks;

namespace Trados.LlmTranslationProvider.Llm
{
    /// <summary>
    /// A single translation request sent to an LLM backend. Deliberately minimal (just the two
    /// assembled prompt strings) so that any chat-completion-style API can implement
    /// <see cref="ILlmClient"/> without depending on OpenAI-specific request/response shapes.
    /// </summary>
    public class LlmTranslationRequest
    {
        public string SystemPrompt { get; set; }

        public string UserPrompt { get; set; }

        public string Model { get; set; }

        public string ApiKey { get; set; }
    }

    /// <summary>
    /// Backend-agnostic interface for calling an LLM to translate a single segment's worth of
    /// text. <see cref="Llm.OpenAiClient"/> is the first implementation; an Anthropic (or other)
    /// client can be added later by implementing this same interface, without any changes needed
    /// in <see cref="LlmTranslationProviderLanguageDirection"/>.
    /// </summary>
    public interface ILlmClient
    {
        Task<string> TranslateAsync(LlmTranslationRequest request);
    }
}
