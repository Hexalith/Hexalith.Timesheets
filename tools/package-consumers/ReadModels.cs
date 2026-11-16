if (Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection.ShardKey(255)
        != "timesheets:magic-links:token-hash-capability-index:v2:shard:FF"
    || Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection.StateStoreName
        != "timesheets-readmodels")
    throw new InvalidOperationException("Read-model address compatibility failed");
