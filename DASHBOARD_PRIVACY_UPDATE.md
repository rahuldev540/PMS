# Dashboard privacy update

- GET /api/pharmacy-super-admin/dashboard now returns platform counts only.
- Removed revenue, sales, branch performance, medicine prices and financial metrics from this response.
- Disabled three legacy super-admin pharmacy revenue report/export endpoints with [NonAction].
- GET /api/pharmacy-admin/dashboard retains pharmacy-scoped financial and stock data.
- No database migrations required.

IMPORTANT: This is a targeted dashboard/revenue endpoint change, not a complete authorization audit of every controller. Verify other Super Admin APIs for sensitive financial fields. The existing Admin dashboard is primarily assigned-branch scoped; pharmacy-wide aggregation has not been added. Test and build in your .NET environment before deploying.
