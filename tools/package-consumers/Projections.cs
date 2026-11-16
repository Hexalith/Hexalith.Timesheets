var checkpoint = new Hexalith.Timesheets.Projections.TimesheetsProjectionCheckpoint(
    "consumer", "test", 7, Hexalith.Timesheets.Projections.ProjectionFreshness.Fresh);
var metadata = Hexalith.Timesheets.Projections.ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint);
if (!checkpoint.CanServeReads || metadata.Cursor != "7") throw new InvalidOperationException("Projection mapping failed");
