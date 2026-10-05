// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Objects.Errors;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Bitvavo error mapping, consumed by the REST and WebSocket clients. Every error body has the same shape —
/// <c>{ "errorCode": int, "error": "msg" }</c> (the WebSocket adds <c>"event": "error"</c>) — and the code alone identifies the
/// problem: this table maps each documented code (docs.bitvavo.com, "Handle errors") onto CryptoExchange.Net's
/// <see cref="ErrorType"/> and says whether the situation clears by itself (<see cref="ErrorInfo.IsTransient"/>), so a caller
/// can react to <c>InsufficientBalance</c>, <c>UnknownOrder</c> or <c>InvalidPrice</c> without parsing the message.
/// </summary>
/// <remarks>
/// <para>The server's own message travels next to the mapped type (<c>Error.Message</c>); a code that is not in the catalog maps to
/// <see cref="ErrorType.Unknown"/>.</para>
/// <para>Code <c>109</c> (503: the request timed out) is the one that must not be retried blindly: the operation may or may not
/// have happened, so it maps to <see cref="ErrorType.Timeout"/> and is NOT marked transient — read the order back first.</para>
/// <para>Code <c>429</c> is an HTTP 400 about too many decimal places; it has nothing to do with HTTP 429 (rate limit), which
/// carries codes 105 and 112.</para>
/// </remarks>
internal static class BitvavoErrors
{
    /// <summary>The mapping for the spot API (REST and WebSocket).</summary>
    public static readonly ErrorMapping SpotMapping = new(
    [
        // Request format and parameters (HTTP 400)
        new ErrorInfo(ErrorType.InvalidParameter, false, "Invalid request, parameter or parameter value", "102", "200", "201", "204", "205", "206", "236", "239", "429", "439", "510"),
        new ErrorInfo(ErrorType.MissingParameter, false, "Missing parameters, or none that change anything", "203", "232"),
        new ErrorInfo(ErrorType.RejectedOrderConfiguration, false, "The order's type or time in force does not allow this", "202", "231", "234"),

        // Orders and trading (HTTP 400)
        new ErrorInfo(ErrorType.InvalidQuantity, false, "Invalid amount", "210", "212", "217", "406"),
        new ErrorInfo(ErrorType.InvalidPrice, false, "Invalid price", "211", "213", "215", "422"),
        new ErrorInfo(ErrorType.InsufficientBalance, false, "Insufficient balance", "216", "408"),
        new ErrorInfo(ErrorType.UnknownSymbol, false, "The market is no longer listed", "219"),
        new ErrorInfo(ErrorType.DuplicateClientOrderId, false, "The clientOrderId is already in use in this market", "220"),
        new ErrorInfo(ErrorType.RateLimitOrder, false, "Too many open orders in this market (at most 100)", "235"),
        new ErrorInfo(ErrorType.InvalidStopParameters, false, "Required parameters of the stop order type are missing", "237", "238"),
        new ErrorInfo(ErrorType.UnknownOrder, false, "The order does not exist or is no longer active", "240"),

        // Market status (HTTP 400, 409): trading resumes by itself
        new ErrorInfo(ErrorType.UnavailableSymbol, true, "The market is not in trading status", "423", "424", "425", "426", "431"),

        // Deposits and withdrawals (HTTP 400)
        new ErrorInfo(ErrorType.InvalidOperation, false, "This deposit, withdrawal or transfer is not possible", "401", "407", "410", "411", "412", "413"),
        new ErrorInfo(ErrorType.InvalidOperation, true, "This withdrawal is not possible yet; wait and try again", "405", "414"),
        new ErrorInfo(ErrorType.Unauthorized, false, "The account must be verified before this deposit or withdrawal", "402", "403", "409"),
        new ErrorInfo(ErrorType.RiskError, false, "The withdrawal is limited by Bitvavo's risk rules", "434", "435", "436", "437", "438"),
        new ErrorInfo(ErrorType.SystemError, true, "An internal error prevented the operation", "404"),

        // Authentication and permissions (HTTP 403)
        new ErrorInfo(ErrorType.MissingCredentials, false, "This endpoint requires authentication", "300"),
        new ErrorInfo(ErrorType.InvalidTimestamp, false, "The request timestamp or access window is invalid or outside the allowed range", "302", "303", "304"),
        new ErrorInfo(ErrorType.Unauthorized, false, "The API key, signature, permissions or account do not allow this",
            "301", "305", "306", "307", "308", "309", "310", "311", "312", "313", "314", "316", "317", "318", "319", "320", "322", "511", "512", "513"),
        new ErrorInfo(ErrorType.InvalidOperation, false, "This operation is not available here", "415", "514"),

        // Rate limit (HTTP 429): blocked until the counter resets
        new ErrorInfo(ErrorType.RateLimitRequest, true, "Rate limit exceeded", "105", "112"),

        // Server side (HTTP 500, 503)
        new ErrorInfo(ErrorType.SystemError, true, "Bitvavo could not process the request; retry after a delay", "101", "400", "107", "108", "111", "419"),
        new ErrorInfo(ErrorType.Timeout, false, "The request timed out and the operation may or may not have happened; verify its state before retrying", "109"),
        new ErrorInfo(ErrorType.Timeout, true, "The connection timed out because Bitvavo received no new market data events", "430"),
    ]);
}
