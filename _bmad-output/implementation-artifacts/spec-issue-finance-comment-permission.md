---
title: 'Issue independent finance permission for Time Entry comments'
type: 'bugfix'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
---

<frozen-after-approval reason="Story 6.1 follow-up under AD-7">

## Intent

Use the server's existing comment publication authority to issue a separate decision for finance disclosure of the exact text. Finance output requires both that decision and the tenant export policy. A caller-supplied export allowance and an external-display decision cannot grant finance permission.

Legacy events retain their publication and external-display behavior. A missing finance decision excludes the text from export. Existing command retries must still verify the original stored effect. A receipt can authorize delivery only when it matches the content currently authorized for export.

</frozen-after-approval>

## Implementation Notes

- The authorized command path uses `ITimeEntryCommentPublicationAuthority.MayExportUnredacted` to issue a decision for the exact text. The default implementation excludes it. The caller's redaction requirement can restrict finance disclosure.
- Publication version 3 carries the finance decision. Updated readers handle versions 1 and 2 and exclude their text from finance output. Older ledger readers withhold version 3 comments. Upgrade evidence projectors before deploying version 3 writers.
- Keyed adjustments verify the original serialized request and retained server decisions. Recording readback validates the finance decision issued by the authorized owner and allows a missing finance decision on a verified legacy retry. It checks the caller's finance redaction requirement against any stored allowance.
- Export recovery verifies the authorized CSV and rows against the original receipt. Legacy non-allow states retain their serialized representation. A changed disclosure result withholds delivery and preserves the original receipt.

## Review Triage Log

- Patched. Version 3 prevents older ledger readers from honoring submitted finance allowances on newly issued comments. The rollout order accounts for older evidence projectors rejecting the new version.
- Patched. Recording readback uses the owner's recorded finance decision without calling the live finance policy. The existing check of projection and external-display decisions uses its pre-submission snapshot.
- Verified. The keyed adjustment binds the raw redaction input in the command ID. A changed requirement fails the retry. Recording readback rejects a retained finance allowance when the retry requires export redaction.
- Patched. Covered legacy denied states, a retry after the authorized file changes, and a server-issued clearance through a corrected ledger history. The installed finance provider and live receipt qualification remain separate ledger entries.

## Verification

The Debug/source solution restored and built against the committed dependency revisions with zero warnings or errors. The focused projection, finance integration and keyed-adjustment runs passed 13, 28 and 3 cases respectively. The projection cases include malformed version 3 and unsupported versions. The keyed-adjustment cases cover retained version 2 and version 3 decisions and reject changed redaction input.

Architecture, Contracts and the serial Server suite passed 95, 109 and 829 cases. The existing submission-budget test missed its wall-clock window on two earlier runs and passed in the serial Server suite. The isolated AppHost baseline refused startup because the Dapr inventory was occupied or unsafe.
