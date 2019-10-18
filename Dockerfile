FROM mcr.microsoft.com/dotnet/core/sdk:3.0k AS build
WORKDIR /publish
COPY . .
RUN dotnet publish src/Ntrada.Host -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /ntrada
COPY --from=build /publish/src/Ntrada.Host/out .
ENV ASPNETCORE_URLS http://*:5000
ENV ASPNETCORE_ENVIRONMENT docker
EXPOSE 5000
ENTRYPOINT dotnet Ntrada.Host.dll