namespace FixIt.Mobile.Services;

public static class HapticService
{
    public static void Click()
    {
        Perform(HapticFeedbackType.Click);
    }

    public static void LongPress()
    {
        Perform(HapticFeedbackType.LongPress);
    }

    private static void Perform(HapticFeedbackType type)
    {
        try
        {
            HapticFeedback.Default.Perform(type);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Haptics] Feedback unavailable: {ex.Message}");
        }
    }
}
