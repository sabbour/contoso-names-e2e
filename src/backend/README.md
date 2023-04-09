# Contoso band name generator (backend)

This is a .NET Core 7 application. It exposes the following endpoints:

- `/names` generates a random combination of names. Optionally stores and retrieves the names from an Azure Table Storage.
- `/metrics` exposes Prometheus metrics for scraping.
- `/swagger` shows the Swagger UI.


## Structure

There are 2 folders
- `/code` contains the application source code.
- `/manifests` contains the Kubernetes manifests needed to deploy the application.

## Code

The application starts a Kestrel web server and listens on port 80.

To configure the Azure Table Storage, you need to pass in these environment variables:
- `STORAGE_ACCOUNT_KEY`
- `STORAGE_ACCOUNT_BLOB_ENDPOINT`

### Build and run locally

Follow the [.NET installation instructions](https://learn.microsoft.com/en-us/dotnet/core/install/) for your platform.

Run locally

```
cd code
dotnet run
```

Run locally with Azure Table Storage

```
dotnet run \
--STORAGE_ACCOUNT_BLOB_ENDPOINT="https://<your storage account>.blob.core.windows.net" \
--STORAGE_ACCOUNT_KEY="<your primary key>"
```

Open the Swagger UI <http://localhost:5129/swagger> or view Prometheus metrics <http://localhost:5129/metrics>. Port `5129` is defined in [Properties/launchSettings.json](./code/Properties/launchSettings.json)

### Build and run using containers

Build using Docker

```
cd code
docker build -t contoso-names-backend .
```

Run using Docker without Azure Table Storage cache

```
docker run -it -p 8080:80 contoso-names-backend:latest
```

Run using Docker with Azure Table Storage cache

```
docker run -it -p 8080:80 \
--env=STORAGE_ACCOUNT_BLOB_ENDPOINT="https://<your storage account>.blob.core.windows.net" \
--env=STORAGE_ACCOUNT_KEY="<your primary key>" \
contoso-names-backend:latest 
```

Open the Swagger UI <http://localhost:8080/swagger> or view Prometheus metrics <http://localhost:8080/metrics>

## Kubernetes manifests

The target deployment of the application is to a Kubernetes cluster. Notable configuration:

- The application should be deployed to the `contoso-names-app` namespace.
- The placeholder name of the image in [deployment.yaml](./manifests/deployment.yaml) `${SERVICE_BACKEND_IMAGE_NAME}` will be replaced by CI/CD or using the Azure Developer CLI.
- The `storageaccount-secret` that is used to set the various Azure Table Storage envionment variables in [deployment.yaml](./manifests/deployment.yaml) is created by the Azure Service Operator.
- [Kubernetes Event Driven Autoscaler (KEDA)](https://keda.sh) is used for scaling as defined in [scaledobject.yaml](./manifests/scaledobject.yaml). The scaling trigger is using the [Prometheus scaler](https://keda.sh/docs/2.10/scalers/prometheus/) for the `http_requests_received_total` metric. The placeholder for the Prometheus endpoint `${PROMETHEUS_ENDPOINT}` will be replaced by CI/CD or using the Azure Developer CLI.
-  [Azure Service Operator](https://azure.github.io/azure-service-operator/) is used  to create an Azure Table Storage as defined in [storageaccount.yaml](../manifests/azure-resources/storageaccount.yaml). The placeholders for the resource group name `${RESOURCE_GROUP_NAME}` and location `${RESOURCE_GROUP_LOCATION}`  will be replaced by CI/CD or using the Azure Developer CLI.
- The pod annotations in [deployment.yaml](./manifests/deployment.yaml) allows Prometheus to scrape the `/metrics` endpoint
    ```
    annotations:
        prometheus.io/scrape: "true"
        prometheus.io/scheme: "http"
        prometheus.io/path: "/metrics"
        prometheus.io/port: "80"
    ```