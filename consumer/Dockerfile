FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY consumer/CdcConsumer.csproj consumer/
COPY src/Cdc.Extensions/Cdc.Extensions.csproj src/Cdc.Extensions/
RUN dotnet restore consumer/CdcConsumer.csproj
COPY consumer/ consumer/
COPY src/Cdc.Extensions/ src/Cdc.Extensions/
RUN dotnet publish consumer/CdcConsumer.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "CdcConsumer.dll"]
