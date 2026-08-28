using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    public const string WelcomeId = "welcome";
    public const string ControlCenterOpenIntent = "chrome.controlcenter.open";
    public const string ControlCenterCloseIntent = "chrome.controlcenter.close";

    private static readonly GuideSequence Welcome = new(WelcomeId, 7, null,
        new[]
        {
            GuideStep.Page(L.Onboarding.HomeTourTitle, L.Onboarding.HomeTourBody, L.Onboarding.Continue),
            GuideStep.Point(L.Onboarding.AppsTourTitle, L.Onboarding.AppsTourBody, "home.app.message"),
            GuideStep.Point(L.Onboarding.StoreTourTitle, L.Onboarding.StoreTourBody, "home.app.appstore"),
            GuideStep.Point(L.Onboarding.WidgetTourTitle, L.Onboarding.WidgetTourBody, "home.widget"),
            GuideStep.Point(L.Onboarding.SearchTourTitle, L.Onboarding.SearchTourBody, "home.search"),
            GuideStep.Note(L.Onboarding.CustomizeTitle, L.Onboarding.CustomizeBody),
            GuideStep.Tap(L.Onboarding.ControlCenterTitle, L.Onboarding.ControlCenterTapBody, "chrome.controlcenter",
                ControlCenterOpenIntent),
            GuideStep.ControlCenterNote(L.Onboarding.ControlCenterInsideTitle, L.Onboarding.ControlCenterInsideBody,
                ControlCenterCloseIntent),
            GuideStep.Point(L.Onboarding.SignalTourTitle, L.Onboarding.SignalTourBody, "chrome.signal"),
            GuideStep.Point(L.Onboarding.BatteryTourTitle, L.Onboarding.BatteryTourBody, "chrome.battery"),
            GuideStep.Point(L.Onboarding.MinimizeTitle, L.Onboarding.MinimizeBody, "chrome.minimize"),
            GuideStep.Point(L.Onboarding.LockTitle, L.Onboarding.LockBody, "chrome.lock"),
        });

    private static readonly Dictionary<string, GuideSequence> Tours = BuildTours();
    public static GuideSequence GetWelcome() => Welcome;

    public static bool TryGetAppTour(string appId, out GuideSequence sequence) =>
        Tours.TryGetValue(appId, out sequence);

    private static Dictionary<string, GuideSequence> BuildTours()
    {
        var tours = new Dictionary<string, GuideSequence>();
        AddMessagingTours(tours);
        AddSocialTours(tours);
        AddGameContentTours(tours);
        AddPlayTours(tours);
        AddMediaTours(tours);
        AddSystemTours(tours);
        return tours;
    }

    private static void Add(Dictionary<string, GuideSequence> tours, string appId, int version, GuideStep[] steps) =>
        tours[appId] = new GuideSequence(appId, version, appId, steps);
}
