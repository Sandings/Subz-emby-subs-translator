using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubZ.Plugin.Services;

public sealed class TranslationRetryPolicy
{
    private readonly int _maxRetries;
    private readonly int _minBackoffMs;
    private readonly int _maxBackoffMs;

    public TranslationRetryPolicy(int maxRetries, int minBackoffMs = 1000, int maxBackoffMs = 10000)
    {
        _maxRetries = Math.Max(0, maxRetries);
        _minBackoffMs = Math.Max(100, minBackoffMs);
        _maxBackoffMs = Math.Max(_minBackoffMs, maxBackoffMs);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (shouldRetry is null)
        {
            throw new ArgumentNullException(nameof(shouldRetry));
        }

        Exception? last = null;
        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                last = ex;

                if (attempt >= _maxRetries || !shouldRetry(ex))
                {
                    throw;
                }

                var wait = ComputeBackoff(attempt);
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }

        throw last ?? new InvalidOperationException("Retry policy exhausted without a concrete exception.");
    }

    private int ComputeBackoff(int attempt)
    {
        var factor = Math.Pow(2, attempt);
        var value = (int)Math.Min(_maxBackoffMs, _minBackoffMs * factor);
        return Math.Max(_minBackoffMs, value);
    }
}

