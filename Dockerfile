FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /publish
COPY . .
RUN dotnet publish src/Ntrada.Host -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /ntrada
COPY --from=build /publish/out .
ENV ASPNETCORE_URLS http://*:80
ENV ASPNETCORE_ENVIRONMENT docker
ENTRYPOINT dotnet Ntrada.Host.dll