var options = new Hexalith.Timesheets.Client.TimesheetsClientOptions { Endpoint = new Uri("https://example.invalid") };
if (options.Endpoint.Host != "example.invalid") throw new InvalidOperationException("Client options failed");
