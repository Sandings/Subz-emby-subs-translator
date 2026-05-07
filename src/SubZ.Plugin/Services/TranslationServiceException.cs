using System;

namespace SubZ.Plugin.Services;

public sealed class TranslationServiceException : Exception
{
    public TranslationServiceException(
        string message,
        Exception? innerException = null,
        int? statusCode = null,
        bool isRetryable = true)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }

    public int? StatusCode { get; }

    public bool IsRetryable { get; }
}

