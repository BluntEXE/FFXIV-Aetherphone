using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddSystemTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "settings", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.SettingsTitle, L.Onboarding.SettingsBody),
                GuideStep.Point(L.Onboarding.SettingsAccountTitle, L.Onboarding.SettingsAccountBody,
                    "settings.account"),
                GuideStep.Point(L.Onboarding.SettingsAppearanceTitle, L.Onboarding.SettingsAppearanceBody,
                    "settings.row.appearance"),
                GuideStep.Point(L.Onboarding.SettingsTutorialsTitle, L.Onboarding.SettingsTutorialsBody,
                    "settings.row.tutorials"),
            });
        Add(tours, "appstore", 1,
            new[]
            {
                GuideStep.Note(L.Apps.AppStore, L.Onboarding.AppStoreBody),
                GuideStep.Point(L.Onboarding.AppStoreGetTitle, L.Onboarding.AppStoreGetBody, "appstore.row"),
                GuideStep.Tap(L.Onboarding.AppStoreBrowseTitle, L.Onboarding.AppStoreBrowseBody, "appstore.tab.apps",
                    "appstore.tab.apps"),
                GuideStep.Point(L.Onboarding.AppStoreSearchTitle, L.Onboarding.AppStoreSearchBody,
                    "appstore.tab.search"),
                GuideStep.Note(L.Onboarding.AppStoreRemoveTitle, L.Onboarding.AppStoreRemoveBody),
            });
        Add(tours, "clock", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Clock, L.Onboarding.ClockIntroBody),
                GuideStep.Tap(L.Onboarding.ClockTabsTitle, L.Onboarding.ClockTabsBody, "clock.tabs",
                    "clock.tab.alarms"),
                GuideStep.Point(L.Onboarding.ClockAddTitle, L.Onboarding.ClockAddBody, "clock.add"),
            });
        Add(tours, "calendar", 2,
            new[]
            {
                GuideStep.Point(L.Calendar.Title, L.Onboarding.CalendarBody, "calendar.grid"),
                GuideStep.Point(L.Onboarding.CalendarAgendaTitle, L.Onboarding.CalendarAgendaBody, "calendar.agenda"),
                GuideStep.Point(L.Calendar.NewEvent, L.Onboarding.CalendarAddBody, "calendar.new"),
            });
        Add(tours, "calculator", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Calculator, L.Onboarding.CalculatorBody),
                GuideStep.Point(L.Onboarding.CalculatorTapeTitle, L.Onboarding.CalculatorTapeBody,
                    "calculator.display"),
            });
        Add(tours, "timers", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Timers, L.Onboarding.TimersBody),
                GuideStep.Point(L.Onboarding.TimersResetsTitle, L.Onboarding.TimersResetsBody, "timers.resets"),
                GuideStep.Point(L.Onboarding.TimersRemindersTitle, L.Onboarding.TimersRemindersBody,
                    "timers.reminders"),
            });
        Add(tours, "shortcuts", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Shortcuts, L.Onboarding.ShortcutsBody),
                GuideStep.Point(L.Onboarding.ShortcutsNewTitle, L.Onboarding.ShortcutsNewBody, "shortcuts.new"),
                GuideStep.Point(L.Onboarding.ShortcutsLibraryTitle, L.Onboarding.ShortcutsLibraryBody,
                    "shortcuts.library"),
                GuideStep.Point(L.Onboarding.ShortcutsImportTitle, L.Onboarding.ShortcutsImportBody,
                    "shortcuts.import"),
                GuideStep.Tap(L.Onboarding.ShortcutsPluginsTitle, L.Onboarding.ShortcutsPluginsBody, "shortcuts.tabs",
                    "shortcuts.tab.plugins"),
                GuideStep.Note(L.Onboarding.ShortcutsHomeTitle, L.Onboarding.ShortcutsHomeBody),
            });
        Add(tours, "wallet", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Wallet, L.Onboarding.WalletBody),
                GuideStep.Point(L.Onboarding.WalletGilTitle, L.Onboarding.WalletGilBody, "wallet.gil"),
                GuideStep.Point(L.Onboarding.WalletCurrenciesTitle, L.Onboarding.WalletCurrenciesBody,
                    "wallet.currencies"),
            });
        Add(tours, "news", 2,
            new[]
            {
                GuideStep.Note(L.Apps.News, L.Onboarding.NewsBody),
                GuideStep.Point(L.Onboarding.NewsCategoriesTitle, L.Onboarding.NewsCategoriesBody, "news.categories"),
                GuideStep.Point(L.Onboarding.NewsReadTitle, L.Onboarding.NewsReadBody, "news.feed"),
                GuideStep.Point(L.Onboarding.NewsRefreshTitle, L.Onboarding.NewsRefreshBody, "news.refresh"),
            });
        Add(tours, "feedback", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Feedback, L.Onboarding.FeedbackIntroBody),
                GuideStep.Point(L.Onboarding.FeedbackWriteTitle, L.Onboarding.FeedbackWriteBody, "feedback.input"),
                GuideStep.Point(L.Onboarding.FeedbackSendTitle, L.Onboarding.FeedbackSendBody, "feedback.send"),
                GuideStep.Note(L.Onboarding.FeedbackPrivacyTitle, L.Onboarding.FeedbackPrivacyBody),
            });
        Add(tours, "polls", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Polls, L.Onboarding.PollsBody),
                GuideStep.Point(L.Onboarding.PollsVoteTitle, L.Onboarding.PollsVoteBody, "polls.card"),
                GuideStep.Point(L.Onboarding.PollsResultsTitle, L.Onboarding.PollsResultsBody, "polls.card"),
            });
        Add(tours, "announcements", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Announcements, L.Onboarding.AnnouncementsBody),
                GuideStep.Point(L.Onboarding.AnnouncementsCardTitle, L.Onboarding.AnnouncementsCardBody,
                    "announcements.card"),
                GuideStep.Note(L.Onboarding.AnnouncementsQuietTitle, L.Onboarding.AnnouncementsQuietBody),
            });
    }
}
