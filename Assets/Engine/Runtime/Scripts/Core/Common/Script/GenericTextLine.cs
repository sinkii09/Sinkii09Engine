using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Common.Script
{
    /// <summary>
    /// Represents parsed metadata for generic text lines
    /// </summary>
    [Serializable]
    public class TextLineMetadata
    {
        /// <summary>
        /// Type of text content (speech, thought, narration, comment, label)
        /// </summary>
        public TextContentType ContentType { get; set; } = TextContentType.Narration;

        /// <summary>
        /// Speaker identifier (null for narration)
        /// </summary>
        public string SpeakerId { get; set; }

        /// <summary>
        /// The actual dialogue/narrative text (without speaker prefix)
        /// </summary>
        public string ProcessedText { get; set; }

        /// <summary>
        /// Label name (for label lines starting with #)
        /// </summary>
        public string LabelName { get; set; }

        /// <summary>
        /// Whether this line contains variable substitutions
        /// </summary>
        public bool HasVariables { get; set; }

        /// <summary>
        /// List of variable names found in the text
        /// </summary>
        public List<string> VariableNames { get; set; } = new List<string>();

        /// <summary>
        /// Whether this line should be skipped during execution (comments, empty lines)
        /// </summary>
        public bool ShouldSkip { get; set; }

        /// <summary>
        /// Any additional metadata or attributes found in the text
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Types of text content that can appear in scripts
    /// </summary>
    public enum TextContentType
    {
        /// <summary>
        /// Plain narration text
        /// </summary>
        Narration,

        /// <summary>
        /// Character speech with speaker: "Speaker: Text"
        /// </summary>
        CharacterSpeech,

        /// <summary>
        /// Character thoughts: "*Speaker thinks: Text*" or "(thoughts)"
        /// </summary>
        CharacterThought,

        /// <summary>
        /// Comments starting with // or ;
        /// </summary>
        Comment,

        /// <summary>
        /// Labels starting with # for navigation
        /// </summary>
        Label,

        /// <summary>
        /// Empty or whitespace-only lines
        /// </summary>
        Empty
    }

    /// <summary>
    /// Enhanced GenericTextLine with comprehensive text parsing capabilities
    /// </summary>
    public class GenericTextLine : ScriptLine
    {
        #region Properties

        /// <summary>
        /// Parsed metadata for this text line
        /// </summary>
        public TextLineMetadata Metadata { get; private set; } = new TextLineMetadata();

        /// <summary>
        /// Whether this line was successfully parsed
        /// </summary>
        public bool IsValidLine { get; private set; } = true;

        #endregion

        #region Constructor

        public GenericTextLine(string scriptName, int lineIndex, string lineText) : base(scriptName, lineIndex, lineText)
        {
        }

        #endregion

        #region Parsing Implementation

        protected override void ParseLineText(string lineText, out string errors)
        {
            errors = string.Empty;

            try
            {
                // Handle null or empty content
                if (string.IsNullOrEmpty(lineText))
                {
                    Metadata.ContentType = TextContentType.Empty;
                    Metadata.ShouldSkip = true;
                    return;
                }

                var trimmedText = lineText.Trim();

                // Handle empty lines after trimming
                if (string.IsNullOrEmpty(trimmedText))
                {
                    Metadata.ContentType = TextContentType.Empty;
                    Metadata.ShouldSkip = true;
                    return;
                }

                // Parse different content types
                if (ParseComment(trimmedText)) return;
                if (ParseLabel(trimmedText)) return;
                if (ParseCharacterThought(trimmedText)) return;
                if (ParseCharacterSpeech(trimmedText)) return;
                
                // Default to narration
                ParseNarration(trimmedText);

                // Check for variables in processed text
                CheckForVariables();

                IsValidLine = true;
            }
            catch (Exception ex)
            {
                errors = $"Error parsing text line: {ex.Message}";
                IsValidLine = false;
                
                // Fallback: treat as narration
                Metadata.ContentType = TextContentType.Narration;
                Metadata.ProcessedText = lineText;
            }
        }

        #endregion

        #region Parsing Methods

        /// <summary>
        /// Parse comment lines (// or ;)
        /// </summary>
        private bool ParseComment(string text)
        {
            if (text.StartsWith("//") || text.StartsWith(";"))
            {
                Metadata.ContentType = TextContentType.Comment;
                Metadata.ProcessedText = text.Substring(text.StartsWith("//") ? 2 : 1).Trim();
                Metadata.ShouldSkip = true; // Comments don't display during normal execution
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parse label lines (#labelName)
        /// </summary>
        private bool ParseLabel(string text)
        {
            if (text.StartsWith("#"))
            {
                Metadata.ContentType = TextContentType.Label;
                Metadata.LabelName = text.Substring(1).Trim();
                Metadata.ProcessedText = Metadata.LabelName;
                Metadata.ShouldSkip = true; // Labels don't display, just register for navigation
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parse character thought lines: *Speaker thinks: Text* or (Speaker: Text)
        /// </summary>
        private bool ParseCharacterThought(string text)
        {
            // Pattern 1: *Speaker thinks: Text* or *Speaker: Text*
            if (text.StartsWith("*") && text.EndsWith("*") && text.Length > 2)
            {
                var innerContent = text.Substring(1, text.Length - 2).Trim();
                return ParseThoughtContent(innerContent);
            }

            // Pattern 2: (Speaker thinks: Text) or (Speaker: Text)
            if (text.StartsWith("(") && text.EndsWith(")") && text.Length > 2)
            {
                var innerContent = text.Substring(1, text.Length - 2).Trim();
                return ParseThoughtContent(innerContent);
            }

            return false;
        }

        private bool ParseThoughtContent(string innerContent)
        {
            var colonIndex = innerContent.IndexOf(':');
            if (colonIndex > 0)
            {
                var speakerPart = innerContent.Substring(0, colonIndex).Trim();
                var textPart = innerContent.Substring(colonIndex + 1).Trim();

                // Extract speaker ID by removing "thinks", "thought", etc.
                var speakerId = speakerPart
                    .Replace(" thinks", "")
                    .Replace(" thought", "")
                    .Replace(" is thinking", "")
                    .Trim();

                if (IsValidSpeakerName(speakerId) && !string.IsNullOrWhiteSpace(textPart))
                {
                    Metadata.ContentType = TextContentType.CharacterThought;
                    Metadata.SpeakerId = speakerId;
                    Metadata.ProcessedText = textPart;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Parse character speech lines: Speaker: Text
        /// </summary>
        private bool ParseCharacterSpeech(string text)
        {
            var colonIndex = text.IndexOf(':');
            if (colonIndex > 0 && colonIndex < text.Length - 1)
            {
                var potentialSpeaker = text.Substring(0, colonIndex).Trim();
                var potentialText = text.Substring(colonIndex + 1).Trim();

                if (IsValidSpeakerName(potentialSpeaker) && !string.IsNullOrWhiteSpace(potentialText))
                {
                    Metadata.ContentType = TextContentType.CharacterSpeech;
                    Metadata.SpeakerId = potentialSpeaker;
                    Metadata.ProcessedText = potentialText;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Parse narration (default case)
        /// </summary>
        private void ParseNarration(string text)
        {
            Metadata.ContentType = TextContentType.Narration;
            Metadata.ProcessedText = text;
            Metadata.SpeakerId = null;
        }

        /// <summary>
        /// Check for variable substitutions {variableName}
        /// </summary>
        private void CheckForVariables()
        {
            if (string.IsNullOrEmpty(Metadata.ProcessedText))
                return;

            var text = Metadata.ProcessedText;
            var startIndex = 0;
            var foundVariables = new List<string>();

            while (startIndex < text.Length)
            {
                var openBrace = text.IndexOf('{', startIndex);
                if (openBrace == -1) break;

                var closeBrace = text.IndexOf('}', openBrace);
                if (closeBrace == -1) break;

                var variableName = text.Substring(openBrace + 1, closeBrace - openBrace - 1).Trim();
                if (!string.IsNullOrWhiteSpace(variableName) && !foundVariables.Contains(variableName))
                {
                    foundVariables.Add(variableName);
                }

                startIndex = closeBrace + 1;
            }

            Metadata.HasVariables = foundVariables.Count > 0;
            Metadata.VariableNames = foundVariables;
        }

        /// <summary>
        /// Validates if a string looks like a valid speaker name
        /// </summary>
        private bool IsValidSpeakerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
                return false;

            // Allow letters, numbers, spaces, and basic punctuation for character names
            // Avoid strings that look like sentences or have complex punctuation
            var wordCount = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return wordCount <= 3 && !name.Contains("..") && !name.Contains("?") && !name.Contains("!");
        }

        #endregion

        #region Public Utility Methods

        /// <summary>
        /// Get a string representation of the parsed content for debugging
        /// </summary>
        public string GetParsedInfo()
        {
            return $"[{Metadata.ContentType}] {Metadata.SpeakerId ?? "Narrator"}: {Metadata.ProcessedText}";
        }

        /// <summary>
        /// Check if this line should be processed during script execution
        /// </summary>
        public bool ShouldExecute()
        {
            return !Metadata.ShouldSkip && IsValidLine;
        }

        /// <summary>
        /// Get the effective content for dialogue service integration
        /// </summary>
        public (string speakerId, string text, TextContentType type) GetDialogueContent()
        {
            return (Metadata.SpeakerId, Metadata.ProcessedText, Metadata.ContentType);
        }

        #endregion
    }
}