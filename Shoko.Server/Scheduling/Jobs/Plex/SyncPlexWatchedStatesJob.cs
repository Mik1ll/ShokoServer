using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Collection;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Plex.TVShow;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Plex;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SyncPlexWatchedStatesJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly IUserDataService _userDataService;
    public SVR_JMMUser User { get; set; }

    public override string TypeName => "Sync Plex States for User";

    public override string Title => "Syncing Plex States for User";
    public override Dictionary<string, object> Details => new()
    {
        { "User", User.Username }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} -> User: {Name}", nameof(SyncTraktCollectionSeriesJob), User.Username);
        var settings = _settingsProvider.GetSettings();
        foreach (var section in PlexHelper.GetForUser(User).GetDirectories().Where(a => settings.Plex.Libraries.Contains(a.Key)))
        {
            var allSeries = ((SVR_Directory)section).GetShows();
            foreach (var series in allSeries)
            {
                var episodes = ((SVR_PlexLibrary)series)?.GetEpisodes()?.Where(s => s != null);
                if (episodes == null) continue;

                foreach (var ep in episodes)
                {
                    using var scope = _logger.BeginScope(ep.Key);
                    var episode = (SVR_Episode)ep;

                    var animeEpisode = episode.AnimeEpisode;


                    _logger.LogInformation("Processing episode {Title} of {SeriesName}", episode.Title, series.Title);
                    if (animeEpisode == null)
                    {
                        var filePath = episode.Media[0].Part[0].File;
                        _logger.LogTrace("Episode not found in Shoko, skipping - {Filename} ({FilePath})", Path.GetFileName(filePath), filePath);
                        continue;
                    }

                    var userRecord = animeEpisode.GetUserRecord(User.JMMUserID);
                    var isWatched = episode.ViewCount is > 0;
                    var lastWatched = userRecord?.WatchedDate;
                    if ((userRecord?.WatchedCount ?? 0) == 0 && isWatched && episode.LastViewedAt != null)
                    {
                        lastWatched = FromUnixTime((long)episode.LastViewedAt);
                        _logger.LogTrace("Last watched date is {LastWatched}", lastWatched);
                    }

                    var video = animeEpisode.VideoLocals?.FirstOrDefault();
                    if (video == null) continue;

                    var alreadyWatched = animeEpisode.VideoLocals
                        .Select(a => _vlUsers.GetByUserIDAndVideoLocalID(User.JMMUserID, a.VideoLocalID))
                        .Where(a => a != null)
                        .Any(x => x.WatchedDate is not null || x.WatchedCount > 0);

                    if (!alreadyWatched && userRecord != null)
                    {
                        alreadyWatched = userRecord.IsWatched;
                    }

                    _logger.LogTrace("Already watched in shoko? {AlreadyWatched} Has been watched in plex? {IsWatched}", alreadyWatched, isWatched);

                    if (alreadyWatched && !isWatched)
                    {
                        _logger.LogInformation("Marking episode watched in plex");
                        episode.Scrobble();
                    }

                    if (isWatched && !alreadyWatched)
                    {
                        _logger.LogInformation("Marking episode watched in Shoko");
                        await _userDataService.SaveVideoUserData(User, video, new() { LastPlayedAt = lastWatched ?? DateTime.Now });
                    }
                }
            }
        }
    }

    private DateTime FromUnixTime(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(unixTime);
    }

    public SyncPlexWatchedStatesJob(ISettingsProvider settingsProvider, VideoLocal_UserRepository vlUsers, IUserDataService userDataService)
    {
        _settingsProvider = settingsProvider;
        _vlUsers = vlUsers;
        _userDataService = userDataService;
    }

    protected SyncPlexWatchedStatesJob() { }
}
