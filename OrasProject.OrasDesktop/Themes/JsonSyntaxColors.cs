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
            PropertyBrush = new SolidColorBrush(Color.Parse("#569CD6")), // Light Blue
            StringBrush = new SolidColorBrush(Color.Parse("#CE9178")), // Light Orange
            NumberBrush = new SolidColorBrush(Color.Parse("#B5CEA8")), // Light Green
            BooleanBrush = new SolidColorBrush(Color.Parse("#C586C0")), // Light Purple
            DefaultBrush = new SolidColorBrush(Color.Parse("#D4D4D4")) // Light Gray
        };
    }
}
