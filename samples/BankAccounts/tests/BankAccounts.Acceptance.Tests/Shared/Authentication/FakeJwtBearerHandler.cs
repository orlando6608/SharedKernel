using BankAccounts.Acceptance.Tests.Shared.Authentication.Events;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System.Collections;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace BankAccounts.Acceptance.Tests.Shared.Authentication
{
    internal class FakeJwtBearerHandler : AuthenticationHandler<FakeJwtBearerOptions>
    {
        public FakeJwtBearerHandler(IOptionsMonitor<FakeJwtBearerOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ISystemClock clock) : base(options, logger,
            encoder, clock)
        { }


        /// <summary>
        /// The handler calls methods on the events which give the application control at certain points where processing is occurring.
        /// If it is not provided a default instance is supplied which does nothing when the methods are called.
        /// </summary>
        protected new JwtBearerEvents Events
        {
            get
            {
                if (base.Events is JwtBearerEvents)
                {
                    return base.Events as JwtBearerEvents;
                }
                base.Events = new JwtBearerEvents();
                return base.Events as JwtBearerEvents;
            }
            set => base.Events = value;
        }

        /// <summary>
        /// Searches the 'Authorization' header for a 'Bearer' token.
        /// </summary>
        /// <returns></returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // Give application opportunity to find from a different location, adjust, or reject token
                var messageReceivedContext = new Events.MessageReceivedContext(Context, Scheme, Options);

                // event can set the token
                await Events.MessageReceived(messageReceivedContext);
                //if (messageReceivedContext.Result != null)
                //{
                //    return messageReceivedContext.Result;
                //}

                //If application retrieved token from somewhere else, use that.
                var token = messageReceivedContext.Token;

                if (string.IsNullOrEmpty(token))
                {
                    string authorization = Request.Headers["Authorization"];

                    // If no authorization header found, nothing to process further
                    if (string.IsNullOrEmpty(authorization))
                    {
                        return AuthenticateResult.NoResult();
                    }

                    if (authorization.StartsWith(FakeJwtBearerDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
                    {
                        token = authorization.Substring($"{FakeJwtBearerDefaults.AuthenticationScheme} ".Length).Trim();
                    }

                    // If no token found, no further work possible
                    if (string.IsNullOrEmpty(token))
                    {
                        return AuthenticateResult.NoResult();
                    }
                }

                var tokenDecoded = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(token);

                var id = new ClaimsIdentity("Identity.Application", ClaimTypes.Sid, ClaimTypes.Role);

                id.AddClaim(new Claim(ClaimTypes.NameIdentifier, "pedro"));

                foreach (var td in tokenDecoded!)
                {
                    if (td.Key == "sub")
                    {
                        id.AddClaim(new Claim("sub", td.Value.ToString()));
                        if (tokenDecoded.All(c => c.Key != "name"))
                        {
                            id.AddClaim(new Claim("name", td.Value.ToString()));
                        }
                    }
                    else
                    {
                        if (td.Value is string)
                        {
                            id.AddClaim(new Claim(td.Key, td.Value));
                        }
                        else if (td.Value is IEnumerable)
                        {
                            foreach (string subValue in td.Value)
                            {
                                id.AddClaim(new Claim(td.Key, subValue));
                            }
                        }
                        else
                        {
                            throw new Exception("Unknown type");
                        }
                    }
                }

                var principal = new ClaimsPrincipal(id);

                var tokenValidatedContext = new TokenValidatedContext(Context, Scheme, Options)
                {
                    Principal = principal
                };

                await Events.TokenValidated(tokenValidatedContext);
                //if (tokenValidatedContext.Result != null)
                //{
                //    return tokenValidatedContext.Result;
                //}

                if (Options.SaveToken)
                {
                    tokenValidatedContext.Properties.StoreTokens(new[]
                    {
                        new AuthenticationToken { Name = "access_token", Value = token }
                    });
                }

                tokenValidatedContext.Success();
                return tokenValidatedContext.Result;
            }
            catch (Exception ex)
            {
                var authenticationFailedContext = new AuthenticationFailedContext(Context, Scheme, Options)
                {
                    Exception = ex
                };

                await Events.AuthenticationFailed(authenticationFailedContext);
                //if (authenticationFailedContext.Result != null)
                //{
                return authenticationFailedContext.Result;
                //}

                //throw;
            }
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var authResult = await HandleAuthenticateOnceSafeAsync();
            var eventContext = new JwtBearerChallengeContext(Context, Scheme, Options, properties)
            {
                AuthenticateFailure = authResult.Failure
            };

            await Events.Challenge(eventContext);
            if (eventContext.Handled)
            {
                return;
            }

            Response.StatusCode = 401;

            if (string.IsNullOrEmpty(eventContext.Error) &&
                string.IsNullOrEmpty(eventContext.ErrorDescription) &&
                string.IsNullOrEmpty(eventContext.ErrorUri))
            {
                Response.Headers.Append(HeaderNames.WWWAuthenticate, Options.Challenge);
            }
            else
            {
                // https://tools.ietf.org/html/rfc6750#section-3.1
                // WWW-Authenticate: Bearer realm="example", error="invalid_token", error_description="The access token expired"
                var builder = new StringBuilder(Options.Challenge);
                if (Options.Challenge.IndexOf(" ", StringComparison.Ordinal) > 0)
                {
                    // Only add a comma after the first param, if any
                    builder.Append(',');
                }
                if (!string.IsNullOrEmpty(eventContext.Error))
                {
                    builder.Append(" error=\"");
                    builder.Append(eventContext.Error);
                    builder.Append("\"");
                }
                if (!string.IsNullOrEmpty(eventContext.ErrorDescription))
                {
                    if (!string.IsNullOrEmpty(eventContext.Error))
                    {
                        builder.Append(",");
                    }

                    builder.Append(" error_description=\"");
                    builder.Append(eventContext.ErrorDescription);
                    builder.Append('\"');
                }
                if (!string.IsNullOrEmpty(eventContext.ErrorUri))
                {
                    if (!string.IsNullOrEmpty(eventContext.Error) ||
                        !string.IsNullOrEmpty(eventContext.ErrorDescription))
                    {
                        builder.Append(",");
                    }

                    builder.Append(" error_uri=\"");
                    builder.Append(eventContext.ErrorUri);
                    builder.Append('\"');
                }

                Response.Headers.Append(HeaderNames.WWWAuthenticate, builder.ToString());
            }
        }

    }
}
