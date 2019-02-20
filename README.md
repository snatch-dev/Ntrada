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

More advanced scenario can be found under [samples](https://github.com/Ntrada/Ntrada/tree/master/samples/Ntrada.Samples.Api) directory - it's using [modules](https://github.com/Ntrada/Ntrada/tree/master/samples/Ntrada.Samples.Api/Modules), message broker, authentication etc.
This sample requires [RabbitMQ](https://www.rabbitmq.com) up and running and provides API Gateway for [DShop](https://github.com/devmentors/DNC-DShop) project (a mirror of [DShop.Api](https://github.com/devmentors/DNC-DShop.Api) which is a standalone ASP.NET Core application).