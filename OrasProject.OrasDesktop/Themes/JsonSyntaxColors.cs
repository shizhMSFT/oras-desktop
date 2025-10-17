using Avalonia.Media;

namespace OrasProject.OrasDesktop.Themes
{
    /// <summary>
    /// Defines color scheme for JSON syntax highlighting
    /// </summary>
    public class JsonSyntaxColors
    {
        /// <summary>
        /// Color for JSON property names
        /// </summary>
        public required IBrush PropertyBrush { get; init; }
        
        /// <summary>
        /// Color for JSON string values
        /// </summary>
        public required IBrush StringBrush { get; init; }
        
        /// <summary>
        /// Color for JSON number values
        /// </summary>
        public required IBrush NumberBrush { get; init; }
        
        /// <summary>
        /// Color for JSON boolean and null values
        /// </summary>
        public required IBrush BooleanBrush { get; init; }
        
        /// <summary>
        /// Default color for normal text
        /// </summary>
        public required IBrush DefaultBrush { get; init; }
        
        /// <summary>
        /// Light theme color scheme
        /// </summary>
        public static JsonSyntaxColors Light => new()
        {
            PropertyBrush = new SolidColorBrush(Color.Parse("#0000FF")), // Blue
            StringBrush = new SolidColorBrush(Color.Parse("#008000")), // DarkGreen
            NumberBrush = new SolidColorBrush(Color.Parse("#FF8C00")), // DarkOrange
            BooleanBrush = new SolidColorBrush(Color.Parse("#8B008B")), // DarkMagenta
            DefaultBrush = new SolidColorBrush(Color.Parse("#000000")) // Black
        };
        
        /// <summary>
        /// Dark theme color scheme
        /// </summary>
        public static JsonSyntaxColors Dark => new()
        {
            PropertyBrush = new SolidColorBrush(Color.Parse("#8DBBFF")), // Softer Blue
            StringBrush = new SolidColorBrush(Color.Parse("#F0B7A4")), // Soft Coral
            NumberBrush = new SolidColorBrush(Color.Parse("#CFE5C8")), // Pale Green
            BooleanBrush = new SolidColorBrush(Color.Parse("#E1C6EC")), // Soft Lavender
            DefaultBrush = new SolidColorBrush(Color.Parse("#E6E6E6")) // Brighter Gray
        };
    }
}
