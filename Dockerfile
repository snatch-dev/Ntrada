FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /publish
COPY . .
RUN dotnet publish src/Ntrada.Host -c Release -o out

FROM microsoft/dotnet:2.2-aspnetcore-runtime
WORKDIR /ntrada
COPY --from=build /publish/src/Ntrada.Host/out .
ENV ASPNETCORE_URLS http://*:5000
ENV ASPNETCORE_ENVIRONMENT docker
EXPOSE 5000
ENTRYPOINT dotnet Ntrada.Host.dll