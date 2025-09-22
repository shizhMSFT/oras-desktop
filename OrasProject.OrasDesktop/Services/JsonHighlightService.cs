using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Documents;

namespace OrasProject.OrasDesktop.Services
{
    /// <summary>
    /// Service for JSON highlighting
    /// </summary>
    public class JsonHighlightService
    {
        private static readonly Regex PropertyRegex = new Regex("\"([^\"]+)\"\\s*:", RegexOptions.Compiled);
        private static readonly Regex StringRegex = new Regex(":\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(":\\s*(-?\\d+(\\.\\d+)?)", RegexOptions.Compiled);
        private static readonly Regex BooleanRegex = new Regex(":\\s*(true|false|null)", RegexOptions.Compiled);

        /// <summary>
        /// Highlight JSON text
        /// </summary>
        /// <param name="json">The JSON text to highlight</param>
        /// <returns>A SelectableTextBlock with highlighted text</returns>
        public TextBlock HighlightJson(string json)
        {
            var textBlock = new SelectableTextBlock();
            var inlineCollection = new InlineCollection();
            textBlock.Inlines = inlineCollection;

            // Format the JSON for better readability
            json = FormatJson(json);

            int currentIndex = 0;
            
            // Find and highlight properties
            foreach (Match match in PropertyRegex.Matches(json))
            {
                // Add any text before the match
                if (match.Index > currentIndex)
                {
                    inlineCollection.Add(new Run { Text = json.Substring(currentIndex, match.Index - currentIndex) });
                }

                // Add the property name with highlighting
                inlineCollection.Add(new Run { Text = match.Value, Foreground = Brushes.Blue });

                currentIndex = match.Index + match.Length;
            }

            // Add any remaining text
            if (currentIndex < json.Length)
            {
                inlineCollection.Add(new Run { Text = json.Substring(currentIndex) });
            }

            // Find and highlight strings
            HighlightInlines(inlineCollection, StringRegex, Brushes.DarkGreen);

            // Find and highlight numbers
            HighlightInlines(inlineCollection, NumberRegex, Brushes.DarkOrange);

            // Find and highlight booleans and null
            HighlightInlines(inlineCollection, BooleanRegex, Brushes.DarkMagenta);

            return textBlock;
        }

        private void HighlightInlines(InlineCollection inlines, Regex regex, IBrush color)
        {
            var newInlines = new List<Inline>();
            
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    int currentIndex = 0;
                    string text = run.Text ?? string.Empty;
                    bool isColored = run.Foreground != null;

                    // Skip already colored text
                    if (isColored)
                    {
                        newInlines.Add(run);
                        continue;
                    }

                    // Find matches in this run
                    foreach (Match match in regex.Matches(text))
                    {
                        // Add any text before the match
                        if (match.Index > currentIndex)
                        {
                            newInlines.Add(new Run { Text = text.Substring(currentIndex, match.Index - currentIndex) });
                        }

                        // Add the value part with highlighting
                        string valueMatch = match.Groups[1].Value;
                        int valueIndex = match.Value.IndexOf(valueMatch);
                        
                        // Add the text before the value
                        newInlines.Add(new Run { Text = match.Value.Substring(0, valueIndex) });
                        
                        // Add the value with color
                        newInlines.Add(new Run { Text = valueMatch, Foreground = color });
                        
                        // Add the text after the value if any
                        if (valueIndex + valueMatch.Length < match.Length)
                        {
                            newInlines.Add(new Run { Text = match.Value.Substring(valueIndex + valueMatch.Length) });
                        }

                        currentIndex = match.Index + match.Length;
                    }

                    // Add any remaining text
                    if (currentIndex < text.Length)
                    {
                        newInlines.Add(new Run { Text = text.Substring(currentIndex) });
                    }
                }
                else
                {
                    newInlines.Add(inline);
                }
            }

            inlines.Clear();
            foreach (var inline in newInlines)
            {
                inlines.Add(inline);
            }
        }



        private string FormatJson(string json)
        {
            try
            {
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }
    }
}