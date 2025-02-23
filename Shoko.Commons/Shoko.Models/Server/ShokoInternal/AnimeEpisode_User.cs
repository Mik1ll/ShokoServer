﻿using System;

namespace Shoko.Models.Server
{
    public class AnimeEpisode_User
    {
        #region Server DB columns

        public int AnimeEpisode_UserID { get; set; }
        public int JMMUserID { get; set; }
        public int AnimeEpisodeID { get; set; }
        public int AnimeSeriesID { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }

        #endregion
    }
}
