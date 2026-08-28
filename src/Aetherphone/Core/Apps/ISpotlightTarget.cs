namespace Aetherphone.Core.Apps;

internal interface ISpotlightPages
{
    int SpotlightPageCount { get; }

    string SpotlightPageTitle(int pageIndex);

    void RequestSpotlightPage(int pageIndex);
}

internal interface ISpotlightNotes
{
    void RequestNote(Guid noteId);
}
