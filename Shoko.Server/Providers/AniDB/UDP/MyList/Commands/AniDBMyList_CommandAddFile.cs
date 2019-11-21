using System;
using Shoko.Models.Enums;
using Shoko.Server.Providers.AniDB.MyList.Exceptions;

namespace Shoko.Server.Providers.AniDB.MyList.Commands
{
    /// <summary>
    /// Add a file to MyList. If it doesn't exist, it will return the MyListID for future updates.
    /// If it exists, it will return the current status on AniDB. 
    /// </summary>
    public class AniDBMyList_CommandAddFile : AniDBUDP_BaseCommand<AniDBMyList_AddFileResponse>
    {
        // These are always added
        protected override string BaseCommand => "MYLISTADD size=%s&ed2k=%s&state=%s";
        // These are dependent on context
        protected override string Command { get; set; }

        public AniDBFile_State State { get; set; }
        
        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        
        protected override AniDBMyList_AddFileResponse ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            switch (code)
            {
                case AniDBUDPReturnCode.MYLIST_ENTRY_ADDED:
                {
                    /* Response Format
                     * {int4 mylist id of new entry}
                     */
                    // parse the MyList ID
                    string[] arrResult = receivedData.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        int.TryParse(arrResult[1], out int myListID);
                        return new AniDBMyList_AddFileResponse
                        {
                            MyListID = myListID,
                            State = State,
                            IsWatched = IsWatched,
                            WatchedDate = WatchedDate
                        };
                    }
                    break;
                }
                case AniDBUDPReturnCode.FILE_ALREADY_IN_MYLIST:
                {
                    /* Response Format
                     * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                     */
                    //file already exists: read 'watched' status
                    string[] arrResult = receivedData.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        string[] arrStatus = arrResult[1].Split('|');
                        bool hasMyListID = int.TryParse(arrStatus[0], out int myListID);
                        if (!hasMyListID) throw new UnexpectedAniDBResponse
                        {
                            Message = "MyListID was not provided. Use AniDBMyList_CommandAddEpisode for generic files.",
                            Response = receivedData,
                            ReturnCode = code
                        };
                        

                        AniDBFile_State state = (AniDBFile_State) int.Parse(arrStatus[6]);

                        int viewdate = int.Parse(arrStatus[7]);
                        bool watched = viewdate > 0;

                        DateTime? watchedDate = null;
                        if (watched)
                        {
                            DateTime utcDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            utcDate = utcDate.AddSeconds(viewdate);

                            watchedDate = utcDate.ToLocalTime();
                        }
                        
                        return new AniDBMyList_AddFileResponse
                        {
                            MyListID = myListID,
                            State = state,
                            IsWatched = watched,
                            WatchedDate = watchedDate
                        };
                    }
                    break;
                }
            }
            throw new UnexpectedAniDBResponse(code, receivedData);
        }
    }
}