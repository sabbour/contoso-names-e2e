## Contoso band name generator demo source code

- `frontend`: Frontend for the application, built in Node JS.
  - Exposes Prometheus metrics at `/metrics`
  - Metric name to look for `nodejs_active_requests_total `
- `service`: Backend for the application, built in .NET Core. Optionally uses a Redis cache when the `REDIS_PRIMARY_KEY`, `REDIS_HOSTNAME`, and `REDIS_PORT` environment variables are set.
  - Exposes Prometheus metrics at `/metrics`
  - Metric name to look for `http_requests_received_total`