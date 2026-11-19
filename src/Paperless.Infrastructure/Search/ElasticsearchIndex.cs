using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;

namespace Paperless.Infrastructure.Search;

public sealed class ElasticsearchIndex(HttpClient client, IOptions<SearchOptions> options) : ISearchIndex
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly SemaphoreSlim _creation = new(1, 1);

    public async Task<string> GetIdentityAsync(CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken);
        using var response = await SendAsync(HttpMethod.Get, IndexPath + "/_settings?flat_settings=true", null, cancellationToken);
        using var document = await ParseAsync(response, cancellationToken);
        try
        {
            var identity = document.RootElement.GetProperty(options.Value.IndexName).GetProperty("settings").GetProperty("index.uuid").GetString();
            return identity is { Length: > 0 and <= 128 } ? identity : throw Unavailable();
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException) { throw Unavailable(exception); }
    }

    public async Task WriteAsync(IndexDocument snapshot, CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken);
        var path = $"{IndexPath}/_doc/{snapshot.DocumentId}?version={snapshot.Revision.ToString(CultureInfo.InvariantCulture)}&version_type=external_gte&refresh=wait_for";
        using var response = await SendAsync(HttpMethod.Put, path, snapshot, cancellationToken, allowConflict: true);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new SearchException("IndexRevisionConflict", "A newer search revision exists. Stale deliveries are ignored; after a database restore rebuild into a fresh search index.", false);
        // Equal revisions always have the same source. The repository discards superseded claims.
    }

    public async Task<SearchCandidates> SearchAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken);
        var match = new Dictionary<string, object?>
        {
            ["query"] = query.Query,
            ["fields"] = new[] { "title^3", "fileName^2", "text" },
            ["type"] = "best_fields",
            ["operator"] = "and"
        };
        if (query.Fuzzy) { match["fuzziness"] = "AUTO"; match["prefix_length"] = 1; match["max_expansions"] = 25; }
        var body = new
        {
            from = (query.Page - 1) * query.PageSize,
            size = query.PageSize,
            track_total_hits = true,
            _source = new[] { "documentId", "revision", "jobId" },
            query = new { @bool = new { filter = new[] { new { term = new { isDeleted = false } } }, must = new[] { new { multi_match = match } } } },
            sort = new object[] { new Dictionary<string, string> { ["_score"] = "desc" }, new { documentId = "asc" } },
            highlight = new { pre_tags = new[] { "" }, post_tags = new[] { "" }, fields = new { text = new { fragment_size = 200, number_of_fragments = 1 } } }
        };
        using var response = await SendAsync(HttpMethod.Post, IndexPath + "/_search", body, cancellationToken);
        using var document = await ParseAsync(response, cancellationToken);
        try
        {
            var root = document.RootElement;
            if (root.GetProperty("timed_out").GetBoolean() || root.GetProperty("_shards").GetProperty("failed").GetInt32() != 0)
                throw Unavailable();
            var hits = root.GetProperty("hits");
            var result = new List<SearchCandidate>();
            foreach (var hit in hits.GetProperty("hits").EnumerateArray())
            {
                var source = hit.GetProperty("_source");
                string? snippet = null;
                if (hit.TryGetProperty("highlight", out var highlight) && highlight.TryGetProperty("text", out var fragments) && fragments.GetArrayLength() > 0)
                {
                    snippet = fragments[0].GetString();
                    if (snippet?.Length > 500) snippet = snippet[..500];
                }
                result.Add(new SearchCandidate(source.GetProperty("documentId").GetInt32(), source.GetProperty("revision").GetInt64(),
                    source.GetProperty("jobId").GetGuid(), hit.GetProperty("_score").GetDouble(), snippet));
            }
            var total = hits.GetProperty("total").GetProperty("value").GetInt64();
            if (total < result.Count || result.Count > query.PageSize || hits.GetProperty("total").GetProperty("relation").GetString() != "eq" ||
                result.Any(candidate => candidate.DocumentId <= 0 || candidate.Revision <= 0 || candidate.JobId == Guid.Empty || !double.IsFinite(candidate.Score)))
                throw Unavailable();
            return new SearchCandidates(total, result);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw Unavailable(exception);
        }
    }

    private string IndexPath => options.Value.IndexName;

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        // Check each operation so a lost/recreated index is detected rather than cached forever.
        using var exists = await SendAsync(HttpMethod.Head, IndexPath, null, cancellationToken, allowMissing: true);
        if (exists.IsSuccessStatusCode) return;
        await _creation.WaitAsync(cancellationToken);
        try
        {
            using var check = await SendAsync(HttpMethod.Head, IndexPath, null, cancellationToken, allowMissing: true);
            if (check.IsSuccessStatusCode) return;
            var mapping = new
            {
                settings = new { number_of_shards = 1, number_of_replicas = 0 },
                mappings = new
                {
                    dynamic = "strict",
                    _meta = new { paperlessSchema = 1 },
                    properties = new
                    {
                        documentId = new { type = "integer" },
                        revision = new { type = "long" },
                        isDeleted = new { type = "boolean" },
                        jobId = new { type = "keyword" },
                        title = new { type = "text" },
                        fileName = new { type = "text" },
                        text = new { type = "text", index_options = "offsets" }
                    }
                }
            };
            using var created = await SendAsync(HttpMethod.Put, IndexPath, mapping, cancellationToken, allowExisting: true);
        }
        finally { _creation.Release(); }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken,
        bool allowMissing = false, bool allowConflict = false, bool allowExisting = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: Json);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
            if (response.IsSuccessStatusCode || (allowMissing && response.StatusCode == HttpStatusCode.NotFound) ||
                (allowConflict && response.StatusCode == HttpStatusCode.Conflict)) return response;
            if (allowExisting && response.StatusCode == HttpStatusCode.BadRequest)
            {
                using var error = await ParseAsync(response, timeout.Token);
                if (error.RootElement.TryGetProperty("error", out var detail) && detail.TryGetProperty("type", out var type) &&
                    type.GetString() == "resource_already_exists_exception") return response;
            }
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new SearchException(status is 401 or 403 or 400 ? "SearchConfigurationError" : "SearchUnavailable",
                status is 401 or 403 or 400 ? "The search service rejected its configuration or request. Check the server configuration." :
                "The search service is temporarily unavailable. Retry after it recovers.", status is not (400 or 401 or 403));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException exception) { throw Unavailable(exception); }
    }

    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try { return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken); }
        catch (JsonException exception) { throw Unavailable(exception); }
    }

    private static SearchException Unavailable(Exception? inner = null) =>
        new("SearchUnavailable", "The search service is temporarily unavailable. Retry after it recovers.", true, inner);
}
