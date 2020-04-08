using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class ContentEncodingNegotiator
{
    // List of encodings by preference order with their associated extension so that we can easily handle "*".
    private static readonly StringSegment[] _preferredEncodings =
        new StringSegment[] { "br", "gzip" };

    private static readonly Dictionary<StringSegment, string> _encodingExtensionMap = new Dictionary<StringSegment, string>(StringSegmentComparer.OrdinalIgnoreCase)
    {
        ["br"] = ".br",
        ["gzip"] = ".gz"
    };

    private readonly RequestDelegate _next;
    private readonly IFileProvider _webRootFileProvider;

    public ContentEncodingNegotiator(RequestDelegate next,
        IFileProvider webRootFileProvider)
    {
        _next = next;
        _webRootFileProvider = webRootFileProvider;
    }

    public Task InvokeAsync(HttpContext context)
    {
        NegotiateEncoding(context);
        return _next(context);
    }

    private void NegotiateEncoding(HttpContext context)
    {
        var accept = context.Request.Headers[HeaderNames.AcceptEncoding];

        if (StringValues.IsNullOrEmpty(accept))
        {
            return;
        }

        if (!StringWithQualityHeaderValue.TryParseList(accept, out var encodings) || encodings.Count == 0)
        {
            return;
        }

        var selectedEncoding = StringSegment.Empty;
        var selectedEncodingQuality = .0;

        foreach (var encoding in encodings)
        {
            var encodingName = encoding.Value;
            var quality = encoding.Quality.GetValueOrDefault(1);

            if (quality >= double.Epsilon && quality >= selectedEncodingQuality)
            {
                if (quality == selectedEncodingQuality)
                {
                    selectedEncoding = PickPreferredEncoding(context, selectedEncoding, encoding);
                }
                else if (_encodingExtensionMap.TryGetValue(encodingName, out var encodingExtension) && ResourceExists(context, encodingExtension))
                {
                    selectedEncoding = encodingName;
                    selectedEncodingQuality = quality;
                }

                if (StringSegment.Equals("*", encodingName, StringComparison.Ordinal))
                {
                    // If we *, pick the first preferrent encoding for which a resource exists.
                    selectedEncoding = PickPreferredEncoding(context, default, encoding);
                    selectedEncodingQuality = quality;
                }

                if (StringSegment.Equals("identity", encodingName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedEncoding = StringSegment.Empty;
                    selectedEncodingQuality = quality;
                }
            }
        }

        if (_encodingExtensionMap.TryGetValue(selectedEncoding, out var extension))
        {
            context.Request.Path = context.Request.Path + extension;
            context.Response.Headers[HeaderNames.ContentEncoding] = selectedEncoding.Value;
            context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.ContentEncoding);
        }

        return;

        StringSegment PickPreferredEncoding(HttpContext c, StringSegment selected, StringWithQualityHeaderValue encoding)
        {
            foreach (var preferredEncoding in _preferredEncodings)
            {
                if (preferredEncoding == selected)
                {
                    return selected;
                }

                if ((preferredEncoding == encoding.Value || encoding.Value == "*") && ResourceExists(c, _encodingExtensionMap[preferredEncoding]))
                {
                    return preferredEncoding;
                }
            }

            return StringSegment.Empty;
        }
    }

    private bool ResourceExists(HttpContext context, string extension) =>
        _webRootFileProvider.GetFileInfo(context.Request.Path + extension).Exists;
}



