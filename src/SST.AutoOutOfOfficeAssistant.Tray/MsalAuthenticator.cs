using Azure.Core;
using SST.AutoOutOfOfficeAssistant.Core;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>The user must sign in (again) interactively.</summary>
internal sealed class SignInRequiredException : Exception
{
    public SignInRequiredException()
        : base("Sign-in required.")
    {
    }

    public SignInRequiredException(Exception innerException)
        : base("Sign-in required.", innerException)
    {
    }

    public SignInRequiredException(string message)
        : base(message)
    {
    }

    public SignInRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Returns true when <paramref name="exception"/> or one of its inner exceptions means "sign in again".</summary>
    public static bool IsCause(Exception? exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is SignInRequiredException or MsalUiRequiredException)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Signs the user in with MSAL and the Windows broker (WAM) and hands out delegated Graph tokens.
/// On an Entra-joined PC the Windows account is used silently; otherwise the user picks an account once.
/// The token cache is encrypted with DPAPI for the current Windows user.
/// </summary>
internal sealed class MsalAuthenticator
{
    /// <summary>The delegated Graph permissions (MSAL adds openid, profile and offline_access).</summary>
    public static readonly string[] Scopes = ["User.Read", "Calendars.ReadBasic", "MailboxSettings.ReadWrite"];

    private readonly IPublicClientApplication app;
    private IAccount? account;

    private MsalAuthenticator(IPublicClientApplication app) => this.app = app;

    /// <summary>Gets the signed-in user's name, or null.</summary>
    public string? UserName => this.account?.Username;

    /// <summary>Gets the signed-in user's key (object ID), or null.</summary>
    public string? UserKey => Core.UserKey.Normalize(this.account?.HomeAccountId?.ObjectId);

    /// <summary>Gets the MSAL account identifier to remember between starts, or null.</summary>
    public string? AccountId => this.account?.HomeAccountId?.Identifier;

    /// <summary>Creates the authenticator.</summary>
    /// <param name="options">The options.</param>
    /// <param name="parentWindow">Returns the window the sign-in dialog belongs to.</param>
    /// <returns>The authenticator.</returns>
    public static async Task<MsalAuthenticator> CreateAsync(TrayOptions options, Func<IntPtr> parentWindow)
    {
        var app = PublicClientApplicationBuilder.Create(options.ClientId)
            .WithAuthority(options.Authority)
            .WithDefaultRedirectUri()
            .WithParentActivityOrWindow(parentWindow)
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "AOOOA" })
            .Build();

        var storage = new StorageCreationPropertiesBuilder(AppPaths.TokenCacheFileName, AppPaths.DataDirectory).Build();
        var cacheHelper = await MsalCacheHelper.CreateAsync(storage).ConfigureAwait(true);
        cacheHelper.RegisterCache(app.UserTokenCache);

        return new MsalAuthenticator(app);
    }

    /// <summary>
    /// Signs in without UI: the remembered account, or on first start the Windows account.
    /// </summary>
    /// <param name="rememberedAccountId">The account from the last session, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when an account is selected (a network error keeps it selected for a later retry).</returns>
    public async Task<bool> TrySignInSilentlyAsync(string? rememberedAccountId, CancellationToken cancellationToken)
    {
        var accounts = await this.app.GetAccountsAsync().ConfigureAwait(true);
        var candidate = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == rememberedAccountId)
            ?? (rememberedAccountId is null ? PublicClientApplication.OperatingSystemAccount : null);
        if (candidate is null)
        {
            return false;
        }

        try
        {
            var result = await this.app.AcquireTokenSilent(Scopes, candidate).ExecuteAsync(cancellationToken).ConfigureAwait(true);
            this.account = result.Account;
            return true;
        }
        catch (MsalUiRequiredException)
        {
            return false;
        }
        catch (Exception ex) when (ex is MsalException or HttpRequestException)
        {
            FileLog.Error("Silent sign-in failed", ex);
            if (candidate == PublicClientApplication.OperatingSystemAccount)
            {
                // No remembered account to keep: the user signs in interactively.
                return false;
            }

            // Probably offline at startup: keep the remembered account, the next sync tries again.
            this.account = candidate;
            return true;
        }
    }

    /// <summary>Shows the account picker and signs in.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SignInInteractiveAsync(CancellationToken cancellationToken)
    {
        var result = await this.app.AcquireTokenInteractive(Scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(true);
        this.account = result.Account;
    }

    /// <summary>Signs out: forgets the account and removes its tokens from the cache.</summary>
    /// <returns>A task.</returns>
    public async Task SignOutAsync()
    {
        if (this.account is not null)
        {
            await this.app.RemoveAsync(this.account).ConfigureAwait(true);
        }

        this.account = null;
    }

    /// <summary>Returns a Graph access token for the signed-in user without UI.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The token.</returns>
    /// <exception cref="SignInRequiredException">The user must sign in interactively.</exception>
    public async Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var current = this.account ?? throw new SignInRequiredException();
        try
        {
            var result = await this.app.AcquireTokenSilent(Scopes, current).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
        catch (MsalUiRequiredException ex)
        {
            throw new SignInRequiredException(ex);
        }
    }
}

/// <summary>Adapts <see cref="MsalAuthenticator"/> to the <see cref="TokenCredential"/> the Graph SDK expects.</summary>
/// <param name="authenticator">The authenticator.</param>
internal sealed class MsalTokenCredential(MsalAuthenticator authenticator) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        this.GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        await authenticator.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
}
