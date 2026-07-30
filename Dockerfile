FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY FplApp.sln ./
COPY src/FplApp.Core/FplApp.Core.csproj src/FplApp.Core/
COPY src/FplApp.Api/FplApp.Api.csproj src/FplApp.Api/
RUN dotnet restore src/FplApp.Api/FplApp.Api.csproj

COPY src/ src/
RUN dotnet publish src/FplApp.Api/FplApp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FplApp.Api.dll"]
