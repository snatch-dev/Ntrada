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

No documentation yet, please take a look at the basic [ntrada.yml](https://github.com/Ntrada/Ntrada/blob/master/src/Ntrada.Host/ntrada.yml) configuration.

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
public static class Program
{
    public static async Task Main(string[] args)
        => await WebHost.CreateDefaultBuilder(args)
            .UseNtrada()
            .UseRabbitMq() // An optional extension
            .Build()
            .RunAsync();
}
```

More complex scenario can be found under [samples](https://github.com/Ntrada/Ntrada/tree/master/samples/Ntrada.Samples.Api) directory - it's using [modules](https://github.com/Ntrada/Ntrada/tree/master/samples/Ntrada.Samples.Api/Modules), message broker, authentication etc.
This sample requires [RabbitMQ](https://www.rabbitmq.com) up and running and provides API Gateway for [DShop](https://github.com/devmentors/DNC-DShop) project (a mirror of [DShop.Api](https://github.com/devmentors/DNC-DShop.Api) which is a standalone ASP.NET Core application).

----------------

**Advanced configuration**

```yml
use_error_handler: true
use_forwarded_headers: true
pass_query_string: true
modules_path: Modules
settings_path: Settings
payloads_folder: Payloads

resource_id:
  property: id
  generate: true

cors:
  enabled: true
  headers:
  - X-Operation
  - X-Resource
  - X-Total-Count

http:
  retries: 2
  interval: 2.0
  exponential: true

auth:
  type: jwt
  global: true
  jwt:
    key: JLBMU2VbJZmt42sUwByUpJJF6Y5mG2gPNU9sQFUpJFcGFJdyKxskR3bxh527kax2UcXHvB
    issuer: dshop-identity-service
    issuers:
    validate_issuer: true
    audience:
    audiences:
    validate_audience: false
    validate_lifetime: false
  claims:
    role: http://schemas.microsoft.com/ws/2008/06/identity/claims/role
#  policies:
#    admin:
#      claims:
#        role: admin
#    product_manager:
#      claims:
#        role: manager
#        access: create_product

extensions:
  dispatcher:
    use: rabbitmq
    configuration: rabbitmq.json

modules:
- name: home
  routes:
  - upstream: /
    method: GET
    use: return_value
    return_value: Welcome to DShop API.
    auth: false

```

----------------

**Additional modules**

```yml
name: Orders
path: /orders

routes:
- upstream: /
  method: GET
  use: downstream
  downstream: orders-service/orders?customerId=@user_id
  on_success:
    data: response.data.items
  
- upstream: /{id:guid}
  method: GET
  use: downstream
  downstream: orders-service/customers/@user_id/orders/{id}
  
- upstream: /
  method: POST
  use: dispatcher
  exchange: orders.create_order
  routing_key: create_order
  bind:
  - customerId:@user_id
  payload: create_order
  schema: create_order.schema
  
- upstream: /{id:guid}/complete
  method: POST
  use: dispatcher
  exchange: orders.complete_order
  routing_key: complete_order
  bind:
  - id:{id}
  - customerId:@user_id
  payload: complete_order
  schema: complete_order.schema
  
- upstream: /{id:guid}/approve
  method: POST
  use: dispatcher
  exchange: orders.approve_order
  routing_key: approve_order
  bind:
  - id:{id}
  payload: approve_order
  schema: approve_order.schema
  claims:
    role: admin
    
- upstream: /{id:guid}
  method: DELETE
  use: dispatcher
  exchange: orders.cancel_order
  routing_key: cancel_order
  bind:
  - id:{id}
  - customerId:@user_id
  payload: cancel_order
  schema: cancel_order.schema

services:
  orders-service:
    url: localhost:5005
#    url: localhost:9999/orders-service
```