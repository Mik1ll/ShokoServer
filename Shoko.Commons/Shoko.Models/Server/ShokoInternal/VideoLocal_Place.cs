﻿namespace Shoko.Models.Server
{
    public class VideoLocal_Place
    {
        public int VideoLocal_Place_ID { get; set; }
        public int VideoLocalID { get; set; }
        public string FilePath { get; set; }
        public int ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }
    }
}
