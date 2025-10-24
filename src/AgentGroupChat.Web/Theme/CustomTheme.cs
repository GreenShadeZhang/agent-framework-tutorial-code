using MudBlazor;

namespace AgentGroupChat.Web.Theme;

/// <summary>
/// Custom theme configuration with violet color palette
/// </summary>
public static class CustomTheme
{
    public static MudTheme VioletTheme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#8B5CF6", // Violet-500
            Secondary = "#A78BFA", // Violet-400
            Tertiary = "#7C3AED", // Violet-600
            Info = "#6366F1", // Indigo-500
            Success = "#10B981", // Emerald-500
            Warning = "#F59E0B", // Amber-500
            Error = "#EF4444", // Red-500
            Dark = "#1F2937", // Gray-800
            TextPrimary = "#1F2937", // Gray-800
            TextSecondary = "#6B7280", // Gray-500
            Background = "#F9FAFB", // Gray-50
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#1F2937",
            AppbarBackground = "#8B5CF6", // Violet-500
            AppbarText = "#FFFFFF",
            LinesDefault = "#E5E7EB", // Gray-200
            LinesInputs = "#D1D5DB", // Gray-300
            Divider = "#E5E7EB",
            DividerLight = "#F3F4F6",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#A78BFA", // Violet-400
            Secondary = "#C4B5FD", // Violet-300
            Tertiary = "#8B5CF6", // Violet-500
            Info = "#818CF8", // Indigo-400
            Success = "#34D399", // Emerald-400
            Warning = "#FBBF24", // Amber-400
            Error = "#F87171", // Red-400
            Dark = "#111827", // Gray-900
            TextPrimary = "#F9FAFB", // Gray-50
            TextSecondary = "#D1D5DB", // Gray-300
            Background = "#111827", // Gray-900
            Surface = "#1F2937", // Gray-800
            DrawerBackground = "#1F2937",
            DrawerText = "#F9FAFB",
            AppbarBackground = "#7C3AED", // Violet-600
            AppbarText = "#FFFFFF",
            LinesDefault = "#374151", // Gray-700
            LinesInputs = "#4B5563", // Gray-600
            Divider = "#374151",
            DividerLight = "#4B5563",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "280px",
            DrawerWidthRight = "280px",
            AppbarHeight = "64px"
        },
        ZIndex = new ZIndex
        {
            Drawer = 1300,
            AppBar = 1400,
            Dialog = 1500,
            Popover = 1600,
            Snackbar = 1700,
            Tooltip = 1800
        }
    };
}
