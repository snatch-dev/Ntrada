```
    / | / / /__________ _____/ /___ _
   /  |/ / __/ ___/ __ `/ __  / __ `/
  / /|  / /_/ /  / /_/ / /_/ / /_/ / 
 /_/ |_/\__/_/   \__,_/\__,_/\__,_/

 /___ API Gateway (Entrance) ___/
```
----------------


**`Ntrada`** (*entrada* is a spanish word meaning an entrance).

The aim of this project is to provide an easily configurable (via YML) and extendable (e.g. RabbitMQ integration) API Gateway, that requires no coding whatsoever and can be started via Docker or as .NET Core application.

### Features:

* Configuration via single file
* Separate modules definitions
* Static content
* Routing
* Request forwarding
* Headers forwarding
* Custom request bodies
* Request body validation
* Query string binding
* Request & response headers modification
* Basic request & response transformation
* Authentication
* Authorization
* HTTP retries
* Strongly-typed service names
* Extensibility with custom request handlers

### Extensions:

* JWT
* RabbitMQ integration
* Open Tracing with Jaeger
* CORS
* Custom errors


No documentation yet, please take a look at the basic [ntrada.yml](https://github.com/snatch-dev/Ntrada/blob/master/samples/Ntrada.Samples.Api/ntrada.yml) configuration.

```yml
modules:
- name: home
  routes:
  - upstream: /
    method: GET
    use: return_value
    return_value: Welcome to Ntrada API.
```
  
Start via Docker:

```
docker build -t ntrada .
docker run -it --rm --name ntrada -p 5000:5000 ntrada
curl localhost:5000
```

Or as .NET Core application:

```
cd src/Ntrada.Host/
dotnet run
curl localhost:5000
```

If you're willing to create your own application (instead of running it via Docker), it's all it takes to use Ntrada:

```csharp
public class Program
{
    public static Task Main(string[] args)
        => CreateHostBuilder(args).Build().RunAsync();

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureAppConfiguration(builder =>
                {
                    var configPath = args?.FirstOrDefault() ?? "ntrada.yml";
                    builder.AddYamlFile(configPath, false);
                }).UseStartup<Startup>();
            });
}
```

More **real-world examples** (modules, asynchronous messaging, load balancing etc.) can be found in the following projects:

* [Pacco](https://github.com/devmentors/Pacco.APIGateway)

----------------

**Advanced configuration**

```yml
auth:
  enabled: true
  global: false
  claims:
    role: http://schemas.microsoft.com/ws/2008/06/identity/claims/role

http:
  retries: 2
  interval: 2.0
  exponential: true

useErrorHandler: true
useJaeger: true
useForwardedHeaders: true
passQueryString: true
modulesPath: Modules
payloadsFolder: Payloads
forwardRequestHeaders: true
forwardResponseHeaders: true
generateRequestId: true
generateTraceId: true
useLocalUrl: true
loadBalancer:
  enabled: false
  url: localhost:9999

extensions:
  customErrors:
    includeExceptionMessage: true
  
  cors:
    allowCredentials: true
    allowedOrigins:
      - '*'
    allowedMethods:
      - post
      - delete
    allowedHeaders:
      - '*'
    exposedHeaders:
      - Request-ID
      - Resource-ID
      - Trace-ID
      - Total-Count
    
  jwt:
    key: eiquief5phee9pazo0Faegaez9gohThailiur5woy2befiech1oarai4aiLi6ahVecah3ie9Aiz6Peij
    issuer: ntrada
    issuers:
    validateIssuer: true
    audience:
    audiences:
    validateAudience: false
    validateLifetime: true
    
  rabbitmq:
    enabled: true
    connectionName: ntrada
    hostnames:
      - localhost
    port: 5672
    virtualHost: /
    username: guest
    password: guest
    requestedConnectionTimeout: 3000
    socketReadTimeout: 3000
    socketWriteTimeout: 3000
    requestedHeartbeat: 60
    exchange:
      declareExchange: true
      durable: true
      autoDelete: false
      type: topic
    messageContext:
      enabled: true
      header: message_context
    logger:
      enabled: true
    spanContextHeader: span_context

  tracing:
    serviceName: ntrada
    udpHost: localhost
    udpPort: 6831
    maxPacketSize: 0
    sampler: const
    useEmptyTracer: false

modules:
  home:
    routes:
      - upstream: /
        method: GET
        use: return_value
        returnValue: Welcome to Ntrada API!
        
      - upstream: /
        method: POST
        auth: false
        use: rabbitmq
        config:
          exchange: sample.exchange
          routing_key: sample.routing.key
```