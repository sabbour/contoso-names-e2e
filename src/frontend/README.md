# Contoso band name generator (frontend)

This is a Node.js application that uses the [Express](https://expressjs.com/) and [Next.js](https://nextjs.org/) frameworks. It exposes the following endpoints:

- `/` is the frontend of the application.
- `/metrics` exposes Prometheus metrics for scraping.


## Structure

There are 2 folders
- `/code` contains the application source code.
- `/manifests` contains the Kubernetes manifests needed to deploy the application.

## Code

The application starts a web server and listens on port 3000. It expects to communicate with another API to retrieve the names.

To configure the API endpoint, you need to pass in these environment variables:
- `NAME_API_SERVICE_HOST`
- `NAME_API_SERVICE_PORT`

## Build and run locally

Build using NPM

```
cd code
npm install
npm run build
```

Run locally passing the API host and port environment variables

```
export NAME_API_SERVICE_HOST="<your backend API hostname>"
export NAME_API_SERVICE_PORT="<your backend API port>"
npm start
```

then open <http://localhost:3000> in your browser.

## Build and run using containers

Build using Docker

```
cd code
docker build -t contoso-names-frontend .
```

Run using Docker 

```
docker run -it -p 8081:3000  \
--env=NAME_API_SERVICE_HOST="k" \
--env=NAME_API_SERVICE_PORT="80" \
contoso-names-frontend:latest
```

then open <http://localhost:8081> in your browser.

## Kubernetes manifests

The target deployment of the application is to a Kubernetes cluster. Notable configuration:

- The application should be deployed to the `contoso-names-app` namespace.
- The placeholder name of the image in [deployment.yaml](./manifests/deployment.yaml) `${SERVICE_FRONTEND_IMAGE_NAME}` will be replaced by CI/CD or using the Azure Developer CLI.
- The values of the `NAME_API_SERVICE_HOST` and `NAME_API_SERVICE_PORT` environment variables in [deployment.yaml](./manifests/deployment.yaml) are predefined to point to the `contoso-backend-service:80` by when deployed.
- The pod annotations in [deployment.yaml](./manifests/deployment.yaml) allows Prometheus to scrape the `/metrics` endpoint
    ```
    annotations:
        prometheus.io/scrape: "true"
        prometheus.io/scheme: "http"
        prometheus.io/path: "/metrics"
        prometheus.io/port: "80"
    ```