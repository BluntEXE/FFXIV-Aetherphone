namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record GameRoomSnapshotDto(
    string RoomId = "",
    string GameKind = "",
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    string GameState = "",
    int Occupancy = 0,
    bool Attached = false,
    int Epoch = 0,
    long Seq = 0,
    long ServerNowUnixMs = 0);

internal sealed record GameRoomEventDto(
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    string GameState = "",
    int Occupancy = 0);

internal sealed record GamePrivateDto(string EventKind = "", string Payload = "");

internal sealed record GameRoomYouDto(
    string RoomId = "",
    int Epoch = 0,
    long Seq = 0,
    string EventKind = "",
    string Payload = "",
    long ServerNowUnixMs = 0);

internal sealed record GameRoomCardDto(
    string RoomId = "",
    string GameKind = "",
    string OwnerUserId = "",
    string OwnerName = "",
    int MaxSeats = 0,
    int SeatedCount = 0,
    int Occupancy = 0,
    int Phase = 0,
    bool Member = false,
    string Reason = "",
    string JoinCode = "");

internal sealed record GameRoomListDto(GameRoomCardDto[]? Rooms = null, long ServerNowUnixMs = 0);

internal sealed record GameRoomCreateRequest(string ClientRoomId, string GameKind);

internal sealed record GameRoomResultDto(bool Granted = false, string Reason = "", GameRoomCardDto? Room = null);

internal sealed record GameRoomJoinRequest(string Code);

internal sealed record GameRoomMemberRequest(string UserId);

internal sealed record GameRoomActionRequest(
    string Action,
    int ActionCount,
    int Card,
    int Color,
    string ClientActionId,
    int From = -1,
    int To = -1);

internal sealed record GameRoomActionResultDto(bool Granted = false, string Reason = "", int ActionCount = 0);

internal sealed record GameRoomActionDto(bool Granted = false, string Reason = "");

internal sealed record UnoPlayerDto(
    string UserId = "",
    string DisplayName = "",
    int Seat = 0,
    int CardCount = 0,
    bool Away = false,
    int Wins = 0);

internal sealed record UnoRoomStateDto(
    long RoundIndex = 0,
    string HostUserId = "",
    UnoPlayerDto[]? Players = null,
    int MaxSeats = 0,
    int DiscardTop = -1,
    int ActiveColor = -1,
    bool Clockwise = true,
    int TurnSeat = -1,
    bool PendingDraw = false,
    int DrawPileCount = 0,
    int ActionCount = 0,
    int TurnSeconds = 0,
    int LastSeat = -1,
    string LastKind = "",
    int LastCard = -1,
    int WinnerSeat = -1);

internal sealed record UnoYouDto(
    int Seat = -1,
    int[]? Hand = null,
    bool PendingDrawnPlayable = false,
    int PendingDrawnCard = -1,
    int ActionCount = 0);

internal sealed record ChessPlayerDto(
    string UserId = "",
    string DisplayName = "",
    int Seat = 0,
    bool Away = false,
    int Wins = 0);

internal sealed record ChessRoomStateDto(
    long RoundIndex = 0,
    string HostUserId = "",
    ChessPlayerDto[]? Players = null,
    int[]? Squares = null,
    bool BlackToMove = false,
    int Castling = 0,
    int EnPassant = -1,
    int HalfmoveClock = 0,
    int WhiteSeat = -1,
    long WhiteMsRemaining = 0,
    long BlackMsRemaining = 0,
    long TurnStartedAtUnixMs = 0,
    int LastFrom = -1,
    int LastTo = -1,
    bool InCheck = false,
    int MoveCount = 0,
    int ActionCount = 0,
    string LastKind = "",
    string EndKind = "",
    int WinnerSeat = -1);
