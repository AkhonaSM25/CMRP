# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CMRP.csproj ./
RUN dotnet restore CMRP.csproj
COPY . .
RUN dotnet publish CMRP.csproj -c Release -o /app --no-restore

# Run stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
# The host provides PORT; the app creates and seeds its own SQLite database on start.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet CMRP.dll"]
