using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Gmail.v1;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;

public sealed class GoogleWorkspaceService
{
    private static readonly string[] Scopes =
    [
        GmailService.Scope.GmailModify,
        CalendarService.Scope.CalendarEvents,
        Oauth2Service.Scope.UserinfoEmail
    ];

    private readonly GoogleWorkspaceOptions _options;
    private readonly IWebHostEnvironment _environment;

    public GoogleWorkspaceService(IOptions<GoogleWorkspaceOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public string ResolvedCredentialsPath => ResolvePath(_options.CredentialsPath);
    public string ResolvedTokenDirectory => ResolvePath(_options.TokenDirectory);

    public GoogleWorkspaceStatus GetStatus()
    {
        return new GoogleWorkspaceStatus(
            File.Exists(ResolvedCredentialsPath),
            Directory.Exists(ResolvedTokenDirectory),
            ResolvedCredentialsPath,
            ResolvedTokenDirectory,
            _options.EnableGoogleCalendarWrite,
            _options.CalendarId,
            null);
    }

    public async Task<GoogleWorkspaceStatus> GetDetailedStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        if (!status.CredentialsFileFound)
        {
            return status;
        }

        try
        {
            var oauthService = await CreateOauthServiceAsync(cancellationToken);
            var userInfo = await oauthService.Userinfo.Get().ExecuteAsync(cancellationToken);
            return status with { AuthenticatedEmail = userInfo.Email };
        }
        catch
        {
            return status;
        }
    }

    public async Task<GmailService> CreateGmailServiceAsync(CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(cancellationToken);
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });
    }

    public async Task<CalendarService> CreateCalendarServiceAsync(CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(cancellationToken);
        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });
    }

    private async Task<Oauth2Service> CreateOauthServiceAsync(CancellationToken cancellationToken)
    {
        var credential = await GetCredentialAsync(cancellationToken);
        return new Oauth2Service(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });
    }

    private async Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ResolvedCredentialsPath))
        {
            throw new InvalidOperationException(
                $"Google OAuth client credentials were not found at '{ResolvedCredentialsPath}'. " +
                "Create a desktop OAuth client in Google Cloud, enable Gmail and Calendar APIs, then place the downloaded JSON file there.");
        }

        Directory.CreateDirectory(ResolvedTokenDirectory);

        await using var stream = new FileStream(ResolvedCredentialsPath, FileMode.Open, FileAccess.Read);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "me",
            cancellationToken,
            new FileDataStore(ResolvedTokenDirectory, true));
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }
}

public sealed record GoogleWorkspaceStatus(
    bool CredentialsFileFound,
    bool TokenDirectoryExists,
    string CredentialsPath,
    string TokenDirectory,
    bool GoogleCalendarWriteEnabled,
    string CalendarId,
    string? AuthenticatedEmail);
