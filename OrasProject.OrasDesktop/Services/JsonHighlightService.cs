using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using OrasProject.OrasDesktop.Themes;

namespace OrasProject.OrasDesktop.Services
{
    /// <summary>
    /// Service for JSON highlighting with theme support
    /// </summary>
    public class JsonHighlightService
    {
        private static readonly Regex PropertyRegex = new Regex("\"([^\"]+)\"\\s*:", RegexOptions.Compiled);
        private static readonly Regex StringRegex = new Regex(":\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(":\\s*(-?\\d+(\\.\\d+)?)", RegexOptions.Compiled);
        private static readonly Regex BooleanRegex = new Regex(":\\s*(true|false|null)", RegexOptions.Compiled);

        private readonly IThemeService _themeService;

        /// <summary>
        /// Initializes a new instance of JsonHighlightService with theme support
        /// </summary>
        /// <param name="themeService">Theme service for retrieving theme-aware colors</param>
        public JsonHighlightService(IThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        }

        /// <summary>
        /// Highlight JSON text with theme-aware colors
        /// </summary>
        /// <param name="json">The JSON text to highlight</param>
        /// <returns>A SelectableTextBlock with highlighted text</returns>
        public TextBlock HighlightJson(string json)
        {
            var colors = _themeService.GetJsonSyntaxColors();
            
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
                    inlineCollection.Add(new Run 
                    { 
                        Text = json.Substring(currentIndex, match.Index - currentIndex),
                        Foreground = colors.DefaultBrush
                    });
                }

                // Add the property name with highlighting
                inlineCollection.Add(new Run { Text = match.Value, Foreground = colors.PropertyBrush });

                currentIndex = match.Index + match.Length;
            }

            // Add any remaining text
            if (currentIndex < json.Length)
            {
                inlineCollection.Add(new Run 
                { 
                    Text = json.Substring(currentIndex),
                    Foreground = colors.DefaultBrush
                });
            }

            // Find and highlight strings
            HighlightInlines(inlineCollection, StringRegex, colors.StringBrush, colors.DefaultBrush);

            // Find and highlight numbers
            HighlightInlines(inlineCollection, NumberRegex, colors.NumberBrush, colors.DefaultBrush);

            // Find and highlight booleans and null
            HighlightInlines(inlineCollection, BooleanRegex, colors.BooleanBrush, colors.DefaultBrush);

            return textBlock;
        }

        private void HighlightInlines(InlineCollection inlines, Regex regex, IBrush color, IBrush defaultBrush)
        {
            var newInlines = new List<Inline>();
            
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    int currentIndex = 0;
                    string text = run.Text ?? string.Empty;
                    bool isColored = run.Foreground != null && run.Foreground != defaultBrush;

                    // Skip already colored text (but not default colored)
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
                            newInlines.Add(new Run 
                            { 
                                Text = text.Substring(currentIndex, match.Index - currentIndex),
                                Foreground = defaultBrush
                            });
                        }

                        // Add the value part with highlighting
                        string valueMatch = match.Groups[1].Value;
                        int valueIndex = match.Value.IndexOf(valueMatch);
                        
                        // Add the text before the value
                        newInlines.Add(new Run 
                        { 
                            Text = match.Value.Substring(0, valueIndex),
                            Foreground = defaultBrush
                        });
                        
                        // Add the value with color
                        newInlines.Add(new Run { Text = valueMatch, Foreground = color });
                        
                        // Add the text after the value if any
                        if (valueIndex + valueMatch.Length < match.Length)
                        {
                            newInlines.Add(new Run 
                            { 
                                Text = match.Value.Substring(valueIndex + valueMatch.Length),
                                Foreground = defaultBrush
                            });
                        }

                        currentIndex = match.Index + match.Length;
                    }

                    // Add any remaining text
                    if (currentIndex < text.Length)
                    {
                        newInlines.Add(new Run 
                        { 
                            Text = text.Substring(currentIndex),
                            Foreground = defaultBrush
                        });
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