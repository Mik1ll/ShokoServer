﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisodeRepository : BaseCachedRepository<SVR_AnimeEpisode, int>
{
    private PocoIndex<int, SVR_AnimeEpisode, int> Series;
    private PocoIndex<int, SVR_AnimeEpisode, int> EpisodeIDs;

    public AnimeEpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeEpisode_User.Delete(
                RepoFactory.AnimeEpisode_User.GetByEpisodeID(cr.AnimeEpisodeID));
        };
    }

    protected override int SelectKey(SVR_AnimeEpisode entity)
    {
        return entity.AnimeEpisodeID;
    }

    public override void PopulateIndexes()
    {
        Series = Cache.CreateIndex(a => a.AnimeSeriesID);
        EpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
    }

    public override void RegenerateDb()
    {
    }

    public List<SVR_AnimeEpisode> GetBySeriesID(int seriesid)
    {
        return ReadLock(() => Series.GetMultiple(seriesid));
    }


    public SVR_AnimeEpisode GetByAniDBEpisodeID(int epid)
    {
        return ReadLock(() => EpisodeIDs.GetOne(epid));
    }


    /// <summary>
    /// Get the AnimeEpisode
    /// </summary>
    /// <param name="name">The filename of the anime to search for.</param>
    /// <returns>the AnimeEpisode given the file information</returns>
    public SVR_AnimeEpisode GetByFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var eps = RepoFactory.VideoLocalPlace.GetAll()
            .Where(v => name.Equals(v?.FilePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(),
                StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.VideoLocal.GetByID(a.VideoLocalID)).Where(a => a != null)
            .SelectMany(a => GetByHash(a.Hash)).ToArray();
        var ep = eps.FirstOrDefault(a => a.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode);
        return ep ?? eps.FirstOrDefault();
    }


    /// <summary>
    /// Get all the AnimeEpisode records associate with an AniDB_File record
    /// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
    /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
    /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public List<SVR_AnimeEpisode> GetByHash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return [];
        return RepoFactory.CrossRef_File_Episode.GetByHash(hash)
            .Select(a => GetByAniDBEpisodeID(a.EpisodeID))
            .Where(a => a != null)
            .ToList();
    }

    private const string MultipleReleasesIgnoreVariationsWithAnimeQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE ani.AnimeID = :animeID AND vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsWithAnimeQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE ani.AnimeID = :animeID AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesIgnoreVariationsQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

    public List<SVR_AnimeEpisode> GetWithMultipleReleases(bool ignoreVariations, int? animeID = null)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            if (animeID.HasValue && animeID.Value > 0)
            {
                var animeQuery = ignoreVariations ? MultipleReleasesIgnoreVariationsWithAnimeQuery : MultipleReleasesCountVariationsWithAnimeQuery;
                return session.CreateSQLQuery(animeQuery)
                    .AddScalar("EpisodeID", NHibernateUtil.Int32)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            }

            var query = ignoreVariations ? MultipleReleasesIgnoreVariationsQuery : MultipleReleasesCountVariationsQuery;
            return session.CreateSQLQuery(query)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, anidbEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.anidbEpisode is not null)
            .OrderBy(tuple => tuple.anidbEpisode!.AnimeID)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeTypeEnum)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!)
            .ToList();
    }

    private const string DuplicateFilesWithAnimeQuery = @"
SELECT
    ani.EpisodeID
FROM
    (
        SELECT
            vlp.VideoLocal_Place_ID,
            vl.FileSize,
            vl.Hash
        FROM
            VideoLocal AS vl
        INNER JOIN
            VideoLocal_Place AS vlp
            ON vlp.VideoLocalID = vl.VideoLocalID
        WHERE
            vl.Hash != ''
        GROUP BY
            vl.VideoLocalID
        HAVING
            COUNT(vl.VideoLocalID) > 1
    ) AS filtered_vlp
