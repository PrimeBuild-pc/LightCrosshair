namespace LightCrosshair.UI.Services;

public sealed record LaunchOptions(string Page, string? Suggest, bool FakeCore, string? ScreenshotsDirectory)
{
    public static LaunchOptions Parse(string[] args)
    {
        var page = UiPages.Studio;
        string? suggest = null;
        var fake = false;
        string? shots = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--page" when i + 1 < args.Length:
                    page = args[++i];
                    break;
                case "--suggest" when i + 1 < args.Length:
                    suggest = args[++i];
                    break;
                case "--fake-core":
                    fake = true;
                    break;
                case "--screenshots" when i + 1 < args.Length:
                    shots = args[++i];
                    break;
            }
        }

        if (page is not (UiPages.Studio or UiPages.Library or UiPages.Games or UiPages.Hotkeys or UiPages.Settings or ViewModels.FpsOverlayViewModel.PageId))
            page = UiPages.Studio;
        return new LaunchOptions(page, suggest, fake, shots);
    }
}
