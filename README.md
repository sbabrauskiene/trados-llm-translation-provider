# Trados.LlmTranslationProvider

A personal Trados Studio 2022 Translation Provider plugin that translates segments using an LLM
(OpenAI first, via a pluggable `ILlmClient` abstraction), with TBX termbase enforcement and
inline-tag-safe placeholder handling.

## Why

Free AppStore LLM plugins for Trados Studio proved unreliable (crashes tied to their
terminology-aware mode) or required a newer Studio release than what's installed. This plugin is
scoped for personal use, installed directly as a local plugin package (no AppStore involved).

## Architecture

- `LlmTranslationProviderFactory` - registers the provider with Trados Studio's plug-in framework.
- `LlmTranslationProvider` - one configured instance (model + termbase + prompt), inherits RWS's
  `AbstractMachineTranslationProvider` base class.
- `LlmTranslationProviderLanguageDirection` - does the actual per-segment translation work,
  inherits `AbstractMachineTranslationProviderLanguageDirection`.
- `Tagging/` - converts Trados inline tags to numbered placeholders (`{1}`, `{2}`) before sending
  text to the LLM, and rebuilds a tagged segment from the response, with round-trip validation.
- `Terminology/` - parses a TBX termbase export into concepts (handling many-to-many
  synonym/preferred-term relationships) and matches them against segment text.
- `Llm/` - `ILlmClient` abstraction, `OpenAiClient` implementation, and `PromptBuilder` (system
  prompt carries placeholder rules + terminology constraints; user prompt carries the segment and,
  eventually, TM few-shot examples).
- `Security/ApiKeyStore` - encrypts the OpenAI API key via Windows DPAPI; never stored in the
  provider URI or project files.

## Status / known risk areas

This was built without the ability to compile against the actual Trados Studio SDK assemblies
(developed on macOS against a Windows VM). Areas most likely to need adjustment on first build:

- Exact `Sdl.Core.PluginFramework` NuGet package version available for restore (see comments in
  the `.csproj`).
- The exact full member list of `ITranslationProviderLanguageDirection`/`ITranslationProvider` on
  your specific installed build (mitigated by inheriting RWS's `AbstractMachineTranslationProvider`
  / `AbstractMachineTranslationProviderLanguageDirection` base classes instead of implementing the
  raw interfaces, but not zero-risk).

If Visual Studio reports missing/extra interface members or NuGet restore failures, that's
expected on the first build - fix based on the exact compiler error.

## Setup

1. Open `Trados.LlmTranslationProvider.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Ensure the project's `Sdl.LanguagePlatform.*` references point at your installed
   `C:\Program Files (x86)\Trados\Trados Studio\Studio17\` folder.
4. Build - this deploys the `.sdlplugin` directly to
   `%AppData%\Trados\Trados Studio\17\Plugins\Packages\`.
5. Start Trados Studio and accept the "uncertified plug-in" prompt.
6. Configure via the provider's settings (API key, model, termbase TBX path, prompt template path).

## Not implemented yet

- Real TM fuzzy-match retrieval for few-shot prompt examples (`UseTranslationMemoryContext`).
- Anthropic/other `ILlmClient` implementations.
