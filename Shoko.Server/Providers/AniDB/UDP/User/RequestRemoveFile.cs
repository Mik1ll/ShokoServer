using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Remove a file from MyList.
/// </summary>
public class RequestRemoveFile : UDPRequest<Void>
{
    // These are dependent on context
    protected override string BaseCommand => $"MYLISTDEL size={Size}&ed2k={Hash}";

    public string Hash { get; set; }

    public long Size { get; set; }

    protected override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.MYLIST_ENTRY_DELETED:
            case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
            case UDPReturnCode.NO_SUCH_FILE:
                return new UDPResponse<Void> { Code = code };
        }

        throw new UnexpectedUDPResponseException(code, receivedData, Command);
    }

    public RequestRemoveFile(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
