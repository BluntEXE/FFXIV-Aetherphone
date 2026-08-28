using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddMessagingTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "messages", 3,
            new[]
            {
                GuideStep.Note(L.Onboarding.MessagesTitle, L.Onboarding.MessagesBody),
                GuideStep.Point(L.Onboarding.MessagesListTitle, L.Onboarding.MessagesListBody, "messages.list"),
                GuideStep.Note(L.Onboarding.MessagesLinkshellsTitle, L.Onboarding.MessagesLinkshellsBody),
                GuideStep.Tap(L.Linkpearl.People, L.Onboarding.ContactsBody, "messages.tab.people",
                    "messages.tab.people"),
                GuideStep.Point(L.Onboarding.ContactsListTitle, L.Onboarding.ContactsListBody, "people.list"),
                GuideStep.Point(L.Onboarding.ContactsSearchTitle, L.Onboarding.ContactsSearchBody, "people.search"),
                GuideStep.Point(L.Onboarding.FindPeopleKindTitle, L.Onboarding.FindPeopleBody, "people.scope"),
            });
        Add(tours, "message", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Message, L.Onboarding.MessageBody),
                GuideStep.Tap(L.Onboarding.MessageCallsTitle, L.Onboarding.PhoneBody, "message.tab.calls",
                    "message.tab.calls"),
                GuideStep.Note(L.Onboarding.PhoneGroupTitle, L.Onboarding.PhoneGroupBody),
                GuideStep.Tap(L.Onboarding.MessageContactsTitle, L.Onboarding.MessageContactsBody,
                    "message.tab.contacts", "message.tab.contacts"),
                GuideStep.Point(L.Onboarding.MyNumberTourTitle, L.Onboarding.MessageNumberCopyBody,
                    "message.mynumber"),
                GuideStep.Point(L.Onboarding.MessageAddFriendTitle, L.Onboarding.MessageAddFriendBody,
                    "message.addcontact"),
                GuideStep.Note(L.Onboarding.PhoneVoiceTitle, L.Onboarding.PhoneVoiceBody),
            });
        Add(tours, "notifications", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Notifications, L.Onboarding.NotificationsBody),
                GuideStep.Point(L.Onboarding.NotificationsHistoryTitle, L.Onboarding.NotificationsHistoryBody,
                    "notifications.list"),
            });
    }
}
