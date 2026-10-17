# Data limits & alerts

If your plan has a monthly allowance (common on mobile broadband, hotspots and some fibre plans), WinSpeed can track it and warn you.

## Setting it up

**Settings → Data plan & alerts**:

| Setting | Meaning |
|---|---|
| **Monthly limit** | Your allowance in GB (0 = no limit). Decimals allowed, e.g. `75.5`. |
| **Billing cycle starts on day** | The day of the month your allowance resets (1–28). |
| **Daily limit** | Optional daily allowance in MB (0 = no limit). Useful for "fair use" daily caps. |
| **Warn me at** | The percentage that triggers the first warning (default 90 %). |

## What happens

- The **Overview** page shows the billing period's usage with a progress bar that turns orange at your warning level and red at 100 %.
- A **Windows notification** appears when you cross the warning percentage, and again at 100 %. Each alert fires once per billing period (or once per day for the daily limit).
- The meter's tooltip shows `x% of <limit>`.
- **Data usage → Range → Current billing period** shows the period day by day.

## Which traffic counts?

Only the **counted adapters** count (see [Network adapters](Network-Adapters)). By default that means your physical adapters: Ethernet, Wi-Fi and mobile broadband. If you use a phone hotspot only sometimes and want to track just that, choose **Only the adapters I tick** and select the hotspot's Wi-Fi adapter.

Changing a limit re-arms the alerts.

## Accuracy

WinSpeed measures traffic on this PC only. Your provider counts every device on the connection, and may count protocol overhead slightly differently. Expect WinSpeed's number to be a little lower than the bill, and set the warning level with some margin (80–90 %).
