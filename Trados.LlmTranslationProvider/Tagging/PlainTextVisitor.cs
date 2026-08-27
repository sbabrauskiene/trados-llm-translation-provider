using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sdl.LanguagePlatform.Core;

namespace Trados.LlmTranslationProvider.Tagging
{
    /// <summary>
    /// Implements <see cref="ISegmentElementVisitor"/> (the exact member set documented by RWS
    /// for the sample ListTranslationProvider plug-in) to walk a segment's elements and produce a
    /// plain-text string where every inline tag has been replaced with a numbered placeholder
    /// such as "{1}". The original <see cref="Tag"/> objects are collected in encounter order so
    /// they can be re-inserted into the translated text afterwards - see
    /// <see cref="SegmentTaggingHelper.ToTargetSegment"/>.
    ///
    /// Sending "{1}", "{2}", ... to the LLM (rather than raw tag markup) keeps prompts compact and
    /// gives the model an unambiguous, easy-to-preserve token instead of Trados's internal tag
    /// representation.
    /// </summary>
    internal class PlaceholderVisitor : ISegmentElementVisitor
    {
        private readonly StringBuilder _text = new StringBuilder();
        private readonly List<Tag> _tags = new List<Tag>();

        public string PlaceholderText => _text.ToString();

        public IReadOnlyList<Tag> Tags => _tags;

        public void Reset()
        {
            _text.Clear();
            _tags.Clear();
        }

        public void VisitDateTimeToken(Sdl.LanguagePlatform.Core.Tokenization.DateTimeToken token)
        {
            _text.Append(token.Text);
        }

        public void VisitMeasureToken(Sdl.LanguagePlatform.Core.Tokenization.MeasureToken token)
        {
            _text.Append(token.Text);
        }

        public void VisitNumberToken(Sdl.LanguagePlatform.Core.Tokenization.NumberToken token)
        {
            _text.Append(token.Text);
        }

        public void VisitSimpleToken(Sdl.LanguagePlatform.Core.Tokenization.SimpleToken token)
        {
            _text.Append(token.Text);
        }

        public void VisitTag(Tag tag)
        {
            _tags.Add(tag);
            _text.Append('{').Append(_tags.Count).Append('}');
        }

        public void VisitTagToken(Sdl.LanguagePlatform.Core.Tokenization.TagToken token)
        {
            _text.Append(token.Text);
        }

        public void VisitText(Text text)
        {
            _text.Append(text);
        }
    }

    /// <summary>
    /// Helper functions built around <see cref="PlaceholderVisitor"/>: converting a source segment
    /// to placeholder text before calling the LLM, and rebuilding a tagged target segment from the
    /// LLM's response afterwards, with a strict round-trip check.
    /// </summary>
    internal static class SegmentTaggingHelper
    {
        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Converts a source segment into plain text with numbered tag placeholders.
        /// </summary>
        public static string ToPlaceholderText(Segment segment, out IReadOnlyList<Tag> tags)
        {
            var visitor = new PlaceholderVisitor();
            foreach (var element in segment.Elements)
            {
                element.AcceptSegmentElementVisitor(visitor);
            }

            tags = visitor.Tags;
            return visitor.PlaceholderText;
        }

        /// <summary>
        /// Rebuilds a target <see cref="Segment"/> from the LLM's translated text, re-inserting the
        /// original tags at the position of each "{N}" placeholder.
        /// </summary>
        /// <param name="translatedTextWithPlaceholders">The LLM's output, expected to contain the same placeholders as the source.</param>
        /// <param name="tags">The tags collected from the source segment, in original order.</param>
        /// <param name="targetCulture">Culture of the target language, used to construct the new segment.</param>
        /// <param name="tagsRoundTripped">
        /// False if the LLM dropped, duplicated, or misused a placeholder (count/index mismatch).
        /// Callers should treat this as a signal that the translation is not safe to accept as-is.
        /// </param>
        public static Segment ToTargetSegment(
            string translatedTextWithPlaceholders,
            IReadOnlyList<Tag> tags,
            CultureInfo targetCulture,
            out bool tagsRoundTripped)
        {
            var segment = new Segment(targetCulture);
            var matches = PlaceholderPattern.Matches(translatedTextWithPlaceholders ?? string.Empty);

            // A valid round-trip uses every placeholder from 1..tags.Count exactly once.
            var seenIndexes = new HashSet<int>();
            var lastPos = 0;
            var valid = true;

            foreach (Match match in matches)
            {
                if (match.Index > lastPos)
                {
                    segment.Add(translatedTextWithPlaceholders.Substring(lastPos, match.Index - lastPos));
                }

                var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (index >= 1 && index <= tags.Count && seenIndexes.Add(index))
                {
                    segment.Add(tags[index - 1]);
                }
                else
                {
                    // Unknown or duplicate placeholder - keep the literal text so nothing is
                    // silently lost, but flag the round-trip as failed.
                    segment.Add(match.Value);
                    valid = false;
                }

                lastPos = match.Index + match.Length;
            }

            if (lastPos < (translatedTextWithPlaceholders ?? string.Empty).Length)
            {
                segment.Add(translatedTextWithPlaceholders.Substring(lastPos));
            }

            if (seenIndexes.Count != tags.Count)
            {
                // The LLM dropped one or more tags entirely.
                valid = false;
            }

            tagsRoundTripped = valid;
            return segment;
        }
    }
}
