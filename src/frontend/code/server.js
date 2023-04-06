const next = require('next');
const express = require('express');
const prometheus = require('prom-client');
const promBundle = require("express-prom-bundle");
const bodyParser = require("body-parser");
const http = require('http');

const dev = process.env.NODE_ENV !== 'production';
const app = next({ dev });
const handle = app.getRequestHandler();

// Add the options to the prometheus middleware most option are for http_request_duration_seconds histogram metric
const metricsMiddleware = promBundle({
    includeMethod: true, 
    includePath: true, 
    includeStatusCode: true, 
    includeUp: true,
    customLabels: {project_name: 'contoso-names-frontend', project_type: 'frontend'},
    promClient: {
        collectDefaultMetrics: {
        }
      }
});


app.prepare().then(() => {
    const server = express();
    server.use(bodyParser.json());
    
    // add the prometheus middleware to all routes
    server.use(metricsMiddleware);

    // Prometheus metrics scraping endpoint
    server.get('/metrics', async (req, res) => {
        res.setHeader('Content-Type', register.contentType);
        res.send(await register.metrics());
    });

    server.get("/api/names", function (req, res) {
        console.log("server.get /api/names")
       requestData(req, process.env.NAME_API_SERVICE_HOST, process.env.NAME_API_SERVICE_PORT, '/names', 'GET', null, (data, error) => {
            if (error != null) {
                res.status(500).send(error);
                return;
            }
            res.send(data);
        });
    });

    server.get('/', (req, res) => {
        console.log("Serving index");
        return app.render(req, res, '/index', {});
    });

    server.get('*', (req, res) => {
        return handle(req, res);
    });

    const port = process.env.PORT || 3000;
    server.listen(port, err => {
        if (err) throw err;
    });

    process.on("SIGINT", () => {
        process.exit(130 /* 128 + SIGINT */);
    });

    process.on("SIGTERM", () => {
        bus.close();
        server.close(() => {
            process.exit(143 /* 128 + SIGTERM */);
        });
    });
});

// Function to request data from backend API
// Checks for the existence of the kubernetes-route-as header and forwards it to the backend API if it exists
// This header is used by the Bridge to Kubernetes routing manager to perform traffic isolation when debugging
function requestData(initialRequest, host, port, path, method, bodyObject, responseHandler) {
    console.log("%s - %s:%s%s", method, host, port, path);
    var options = {
        host: host,
        port: port,
        path: path,
        method: method,
        headers: {'content-type': 'application/json'}
    };
    const routeAsValue = initialRequest.get('kubernetes-route-as');
    if (routeAsValue) {
        console.log('Forwarding kubernetes-route-as header value: %s', routeAsValue);
        options.headers['kubernetes-route-as'] = routeAsValue;
    } else {
        console.log('No kubernetes-route-as header value to forward');
    }
    var newRequest = http.request(options, function(statResponse) {
        var responseString = '';
        //another chunk of data has been received, so append it to `responseString`
        statResponse.on('data', function (chunk) {
            responseString += chunk;
        });
        statResponse.on('end', function () {
            console.log('Response: %s', responseString);
            var responseObject;
            try {
                responseObject = JSON.parse(responseString);
            }
            catch (error) {
                responseObject = null;
            }
            responseHandler(responseObject, null);
        });
    });

    newRequest.on('error', function (error) {
        console.log('Request error: ' + error);
        responseHandler(null, error.message);
    });

    if (bodyObject != null) {
        newRequest.ContentType = 'application/json';
        newRequest.write(JSON.stringify(bodyObject));
    }

    newRequest.end();
}