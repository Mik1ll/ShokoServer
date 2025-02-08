using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.AniDB;

[JsonConverter(typeof(StringEnumConverter))]
public enum EpisodeType
{
    /// <summary>
    /// The episode type is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
    /// </summary>
    Other = 1,

    /// <summary>
    /// A normal episode.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// A special episode.
    /// </summary>
    Special = 3,

    /// <summary>
    /// A trailer.
    /// </summary>
    Trailer = 4,

    /// <summary>
    /// Either an opening-song, or an ending-song.
    /// </summary>
    ThemeSong = 5,

    /// <summary>
    /// AniDB parody type. Where else would this be useful?
    /// </summary>
    Parody = 6,

    /// <summary>
    /// A DVD or BD extra, e.g. BD-menu or deleted scenes.
    /// </summary>
    Extra = 7,
}
