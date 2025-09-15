using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;

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
        private static readonly Regex DigestRegex = new Regex("\"digest\"\\s*:\\s*\"(sha256:[a-f0-9]+)\"", RegexOptions.Compiled);

        /// <summary>
        /// Highlight JSON text and make digest links clickable
        /// </summary>
        /// <param name="json">The JSON text to highlight</param>
        /// <param name="digestClickCallback">Callback for when a digest link is clicked</param>
        /// <returns>A TextBlock with highlighted text</returns>
        public TextBlock HighlightJson(string json, Action<string> digestClickCallback)
        {
            var textBlock = new TextBlock();
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

            // Find and make digest links clickable
            MakeDigestsClickable(inlineCollection, digestClickCallback, textBlock);

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

        private void MakeDigestsClickable(InlineCollection inlines, Action<string> digestClickCallback, TextBlock parentTextBlock)
        {
            var newInlines = new List<Inline>();
            
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    int currentIndex = 0;
                    string text = run.Text ?? string.Empty;
                    bool isLink = run.Foreground != null && run.Foreground.Equals(Brushes.Blue);

                    // Skip already processed text
                    if (isLink)
                    {
                        newInlines.Add(run);
                        continue;
                    }

                    // Find digest matches in this run
                    foreach (Match match in DigestRegex.Matches(text))
                    {
                        // Add any text before the match
                        if (match.Index > currentIndex)
                        {
                            newInlines.Add(new Run { Text = text.Substring(currentIndex, match.Index - currentIndex) });
                        }

                        // Get the digest value
                        string digestValue = match.Groups[1].Value;
                        int digestIndex = match.Value.IndexOf(digestValue);
                        
                        // Add the text before the digest
                        newInlines.Add(new Run { Text = match.Value.Substring(0, digestIndex) });
                        
                        // Add the digest as a clickable link
                        var linkRun = new Run { Text = digestValue, Foreground = Brushes.Blue, TextDecorations = TextDecorations.Underline };
                        
                        // Create a button to handle the click
                        var button = new Button
                        {
                            Content = digestValue,
                            Foreground = Brushes.Blue,
                            Background = Brushes.Transparent,
                            BorderThickness = new Avalonia.Thickness(0),
                            Padding = new Avalonia.Thickness(0),
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                        };
                        
                        button.Click += (s, e) => digestClickCallback(digestValue);
                        
                        // Use an InlineUIContainer to embed the button in the text
                        newInlines.Add(new InlineUIContainer { Child = button });
                        
                        // Add the text after the digest if any
                        if (digestIndex + digestValue.Length < match.Length)
                        {
                            newInlines.Add(new Run { Text = match.Value.Substring(digestIndex + digestValue.Length) });
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