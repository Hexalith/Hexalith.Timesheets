---
title: 'Issue independent finance permission for Time Entry comments'
type: 'bugfix'
created: '2026-11-24'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
---

<frozen-after-approval reason="Story 6.1 follow-up under AD-7">

## Intent

Use the server's existing comment publication authority to issue a separate decision for finance disclosure of the exact text. Finance output requires both that decision and the tenant export policy. A caller-supplied export allowance and an external-display decision cannot grant finance permission.

Legacy events retain their publication and external-display behavior. A missing finance decision excludes the text from export. Existing command retries must still verify the original stored effect. A receipt can authorize delivery only when it matches the content currently authorized for export.

</frozen-after-approval>

## Implementation Notes
