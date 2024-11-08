﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Providers.AniDB;

public abstract class AniDBRateLimiter
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Stopwatch _requestWatch = new();
    private readonly Stopwatch _activeTimeWatch = new();

    // Short Term:
    // A Client MUST NOT send more than 0.5 packets per second(that's one packet every two seconds, not two packets a second!)
    // The server will start to enforce the limit after the first 5 packets have been received.
    protected abstract int ShortDelay { get; init; }

    // Long Term:
    // A Client MUST NOT send more than one packet every four seconds over an extended amount of time.
    // An extended amount of time is not defined. Use common sense.
    protected abstract int LongDelay { get; init; }

    // Switch to longer delay after a short period
    protected abstract long ShortPeriod { get; init; }

    // Switch to shorter delay after inactivity
    protected abstract long ResetPeriod { get; init; }

    protected AniDBRateLimiter(ILogger logger)
    {
        _logger = logger;
        _requestWatch.Start();
        _activeTimeWatch.Start();
    }

    private void ResetRate()
    {
        var elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
        _activeTimeWatch.Restart();
        _logger.LogTrace("Rate is reset. Active time was {Time} ms", elapsedTime);
    }

    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> action, bool forceShortDelay = false)
    {
        await _lock.WaitAsync();
        try
        {
            var delay = _requestWatch.ElapsedMilliseconds;
            if (delay > ResetPeriod) ResetRate();
            var currentDelay = !forceShortDelay && _activeTimeWatch.ElapsedMilliseconds > ShortPeriod ? LongDelay : ShortDelay;

            if (delay > currentDelay)
            {
                _logger.LogTrace("Time since last request is {Delay} ms, not throttling", delay);
                _logger.LogTrace("Sending AniDB command");
                return await action();
            }

            // add 50ms for good measure
            var waitTime = currentDelay - (int)delay + 50;

            _logger.LogTrace("Time since last request is {Delay} ms, throttling for {Time}", delay, waitTime);
            await Task.Delay(waitTime);

            _logger.LogTrace("Sending AniDB command");
            return await action();
        }
        finally
        {
            _requestWatch.Restart();
            _lock.Release();
        }
    }
}
