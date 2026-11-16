var tenant = new Hexalith.Timesheets.Contracts.References.TenantReference("consumer");
if (tenant.TenantId != "consumer") throw new InvalidOperationException("Tenant reference failed");
