# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StockPilot.Api.csproj .
RUN dotnet restore StockPilot.Api.csproj

COPY . .
RUN dotnet publish StockPilot.Api.csproj -c Release -o /app/publish -r linux-x64 --self-contained false -p:PublishReadyToRun=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

#RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StockPilot.Api.dll"]