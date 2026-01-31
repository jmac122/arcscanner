using System.Windows.Media;

namespace ArcRaidersOverlay;

/// <summary>
/// Centralized color and brush constants for the overlay UI.
/// Eliminates duplicate color definitions and provides a single source of truth.
/// </summary>
public static class Theme
{
    #region Base Colors

    /// <summary>Gray used for secondary/muted text (0xAAAAAA)</summary>
    public static readonly Color TextMuted = Color.FromRgb(0xAA, 0xAA, 0xAA);

    /// <summary>Darker gray for disabled/inactive text (0x888888)</summary>
    public static readonly Color TextSecondary = Color.FromRgb(0x88, 0x88, 0x88);

    /// <summary>Darkest gray for placeholder text (0x666666)</summary>
    public static readonly Color TextDisabled = Color.FromRgb(0x66, 0x66, 0x66);

    /// <summary>Default text color (0xCCCCCC)</summary>
    public static readonly Color TextDefault = Color.FromRgb(0xCC, 0xCC, 0xCC);

    #endregion

    #region Event Colors

    /// <summary>Supply Drop events - Orange (0xFFAA00)</summary>
    public static readonly Color EventDrop = Color.FromRgb(0xFF, 0xAA, 0x00);

    /// <summary>Storm events - Blue (0x8888FF)</summary>
    public static readonly Color EventStorm = Color.FromRgb(0x88, 0x88, 0xFF);

    /// <summary>Convoy events - Red (0xFF8888)</summary>
    public static readonly Color EventConvoy = Color.FromRgb(0xFF, 0x88, 0x88);

    /// <summary>Extraction events - Green (0x88FF88)</summary>
    public static readonly Color EventExtraction = Color.FromRgb(0x88, 0xFF, 0x88);

    #endregion

    #region Timer Colors

    /// <summary>Active event - Bright green (0x00FF00)</summary>
    public static readonly Color TimerActive = Color.FromRgb(0x00, 0xFF, 0x00);

    /// <summary>Urgent timer (less than 2 min) - Red (0xFF4444)</summary>
    public static readonly Color TimerUrgent = Color.FromRgb(0xFF, 0x44, 0x44);

    /// <summary>Warning timer (2-5 min) - Orange (0xFFAA00)</summary>
    public static readonly Color TimerWarning = Color.FromRgb(0xFF, 0xAA, 0x00);

    /// <summary>Normal timer - Light green (0x88FF88)</summary>
    public static readonly Color TimerNormal = Color.FromRgb(0x88, 0xFF, 0x88);

    #endregion

    #region Tooltip Colors

    /// <summary>Project uses text - Light blue (0x88AAFF)</summary>
    public static readonly Color TooltipProject = Color.FromRgb(0x88, 0xAA, 0xFF);

    /// <summary>Workshop uses text - Light purple (0xCC88FF)</summary>
    public static readonly Color Workshop = Color.FromRgb(0xCC, 0x88, 0xFF);

    #endregion

    #region Recommendation Banner Colors

    /// <summary>Keep recommendation - Green background</summary>
    public static readonly Color RecommendKeep = Color.FromArgb(0x55, 0x00, 0xFF, 0x00);

    /// <summary>Sell recommendation - Gold/Yellow background</summary>
    public static readonly Color RecommendSell = Color.FromArgb(0x55, 0xFF, 0xCC, 0x00);

    /// <summary>Recycle recommendation - Cyan background</summary>
    public static readonly Color RecommendRecycle = Color.FromArgb(0x55, 0x00, 0xCC, 0xFF);

    /// <summary>Either (sell or recycle) - Gray background</summary>
    public static readonly Color RecommendEither = Color.FromArgb(0x44, 0x88, 0x88, 0x88);

    /// <summary>Quest item - Orange/Warning background</summary>
    public static readonly Color RecommendQuest = Color.FromArgb(0x66, 0xFF, 0x88, 0x00);

    #endregion

    #region Recycle Efficiency Colors

    /// <summary>Good recycling value (70%+) - Green</summary>
    public static readonly Color RecycleGood = Color.FromRgb(0x00, 0xFF, 0x88);

    /// <summary>Medium recycling value (50-69%) - Yellow</summary>
    public static readonly Color RecycleMedium = Color.FromRgb(0xFF, 0xCC, 0x00);

    /// <summary>Poor recycling value (&lt;50%) - Red</summary>
    public static readonly Color RecyclePoor = Color.FromRgb(0xFF, 0x66, 0x66);

    #endregion

    #region Cached Brushes (for performance)

    public static readonly SolidColorBrush BrushTextMuted = new(TextMuted);
    public static readonly SolidColorBrush BrushTextSecondary = new(TextSecondary);
    public static readonly SolidColorBrush BrushTextDisabled = new(TextDisabled);
    public static readonly SolidColorBrush BrushTextDefault = new(TextDefault);

    public static readonly SolidColorBrush BrushEventDrop = new(EventDrop);
    public static readonly SolidColorBrush BrushEventStorm = new(EventStorm);
    public static readonly SolidColorBrush BrushEventConvoy = new(EventConvoy);
    public static readonly SolidColorBrush BrushEventExtraction = new(EventExtraction);

    public static readonly SolidColorBrush BrushTimerActive = new(TimerActive);
    public static readonly SolidColorBrush BrushTimerUrgent = new(TimerUrgent);
    public static readonly SolidColorBrush BrushTimerWarning = new(TimerWarning);
    public static readonly SolidColorBrush BrushTimerNormal = new(TimerNormal);

    public static readonly SolidColorBrush BrushTooltipProject = new(TooltipProject);
    public static readonly SolidColorBrush BrushWorkshop = new(Workshop);

    // Recommendation banner brushes
    public static readonly SolidColorBrush BrushRecommendKeep = new(RecommendKeep);
    public static readonly SolidColorBrush BrushRecommendSell = new(RecommendSell);
    public static readonly SolidColorBrush BrushRecommendRecycle = new(RecommendRecycle);
    public static readonly SolidColorBrush BrushRecommendEither = new(RecommendEither);
    public static readonly SolidColorBrush BrushRecommendQuest = new(RecommendQuest);

    // Recycle efficiency brushes
    public static readonly SolidColorBrush BrushRecycleGood = new(RecycleGood);
    public static readonly SolidColorBrush BrushRecycleMedium = new(RecycleMedium);
    public static readonly SolidColorBrush BrushRecyclePoor = new(RecyclePoor);

    // Utility brushes
    public static readonly SolidColorBrush BrushTransparent = new(Colors.Transparent);

    #endregion

    // Freeze brushes for better performance (makes them immutable and thread-safe)
    static Theme()
    {
        BrushTextMuted.Freeze();
        BrushTextSecondary.Freeze();
        BrushTextDisabled.Freeze();
        BrushTextDefault.Freeze();

        BrushEventDrop.Freeze();
        BrushEventStorm.Freeze();
        BrushEventConvoy.Freeze();
        BrushEventExtraction.Freeze();

        BrushTimerActive.Freeze();
        BrushTimerUrgent.Freeze();
        BrushTimerWarning.Freeze();
        BrushTimerNormal.Freeze();

        BrushTooltipProject.Freeze();
        BrushWorkshop.Freeze();

        BrushRecommendKeep.Freeze();
        BrushRecommendSell.Freeze();
        BrushRecommendRecycle.Freeze();
        BrushRecommendEither.Freeze();
        BrushRecommendQuest.Freeze();

        BrushRecycleGood.Freeze();
        BrushRecycleMedium.Freeze();
        BrushRecyclePoor.Freeze();

        BrushTransparent.Freeze();
    }
}
