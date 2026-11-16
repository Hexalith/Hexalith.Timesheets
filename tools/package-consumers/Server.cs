var decision = Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision.Denied("consumer");
if (decision.IsAuthorized || decision.Reason != "consumer") throw new InvalidOperationException("Authorization decision failed");