INNER JOIN
    CrossRef_File_Episode ani
    ON filtered_vlp.Hash = ani.Hash
       AND filtered_vlp.FileSize = ani.FileSize
WHERE ani.AnimeID = :animeID
GROUP BY
    ani.EpisodeID
";

    private const string DuplicateFilesQuery = @"
SELECT
    ani.EpisodeID
FROM
    (
        SELECT
            vlp.VideoLocal_Place_ID,
            vl.FileSize,
            vl.Hash
        FROM
            VideoLocal AS vl
        INNER JOIN
            VideoLocal_Place AS vlp
            ON vlp.VideoLocalID = vl.VideoLocalID
        WHERE
            vl.Hash != ''
        GROUP BY
            vl.VideoLocalID
        HAVING
            COUNT(vl.VideoLocalID) > 1
    ) AS filtered_vlp
INNER JOIN
    CrossRef_File_Episode ani
    ON filtered_vlp.Hash = ani.Hash
       AND filtered_vlp.FileSize = ani.FileSize
GROUP BY
    ani.EpisodeID
";

    public IEnumerable<SVR_AnimeEpisode> GetWithDuplicateFiles(int? animeID = null)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            if (animeID.HasValue && animeID.Value > 0)
            {
                return session.CreateSQLQuery(DuplicateFilesWithAnimeQuery)
                    .AddScalar("EpisodeID", NHibernateUtil.Int32)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            }

            return session.CreateSQLQuery(DuplicateFilesQuery)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, anidbEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.anidbEpisode is not null)
            .OrderBy(tuple => tuple.anidbEpisode!.AnimeID)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeTypeEnum)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!);
    }

    public List<SVR_AnimeEpisode> GetUnwatchedEpisodes(int seriesid, int userid)
    {
        var eps =
            RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(userid, seriesid)
                .Where(a => a.WatchedDate.HasValue)
                .Select(a => a.AnimeEpisodeID)
                .ToList();
        return GetBySeriesID(seriesid).Where(a => !eps.Contains(a.AnimeEpisodeID)).ToList();
    }

    public List<SVR_AnimeEpisode> GetAllWatchedEpisodes(int userid, DateTime? after_date)
    {
        var eps = RepoFactory.AnimeEpisode_User.GetByUserID(userid).Where(a => a.IsWatched())
            .Where(a => a.WatchedDate > after_date).OrderBy(a => a.WatchedDate).ToList();
        var list = new List<SVR_AnimeEpisode>();
        foreach (var ep in eps)
        {
            list.Add(GetByID(ep.AnimeEpisodeID));
        }

        return list;
    }

    public List<SVR_AnimeEpisode> GetEpisodesWithNoFiles(bool includeSpecials, bool includeOnlyAired = false)
    {
        var all = GetAll().Where(a =>
            {
                var aniep = a.AniDB_Episode;
                if (aniep?.HasAired ?? false)
                {
                    return false;
                }

                if (aniep.EpisodeType != (int)EpisodeType.Episode &&
                    aniep.EpisodeType != (int)EpisodeType.Special)
                {
                    return false;
                }

                if (!includeSpecials &&
                    aniep.EpisodeType == (int)EpisodeType.Special)
                {
                    return false;
                }

                if (includeOnlyAired && !aniep.HasAired)
                {
                    return false;
                }

                return a.VideoLocals.Count == 0;
            })
            .ToList();
        all.Sort((a1, a2) =>
        {
            var name1 = a1.AnimeSeries?.PreferredTitle;
            var name2 = a2.AnimeSeries?.PreferredTitle;

            if (!string.IsNullOrEmpty(name1) && !string.IsNullOrEmpty(name2))
            {
                return string.Compare(name1, name2, StringComparison.Ordinal);
            }

            if (string.IsNullOrEmpty(name1))
            {
                return 1;
            }

            if (string.IsNullOrEmpty(name2))
            {
                return -1;
            }

            return a1.AnimeSeriesID.CompareTo(a2.AnimeSeriesID);
        });

        return all;
    }
}
